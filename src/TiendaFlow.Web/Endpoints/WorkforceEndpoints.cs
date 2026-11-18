using System.Globalization;
using System.Text;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Endpoints;

/// <summary>
/// CSV downloads for the workforce screens. Each endpoint carries the same
/// policy as its page, so a hidden menu link is never the only guard. The CSV
/// builder in the application layer neutralizes spreadsheet formulas and quotes
/// separators; this class only chooses the file name and the encoding.
/// </summary>
public static class WorkforceEndpoints
{
    public static IEndpointRouteBuilder MapWorkforceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rrhh/asistencia/export.csv", (
            string? from,
            string? to,
            int? storeId,
            AttendanceService attendance,
            IClock clock) =>
        {
            try
            {
                if (!TryParseRange(from, to, clock.UtcNow, out var start, out var end))
                    return Results.BadRequest(new { error = "Las fechas deben tener el formato aaaa-MM-dd." });

                return Csv(
                    $"asistencia-{start:yyyyMMdd}-{end:yyyyMMdd}.csv",
                    attendance.SummaryCsv(start, end, storeId));
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (InvalidAttendanceException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization(Permissions.HrAttendanceManage);

        app.MapGet("/rrhh/turnos/reporte/export.csv", (
            string? from,
            string? to,
            int? storeId,
            ShiftService shifts,
            IClock clock) =>
        {
            try
            {
                if (!TryParseRange(from, to, clock.UtcNow, out var start, out var end))
                    return Results.BadRequest(new { error = "Las fechas deben tener el formato aaaa-MM-dd." });

                return Csv(
                    $"turnos-vs-asistencia-{start:yyyyMMdd}-{end:yyyyMMdd}.csv",
                    shifts.AttendanceReportCsv(start, end, storeId));
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (InvalidShiftException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization(Permissions.HrShiftsManage);

        return app;
    }

    private static bool TryParseRange(
        string? from,
        string? to,
        DateTime utcNow,
        out DateOnly start,
        out DateOnly end)
    {
        var today = DateOnly.FromDateTime(LocalTime.ToLocal(utcNow));
        start = today.AddDays(-30);
        end = today;

        if (from is not null && !DateOnly.TryParseExact(
                from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            return false;
        }

        if (to is not null && !DateOnly.TryParseExact(
                to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
        {
            return false;
        }

        return true;
    }

    private static IResult Csv(string fileName, string content)
    {
        // The UTF-8 BOM makes Excel open the accents correctly.
        var payload = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(content))
            .ToArray();

        return Results.File(payload, "text/csv; charset=utf-8", fileName);
    }
}
