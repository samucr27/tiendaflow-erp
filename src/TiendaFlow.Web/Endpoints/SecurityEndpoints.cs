using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Endpoints;

/// <summary>
/// CSV download for the aggregated security report. The endpoint repeats the
/// page policy and the service repeats it with <c>Demand</c>, so typing the URL
/// never bypasses authorization. The workbook-safe writer lives in the
/// application layer; this class only chooses the name and the UTF-8 BOM.
/// </summary>
public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/seguridad/reportes/export.csv", (
            SecurityReportService reports,
            [FromQuery] string? from,
            [FromQuery] string? to) =>
        {
            var today = DateOnly.FromDateTime(LocalTime.ToLocal(DateTime.UtcNow));

            if (!TryParse(from, new DateOnly(today.Year, today.Month, 1), out var startDate)
                || !TryParse(to, today, out var endDate))
            {
                return Results.BadRequest(new { error = "Las fechas deben tener el formato aaaa-MM-dd." });
            }

            try
            {
                var report = reports.Get(startDate, endDate);

                return Csv(
                    $"seguridad-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv",
                    report.ToCsv());
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "La fecha inicial no puede ser posterior a la final." });
            }
        }).RequireAuthorization(Permissions.SecurityReportsView);

        return app;
    }

    private static bool TryParse(string? value, DateOnly fallback, out DateOnly parsed)
    {
        if (value is null)
        {
            parsed = fallback;
            return true;
        }

        return DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
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
