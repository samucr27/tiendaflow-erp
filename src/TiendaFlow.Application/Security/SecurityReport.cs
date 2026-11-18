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
    IReadOnlyList<AuditTrailRow> Events)
{
    public string ToCsv() =>
        CsvReport.Build(
            ["Fecha", "Hora", "Evento", "Modulo", "Usuario", "IP", "Detalle"],
            Events.Select(entry =>
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
}

/// <summary>
/// Read side of the security report. It returns audit rows already ordered by
/// instant and capped, so a long-lived audit table cannot exhaust memory. The
/// date range is expressed in UTC instants; grouping into business days happens
/// in the application layer where the Colombian time zone is known.
/// </summary>
public interface ISecurityEventQuery
{
    IReadOnlyList<AuditTrailRow> InRange(DateTime fromUtc, DateTime toUtc);
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

        var events = _query
            .InRange(LocalTime.ToUtc(from), LocalTime.ToUtc(to.AddDays(1)))
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
            events);
    }
}
