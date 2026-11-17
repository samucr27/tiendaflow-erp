using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

/// <summary>Spanish labels for the HR and CRM closing modules.</summary>
public static class HrText
{
    public static string Attendance(AttendanceType type) => type switch
    {
        AttendanceType.Worked => "Trabajado",
        AttendanceType.Absence => "Ausencia",
        AttendanceType.SickLeave => "Incapacidad",
        AttendanceType.Permission => "Permiso",
        AttendanceType.Vacation => "Vacaciones",
        _ => type.ToString()
    };

    public static string AttendanceTone(AttendanceType type) => type switch
    {
        AttendanceType.Worked => "ok",
        AttendanceType.Absence => "danger",
        _ => "warning"
    };

    public static string Pqrs(PqrsType type) => type switch
    {
        PqrsType.Petition => "Petición",
        PqrsType.Complaint => "Queja",
        PqrsType.Claim => "Reclamo",
        PqrsType.Suggestion => "Sugerencia",
        _ => type.ToString()
    };

    public static string PqrsStatusText(PqrsStatus status) => status switch
    {
        PqrsStatus.Open => "Abierta",
        PqrsStatus.InProgress => "En trámite",
        PqrsStatus.Closed => "Cerrada",
        _ => status.ToString()
    };

    public static string PqrsTone(PqrsStatus status, bool overdue) =>
        overdue ? "danger" : status switch
        {
            PqrsStatus.Open => "warning",
            PqrsStatus.InProgress => "info",
            _ => "ok"
        };
}
