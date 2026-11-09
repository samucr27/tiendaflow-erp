using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfSupplierQuery : ISupplierQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfSupplierQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<SupplierRow> List() =>
        Read().OrderBy(row => row.BusinessName).ToList();

    public SupplierRow? GetById(int id) =>
        Read().FirstOrDefault(row => row.Id == id);

    private IQueryable<SupplierRow> Read()
    {
        var orders = _context.PurchaseOrders.AsNoTracking();

        return
            from supplier in _context.Suppliers.AsNoTracking()
            select new SupplierRow(
                supplier.Id,
                supplier.Nit,
                supplier.BusinessName,
                supplier.Address,
                supplier.Rating,
                supplier.Active,
                orders.Count(order => order.SupplierId == supplier.Id),
                orders
                    .Where(order => order.SupplierId == supplier.Id
                        && order.Status != Domain.PurchaseOrderStatus.Cancelled)
                    .Select(order => (decimal?)order.Total)
                    .Sum() ?? 0m);
    }
}
