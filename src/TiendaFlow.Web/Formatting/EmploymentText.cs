using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

public static class EmploymentText
{
    public static string ContractStatus(EmploymentContractStatus status) => status switch
    {
        EmploymentContractStatus.Active => "Vigente",
        EmploymentContractStatus.Extended => "Prorrogado",
        EmploymentContractStatus.Terminated => "Terminado",
        _ => status.ToString()
    };

    public static string ContractTone(EmploymentContractStatus status) => status switch
    {
        EmploymentContractStatus.Active => "ok",
        EmploymentContractStatus.Extended => "warn",
        EmploymentContractStatus.Terminated => "neutral",
        _ => "neutral"
    };
}
