using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core store for the closing modules. Entities are keyed by an int "Id";
/// child collections load through AutoInclude in their configuration.
/// </summary>
public sealed class EfEntityStore<T> : IEntityStore<T> where T : class
{
    private readonly TiendaFlowDbContext _context;

    public EfEntityStore(TiendaFlowDbContext context) => _context = context;

    public T? Get(int id) =>
        _context.LoadFresh<T>(
            entity => IdOf(entity) == id,
            () => _context.Set<T>().FirstOrDefault(entity => EF.Property<int>(entity, "Id") == id));

    public IReadOnlyList<T> All() => _context.Set<T>().AsNoTracking().ToList();

    public void Add(T entity)
    {
        _context.Set<T>().Add(entity);
        _context.SaveChanges();
    }

    public void Update(T entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
            _context.Set<T>().Update(entity);

        _context.SaveChanges();
    }

    private int IdOf(T entity) => (int)_context.Entry(entity).Property("Id").CurrentValue!;
}

public sealed class EfRolePermissionStore : IRolePermissionStore
{
    private readonly TiendaFlowDbContext _context;

    public EfRolePermissionStore(TiendaFlowDbContext context) => _context = context;

    public IReadOnlySet<string>? StoredFor(int roleId)
    {
        var stored = _context.RolePermissionGrants
            .AsNoTracking()
            .Where(grant => grant.RoleId == roleId)
            .Select(grant => grant.Permission)
            .ToList();

        return stored.Count == 0 ? null : stored.ToHashSet(StringComparer.Ordinal);
    }

    public void Replace(int roleId, IReadOnlySet<string> permissions)
    {
        _context.RolePermissionGrants.RemoveRange(
            _context.RolePermissionGrants.Where(grant => grant.RoleId == roleId));
        _context.RolePermissionGrants.AddRange(
            permissions.Select(permission => new RolePermissionGrant(roleId, permission)));
        _context.SaveChanges();
    }

    public void Clear(int roleId)
    {
        _context.RolePermissionGrants.RemoveRange(
            _context.RolePermissionGrants.Where(grant => grant.RoleId == roleId));
        _context.SaveChanges();
    }
}
