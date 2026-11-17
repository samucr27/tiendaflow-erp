using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Endpoints;

/// <summary>
/// Downloads the documents attached to an employment contract. The file can
/// contain personal data, so the endpoint demands the same permission the
/// screen requires and the service writes an audit entry for every download.
/// </summary>
public static class ContractEndpoints
{
    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rrhh/contratos/{contractId:int}/adjunto/{attachmentId:int}", (
            int contractId,
            int attachmentId,
            EmploymentContractService contracts) =>
        {
            try
            {
                var download = contracts.DownloadAttachment(contractId, attachmentId);

                return Results.File(
                    download.Content,
                    download.ContentType,
                    download.FileName);
            }
            catch (AccessDeniedException)
            {
                return Results.Forbid();
            }
            catch (ContractAttachmentNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization(Permissions.HrContractsManage);

        return app;
    }
}
