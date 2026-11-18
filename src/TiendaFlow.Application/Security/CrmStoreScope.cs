using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Store scope for the CRM screens that belong to a branch: the loyalty ledger
/// written by cashiers and the PQRS cases handled by store managers. Corporate
/// roles keep the global view. The rule runs inside the service before any
/// repository is queried, so a forged store id in the UI or the CSV query string
/// cannot reveal another branch's customers.
/// </summary>
public static class CrmStoreScope
{
    private const string DenialCode = "crm.store.scope";

    public static bool IsGlobal(string role) =>
        role is RoleNames.Administrator or RoleNames.CorporateManager;

    public static int? Restrict(ICurrentUser user, int? storeId)
    {
        if (IsGlobal(user.Role))
            return storeId;

        var ownStoreId = user.StoreId
            ?? throw new AccessDeniedException(DenialCode);

        if (storeId is not null && storeId != ownStoreId)
            throw new AccessDeniedException(DenialCode);

        return ownStoreId;
    }

    public static void Demand(ICurrentUser user, int storeId)
    {
        if (IsGlobal(user.Role))
            return;

        if (user.StoreId is null || user.StoreId != storeId)
            throw new AccessDeniedException(DenialCode);
    }
}
