using System.Globalization;
using System.Text;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Endpoints;

/// <summary>
/// CSV downloads for the CRM screens. Every endpoint repeats the policy of its
/// page and the service repeats it in `Demand`, so hiding a link is never the
/// only control. The workbook-safe writer lives in the application layer; this
/// class only chooses the file name and the UTF-8 BOM.
/// </summary>
public static class CrmEndpoints
{
    public static IEndpointRouteBuilder MapCrmEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/crm/puntos/export.csv", (
            string? from,
            string? to,
            int? storeId,
            int? customerId,
            LoyaltyService loyalty,
            IClock clock) =>
        {
            try
            {
                if (!TryParseRange(from, to, clock.UtcNow, out var start, out var end))
                    return Results.BadRequest(new { error = "Las fechas deben tener el formato aaaa-MM-dd." });

                return Csv(
                    $"puntos-fidelizacion-{start:yyyyMMdd}-{end:yyyyMMdd}.csv",
                    loyalty.LedgerCsv(start, end, storeId, customerId));
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (Exception exception) when (exception is InvalidLoyaltyMovementException
                                                  or LoyaltyReferenceAlreadyUsedException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization(Permissions.CrmPointsManage);

        app.MapGet("/crm/pqrs/export.csv", (
            int? storeId,
            PqrsStatus? status,
            PqrsService pqrs) =>
        {
            try
            {
                return Csv("pqrs.csv", pqrs.CasesCsv(storeId, status));
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (Exception exception) when (exception is InvalidPqrsException
                                                  or PqrsNotFoundException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization(Permissions.CrmPqrsManage);

        app.MapGet("/crm/campanas/export.csv", (CampaignService campaigns) =>
        {
            try
            {
                return Csv("campanas.csv", campaigns.CampaignsCsv());
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (Exception exception) when (exception is InvalidCampaignException
                                                  or DuplicateCampaignNameException
                                                  or CampaignNotFoundException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization(Permissions.CrmCampaignsManage);

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
