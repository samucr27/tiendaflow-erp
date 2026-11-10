using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfPurchaseOrderQuery : IPurchaseOrderQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfPurchaseOrderQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<PurchaseOrderRow> List() =>
        Read().OrderByDescending(row => row.OrderDate)
            .ThenByDescending(row => row.Id)
            .ToList();

    public IReadOnlyList<PurchaseOrderRow> ListBySupplier(int supplierId) =>
        Read().Where(row => row.SupplierId == supplierId)
            .OrderByDescending(row => row.OrderDate)
            .ThenByDescending(row => row.Id)
            .ToList();

    public PurchaseOrderDetail? GetById(int id)
    {
        var header = Read().FirstOrDefault(row => row.Id == id);
        if (header is null)
            return null;

        var lines =
            from line in _context.PurchaseOrderLines.AsNoTracking()
            join product in _context.Products.AsNoTracking()
                on line.ProductSku equals product.Sku into products
            from product in products.DefaultIfEmpty()
            where line.OrderId == id
            select new PurchaseOrderLineRow(
                line.ProductSku,
                product != null ? product.Name : line.ProductSku,
                line.Quantity,
                line.UnitCost,
                line.ReceivedQuantity);

        return new PurchaseOrderDetail(header, lines.OrderBy(line => line.ProductName).ToList());
    }

    private IQueryable<PurchaseOrderRow> Read() =>
        from order in _context.PurchaseOrders.AsNoTracking()
        join supplier in _context.Suppliers.AsNoTracking()
            on order.SupplierId equals supplier.Id into suppliers
        from supplier in suppliers.DefaultIfEmpty()
        join store in _context.Stores.AsNoTracking()
            on order.StoreId equals store.Id into stores
        from store in stores.DefaultIfEmpty()
        select new PurchaseOrderRow(
            order.Id,
            order.SupplierId,
            supplier != null ? supplier.BusinessName : $"Proveedor {order.SupplierId}",
            order.StoreId,
            store != null ? store.Name : $"Tienda {order.StoreId}",
            order.OrderDate,
            order.Status,
            order.Total,
            order.Lines.Count);
}
