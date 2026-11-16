using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfPosSaleRepository : IPosSaleRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfPosSaleRepository(TiendaFlowDbContext context) => _context = context;

    public void Add(PosSale sale)
    {
        _context.PosSales.Add(sale);
        _context.SaveChanges();
    }
}

public sealed class EfPosSaleQuery : IPosSaleQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfPosSaleQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<PosSaleRow> List(DateTime fromUtc, DateTime toUtcExclusive) =>
        (from sale in _context.PosSales.AsNoTracking()
         join store in _context.Stores.AsNoTracking() on sale.StoreId equals store.Id
         join user in _context.AppUsers.AsNoTracking() on sale.CashierUserId equals (int?)user.Id into users
         from user in users.DefaultIfEmpty()
         join employee in _context.Employees.AsNoTracking() on user.EmployeeId equals (int?)employee.Id into employees
         from employee in employees.DefaultIfEmpty()
         where sale.SoldAt >= fromUtc && sale.SoldAt < toUtcExclusive
         orderby sale.SoldAt descending, sale.Id descending
         select new PosSaleRow(
             sale.Id,
             sale.Code,
             sale.StoreId,
             store.Name,
             employee != null ? employee.Name : "Sin cajero",
             sale.SoldAt,
             sale.Lines.Sum(line => line.Quantity),
             sale.Total))
        .ToList();
}
