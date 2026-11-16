using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfSupplierQuery : ISupplierQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfSupplierQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<SupplierRow> List() => Read(orderByName: true).ToList();

    public SupplierRow? GetById(int id) => Read(id: id).FirstOrDefault();

    private IQueryable<SupplierRow> Read(int? id = null, bool orderByName = false)
    {
        var suppliers = _context.Suppliers.AsNoTracking();

        if (id is int supplierId)
            suppliers = suppliers.Where(supplier => supplier.Id == supplierId);

        if (orderByName)
            suppliers = suppliers.OrderBy(supplier => supplier.BusinessName);

        var orders = _context.PurchaseOrders.AsNoTracking();
        var lines = _context.PurchaseOrderLines.AsNoTracking();

        return
            from supplier in suppliers
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
                        && order.Status != PurchaseOrderStatus.Cancelled)
                    .Select(order => (decimal?)order.Total)
                    .Sum() ?? 0m,
                orders.Count(order => order.SupplierId == supplier.Id
                    && order.Status == PurchaseOrderStatus.Received),
                // Only fully received orders with a promised date can be
                // measured; the rest stay out of the punctuality denominator.
                orders.Count(order => order.SupplierId == supplier.Id
                    && order.Status == PurchaseOrderStatus.Received
                    && order.PromisedDate != null),
                orders.Count(order => order.SupplierId == supplier.Id
                    && order.Status == PurchaseOrderStatus.Received
                    && order.PromisedDate != null
                    && order.ReceivedDate != null
                    && order.ReceivedDate <= order.PromisedDate),
                lines
                    .Where(line => line.ReceivedQuantity > 0
                        && orders.Any(order => order.Id == line.OrderId
                            && order.SupplierId == supplier.Id
                            && order.Status != PurchaseOrderStatus.Cancelled))
                    .Select(line => (int?)line.Quantity)
                    .Sum() ?? 0,
                lines
                    .Where(line => line.ReceivedQuantity > 0
                        && orders.Any(order => order.Id == line.OrderId
                            && order.SupplierId == supplier.Id
                            && order.Status != PurchaseOrderStatus.Cancelled))
                    .Select(line => (int?)line.ReceivedQuantity)
                    .Sum() ?? 0);
    }
}
