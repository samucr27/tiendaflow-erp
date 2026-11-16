using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TiendaFlow.Infrastructure.Persistence;

/// <summary>
/// Blazor Server keeps one DbContext for the whole circuit (the browser tab),
/// so an entity loaded minutes ago stays in the change tracker. A tracked
/// query returns that cached instance and ignores newer values in the
/// database, which made two cashiers overwrite each other's stock. Repository
/// loads that are about to be modified go through this helper: an unchanged
/// tracked copy (and its loaded child collections) is forgotten so the query
/// reads the current row. A copy with pending changes belongs to the current
/// operation and is returned as is.
/// </summary>
internal static class FreshTracking
{
    public static TEntity? LoadFresh<TEntity>(
        this DbContext context,
        Func<TEntity, bool> isSameEntity,
        Func<TEntity?> load)
        where TEntity : class
    {
        var tracked = context.Set<TEntity>().Local.FirstOrDefault(isSameEntity);

        if (tracked is not null)
        {
            var entry = context.Entry(tracked);

            if (HasPendingChanges(entry))
                return tracked;

            Forget(entry);
        }

        return load();
    }

    private static bool HasPendingChanges(EntityEntry entry) =>
        entry.State != EntityState.Unchanged
        || entry.Collections
            .Where(collection => collection.CurrentValue is not null)
            .SelectMany(collection => collection.CurrentValue!.Cast<object>())
            .Any(child => entry.Context.Entry(child).State != EntityState.Unchanged);

    private static void Forget(EntityEntry entry)
    {
        foreach (var collection in entry.Collections)
        {
            if (collection.CurrentValue is null)
                continue;

            foreach (var child in collection.CurrentValue.Cast<object>().ToList())
                entry.Context.Entry(child).State = EntityState.Detached;
        }

        entry.State = EntityState.Detached;
    }
}
