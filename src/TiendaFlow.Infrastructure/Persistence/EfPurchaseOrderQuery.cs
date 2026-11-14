using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfPurchaseOrderQuery : IPurchaseOrderQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfPurchaseOrderQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<PurchaseOrderRow> List() =>
        Read(orderDescending: true).ToList();

    public IReadOnlyList<PurchaseOrderRow> ListBySupplier(int supplierId) =>
        Read(supplierId: supplierId, orderDescending: true).ToList();

    public PurchaseOrderDetail? GetById(int id)
    {
        var header = Read(id: id).FirstOrDefault();
        if (header is null)
            return null;

        var lines =
            from line in _context.PurchaseOrderLines.AsNoTracking()
            join product in _context.Products.AsNoTracking()
                on line.ProductSku equals product.Sku into products
            from product in products.DefaultIfEmpty()
            where line.OrderId == id
            orderby product != null ? product.Name : line.ProductSku
            select new PurchaseOrderLineRow(
                line.ProductSku,
                product != null ? product.Name : line.ProductSku,
                line.Quantity,
                line.UnitCost,
                line.ReceivedQuantity);

        return new PurchaseOrderDetail(header, lines.ToList());
    }

    private IQueryable<PurchaseOrderRow> Read(
        int? id = null,
        int? supplierId = null,
        bool orderDescending = false)
    {
        var orders = _context.PurchaseOrders.AsNoTracking();

        // Filter and order on the entity before projecting to the record: EF
        // cannot translate OrderBy/Where over a constructor projection.
        if (id is int orderId)
            orders = orders.Where(order => order.Id == orderId);

        if (supplierId is int supplierFilter)
            orders = orders.Where(order => order.SupplierId == supplierFilter);

        if (orderDescending)
            orders = orders.OrderByDescending(order => order.OrderDate)
                .ThenByDescending(order => order.Id);

        return
            from order in orders
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
                order.PromisedDate,
                order.ReceivedDate,
                order.Status,
                order.Total,
                order.Lines.Count);
    }
}
