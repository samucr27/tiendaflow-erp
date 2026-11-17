using TiendaFlow.Application.Security;

namespace TiendaFlow.Application.Reporting;

public sealed record SecurityActionCount(string Action, string Label, int Count);

public sealed record SecurityReport(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<SecurityActionCount> Actions,
    IReadOnlyList<AuditTrailRow> DeniedAccesses)
{
    public int Count(string action) => Actions.FirstOrDefault(item => item.Action == action)?.Count ?? 0;

    public string ToCsv() =>
        CsvReport.Build(
            ["Evento", "Accion", "Cantidad"],
            Actions.Select(item => new[] { item.Label, item.Action, item.Count.ToString() }));
}

/// <summary>
/// Security report (security.reports.view): sign-ins, failed sign-ins, denied
/// accesses and changes to users and roles in a local-date range, built from
/// the audit trail.
/// </summary>
public sealed class SecurityReportService
{
    private static readonly (string Action, string Label)[] Tracked =
    {
        ("LoginSucceeded", "Inicios de sesión correctos"),
        ("LoginFailed", "Inicios de sesión fallidos"),
        ("AccessDenied", "Accesos denegados"),
        ("UserCreated", "Usuarios creados"),
        ("UserUpdated", "Usuarios modificados"),
        ("UserDeactivated", "Usuarios desactivados"),
        ("PasswordReset", "Contraseñas restablecidas"),
        ("RolePermissionsUpdated", "Cambios en permisos de roles"),
        ("RolePermissionsReset", "Roles restablecidos")
    };

    private readonly IAuditTrailQuery _audit;
    private readonly IAccessControl _accessControl;

    public SecurityReportService(IAuditTrailQuery audit, IAccessControl accessControl)
    {
        _audit = audit;
        _accessControl = accessControl;
    }

    public SecurityReport Get(DateOnly fromDate, DateOnly toDate)
    {
        _accessControl.Demand(Permissions.SecurityReportsView);

        if (fromDate > toDate)
            throw new ArgumentException("The start date cannot be after the end date.");

        var rows = _audit.Between(LocalTime.ToUtc(fromDate), LocalTime.ToUtc(toDate.AddDays(1)));

        var counts = Tracked
            .Select(item => new SecurityActionCount(item.Action, item.Label,
                rows.Count(row => row.Action == item.Action)))
            .ToList();

        var denied = rows.Where(row => row.Action == "AccessDenied").Take(50).ToList();

        return new SecurityReport(fromDate, toDate, counts, denied);
    }
}
