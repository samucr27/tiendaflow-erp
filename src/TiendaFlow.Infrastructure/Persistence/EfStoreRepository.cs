using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfStoreRepository : IStoreRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfStoreRepository(TiendaFlowDbContext context) => _context = context;

    public Store? GetById(int id) => _context.Stores.Find(id);

    public IReadOnlyList<Store> List() =>
        _context.Stores.AsNoTracking().OrderBy(store => store.Name).ToList();

    public void Add(Store store)
    {
        _context.Stores.Add(store);
        _context.SaveChanges();
    }

    public void Update(Store store)
    {
        if (_context.Entry(store).State == EntityState.Detached)
            _context.Stores.Update(store);

        _context.SaveChanges();
    }
}

public sealed class EfRoleRepository : IRoleRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfRoleRepository(TiendaFlowDbContext context) => _context = context;

    public Role? GetById(int id) => _context.Roles.Find(id);

    public IReadOnlyList<Role> List() =>
        _context.Roles.AsNoTracking().OrderBy(role => role.Id).ToList();

    public void Add(Role role)
    {
        _context.Roles.Add(role);
        _context.SaveChanges();
    }
}
