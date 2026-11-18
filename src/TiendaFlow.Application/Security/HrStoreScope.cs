using TiendaFlow.Domain;
namespace TiendaFlow.Application.Security;

/// <summary>
/// Store scope for the HR screens. The human resources officer works across
/// every store, like the administrator and the corporate manager; the store
/// manager only schedules and records attendance for their own store. The rule
/// lives in the application layer so it is enforced before any repository call,
/// not only by hiding options in the page.
/// </summary>
public static class HrStoreScope
{
    public static bool IsGlobal(string role) =>
        role is RoleNames.Administrator
            or RoleNames.CorporateManager
            or RoleNames.HumanResourcesOfficer;

    /// <summary>
    /// Forces a list filter to the current user's store and rejects an explicit
    /// request for another store. Global HR roles keep the requested filter.
    /// </summary>
    public static int? Restrict(ICurrentUser user, int? storeId)
    {
        if (IsGlobal(user.Role))
            return storeId;

        var ownStoreId = user.StoreId
            ?? throw new AccessDeniedException("hr.store.scope");

        if (storeId is not null && storeId != ownStoreId)
            throw new AccessDeniedException("hr.store.scope");

        return ownStoreId;
    }

    /// <summary>
    /// Denies the operation when a scoped user targets a store that is not their
    /// own. A user without a store cannot operate HR documents of a store.
    /// </summary>
    public static void Demand(ICurrentUser user, int storeId)
    {
        if (IsGlobal(user.Role))
            return;

        if (user.StoreId is null || user.StoreId != storeId)
            throw new AccessDeniedException("hr.store.scope");
    }
}
