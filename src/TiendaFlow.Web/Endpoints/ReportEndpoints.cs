using System.Text;
using Microsoft.AspNetCore.Mvc;
using TiendaFlow.Application;
using TiendaFlow.Application.Invoicing;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Endpoints;

/// <summary>
/// CSV downloads for the report screens. Each endpoint carries the same policy
/// as its page, so a user cannot fetch a report by typing the URL even when the
/// page is hidden from the menu.
/// </summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reportes/inventario/export.csv", (
            InventoryReportService reports,
            ICurrentUser currentUser) =>
        {
            try
            {
                var report = reports.Get(currentUser.StoreId ?? 0);
                return Csv($"inventario-{Stamp()}.csv", report.ToCsv());
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest(new { error = "La tienda del usuario no es válida." });
            }
        }).RequireAuthorization(Permissions.InventoryReportsView);

        app.MapGet("/reportes/ventas/export.csv", (
            SalesReportService reports,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to) =>
        {
            var today = DateOnly.FromDateTime(LocalTime.ToLocal(DateTime.UtcNow));
            var fromDate = from ?? new DateOnly(today.Year, today.Month, 1);
            var toDate = to ?? today;

            try
            {
                var report = reports.Get(fromDate, toDate);
                return Csv($"ventas-{Stamp()}.csv", report.ToCsv());
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "La fecha inicial no puede ser posterior a la final." });
            }
        }).RequireAuthorization(Permissions.SalesReportsView);

        app.MapGet("/reportes/nomina/export.csv", (
            PayrollRunService payroll,
            [FromQuery] int? days,
            [FromQuery] int? risk) =>
        {
            try
            {
                var run = payroll.Calculate(days ?? 30, risk ?? 1);
                return Csv($"nomina-{Stamp()}.csv", PayrollReportCsv.Build(run));
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (InvalidPayrollInputException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization(Permissions.HrPayrollManage);

        app.MapGet("/facturacion/{id:int}/xml", (
            int id,
            ElectronicInvoiceService electronicInvoices) =>
        {
            try
            {
                var document = electronicInvoices.Generate(id);
                return Results.File(
                    Encoding.UTF8.GetBytes(document.Xml),
                    "application/xml; charset=utf-8",
                    $"{document.InvoiceNumber}-simulada.xml");
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (InvoiceNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidInvoiceException)
            {
                return Results.BadRequest(new { error = "Una factura anulada no se puede exportar." });
            }
        }).RequireAuthorization(Permissions.SalesInvoicesView);

        return app;
    }

    private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMdd");

    private static IResult Csv(string fileName, string content)
    {
        // The UTF-8 BOM makes Excel open the accents and the peso signs correctly.
        var payload = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(content))
            .ToArray();

        return Results.File(payload, "text/csv; charset=utf-8", fileName);
    }
}
