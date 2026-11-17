using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Store-level scope for inventory documents. The corporate manager and the
/// administrator work across every store; the store manager and the stock clerk
/// only work on documents whose store is their own. The rule lives here so the
/// application service enforces it before touching a repository, not only the
/// screen that hides the option.
/// </summary>
public static class StoreScope
{
    public static bool IsGlobal(string role) =>
        role is RoleNames.Administrator or RoleNames.CorporateManager;

    /// <summary>
    /// Forces a list filter to the current user's store and rejects an explicit
    /// request for another store. Global roles keep the requested filter.
    /// </summary>
    public static int? Restrict(ICurrentUser user, int? storeId)
    {
        if (IsGlobal(user.Role))
            return storeId;

        var ownStoreId = user.StoreId
            ?? throw new AccessDeniedException("inventory.store.scope");

        if (storeId is not null && storeId != ownStoreId)
            throw new AccessDeniedException("inventory.store.scope");

        return ownStoreId;
    }

    /// <summary>
    /// Denies the operation when a scoped user targets a store that is not their
    /// own. A user without a store cannot operate on store documents.
    /// </summary>
    public static void Demand(ICurrentUser user, int storeId)
    {
        if (IsGlobal(user.Role))
            return;

        if (user.StoreId is null || user.StoreId != storeId)
            throw new AccessDeniedException("inventory.store.scope");
    }
}
