using System.Globalization;
using TiendaFlow.Application.Reporting;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Aggregated security activity for one inclusive local-date range. The
/// classification is intentionally explicit: only these audit actions count as
/// access or user-change events, and every other action still contributes to
/// the total so the report cannot silently drop activity it does not know.
/// </summary>
public static class SecurityAuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string AccessDenied = "AccessDenied";

    public static readonly string[] UserChanges =
    [
        "UserCreated",
        "UserUpdated",
        "UserDeactivated",
        "PasswordReset"
    ];

    public static bool IsUserChange(string action) =>
        UserChanges.Contains(action, StringComparer.Ordinal);
}

public sealed record SecurityDailyRow(
    DateOnly Date,
    int SuccessfulLogins,
    int FailedLogins,
    int AccessDenials,
    int UserChanges,
    int TotalEvents);

public sealed record SecurityReport(
    DateOnly From,
    DateOnly To,
    int SuccessfulLogins,
    int FailedLogins,
    int AccessDenials,
    int UserChanges,
    int TotalEvents,
    int DistinctActors,
    IReadOnlyList<SecurityDailyRow> Daily,
    IReadOnlyList<AuditTrailRow> Events,
    bool Truncated)
{
    /// <summary>
    /// Shared by the page and the CSV so a partial report is never mistaken for
    /// a complete count. The number matches <see cref="SecurityEventWindow.MaxEvents"/>.
    /// </summary>
    public const string TruncationNotice =
        "Mostrando los 5.000 eventos más recientes del rango seleccionado. Acota el rango de fechas para ver el detalle completo.";

    public string ToCsv()
    {
        var rows = new List<IReadOnlyList<string>>();

        if (Truncated)
            rows.Add([TruncationNotice]);

        rows.AddRange(Events.Select(entry =>
        {
            var local = LocalTime.ToLocal(entry.Date);

            return new[]
            {
                local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                local.ToString("HH:mm", CultureInfo.InvariantCulture),
                entry.Action,
                entry.Module,
                entry.UserName ?? string.Empty,
                entry.Ip ?? string.Empty,
                entry.Detail
            };
        }));

        return CsvReport.Build(
            ["Fecha", "Hora", "Evento", "Modulo", "Usuario", "IP", "Detalle"],
            rows);
    }
}

/// <summary>
/// Result of a capped audit query. Implementations fetch one probe row beyond
/// the limit and call <see cref="SplitProbe"/> to learn whether the range was
/// truncated, avoiding a separate COUNT over the whole audit log.
/// </summary>
public sealed record SecurityEventWindow(
    IReadOnlyList<AuditTrailRow> Events,
    bool Truncated)
{
    public const int MaxEvents = 5000;

    /// <summary>
    /// Splits a probe result already ordered newest first. When the extra row is
    /// present it is discarded, so the caller keeps exactly the newest
    /// <paramref name="maxEvents"/> rows and can flag the report as partial.
    /// </summary>
    public static (IReadOnlyList<T> Kept, bool Truncated) SplitProbe<T>(
        IReadOnlyList<T> newestFirst,
        int maxEvents = MaxEvents)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);

        if (maxEvents < 0)
            throw new ArgumentOutOfRangeException(nameof(maxEvents));

        var truncated = newestFirst.Count > maxEvents;

        return (
            truncated ? newestFirst.Take(maxEvents).ToArray() : newestFirst.ToArray(),
            truncated);
    }
}

/// <summary>
/// Read side of the security report. It returns at most
/// <see cref="SecurityEventWindow.MaxEvents"/> audit rows, plus a flag that says
/// whether the range held more. The date range is expressed in UTC instants;
/// grouping into business days happens in the application layer where the
/// Colombian time zone is known.
/// </summary>
public interface ISecurityEventQuery
{
    SecurityEventWindow InRange(DateTime fromUtc, DateTime toUtc);
}

public sealed class SecurityReportService
{
    private readonly ISecurityEventQuery _query;
    private readonly IAccessControl _accessControl;

    public SecurityReportService(ISecurityEventQuery query, IAccessControl accessControl)
    {
        _query = query;
        _accessControl = accessControl;
    }

    public SecurityReport Get(DateOnly from, DateOnly to)
    {
        _accessControl.Demand(Permissions.SecurityReportsView);

        if (from > to)
            throw new ArgumentException("The start date cannot be after the end date.");

        var window = _query.InRange(LocalTime.ToUtc(from), LocalTime.ToUtc(to.AddDays(1)));

        var events = window.Events
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Id)
            .ToList();

        var daily = events
            .GroupBy(entry => DateOnly.FromDateTime(LocalTime.ToLocal(entry.Date)))
            .OrderBy(group => group.Key)
            .Select(group => new SecurityDailyRow(
                group.Key,
                group.Count(entry => entry.Action == SecurityAuditActions.LoginSucceeded),
                group.Count(entry => entry.Action == SecurityAuditActions.LoginFailed),
                group.Count(entry => entry.Action == SecurityAuditActions.AccessDenied),
                group.Count(entry => SecurityAuditActions.IsUserChange(entry.Action)),
                group.Count()))
            .ToList();

        return new SecurityReport(
            from,
            to,
            events.Count(entry => entry.Action == SecurityAuditActions.LoginSucceeded),
            events.Count(entry => entry.Action == SecurityAuditActions.LoginFailed),
            events.Count(entry => entry.Action == SecurityAuditActions.AccessDenied),
            events.Count(entry => SecurityAuditActions.IsUserChange(entry.Action)),
            events.Count,
            events
                .Where(entry => entry.UserId is not null)
                .Select(entry => entry.UserId!.Value)
                .Distinct()
                .Count(),
            daily,
            events,
            window.Truncated);
    }
}
