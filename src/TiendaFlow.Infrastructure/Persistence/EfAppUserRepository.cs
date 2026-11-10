using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfAppUserRepository : IAppUserRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfAppUserRepository(TiendaFlowDbContext context) => _context = context;

    public AppUser? GetById(int id) => _context.AppUsers.Find(id);

    public AppUser? GetByEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _context.AppUsers.FirstOrDefault(user => user.Email == normalized);
    }

    public IReadOnlyList<AppUser> List() =>
        _context.AppUsers.AsNoTracking().OrderBy(user => user.Email).ToList();

    public bool ExistsEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _context.AppUsers.Any(user => user.Email == normalized);
    }

    public void Add(AppUser user)
    {
        _context.AppUsers.Add(user);
        _context.SaveChanges();
    }

    public void Update(AppUser user)
    {
        if (_context.Entry(user).State == EntityState.Detached)
            _context.AppUsers.Update(user);

        _context.SaveChanges();
    }
}
