using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public class EfAuditLog : IAuditLog
{
    private readonly TiendaFlowDbContext _context;

    public EfAuditLog(TiendaFlowDbContext context) => _context = context;

    public void Record(AuditEntry entry)
    {
        _context.AuditEntries.Add(entry);
        _context.SaveChanges();
    }
}
