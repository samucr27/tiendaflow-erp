using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

/// <summary>
/// Read models for the inventory count and transfer screens. The filter and the
/// order run on the entity query before it is materialized, so SQL Server keeps
/// doing the work; only the store and product names are joined afterwards.
/// </summary>
public sealed class EfInventoryDocumentQuery : IInventoryDocumentQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfInventoryDocumentQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<InventoryCountRow> ListCounts(int? storeId = null)
    {
        var stores = StoreNames();
        var query = _context.InventoryCounts
            .AsNoTracking()
            .Include(count => count.Lines)
            .AsQueryable();

        if (storeId is not null)
            query = query.Where(count => count.StoreId == storeId);

        var counts = query
            .OrderByDescending(count => count.CreatedAt)
            .ThenByDescending(count => count.Id)
            .ToList();

        return counts
            .Select(count => ToRow(count, stores))
            .ToList();
    }

    public InventoryCountDetail? GetCount(int id)
    {
        var count = _context.InventoryCounts
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefault(item => item.Id == id);

        if (count is null)
            return null;

        var stores = StoreNames();
        var products = ProductNames(count.Lines.Select(line => line.ProductSku));

        var lines = count.Lines
            .OrderBy(line => ProductName(products, line.ProductSku), StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.ProductSku, StringComparer.Ordinal)
            .Select(line => new InventoryCountLineRow(
                line.ProductSku,
                ProductName(products, line.ProductSku),
                line.SystemQuantity,
                line.CountedQuantity))
            .ToList();

        return new InventoryCountDetail(ToRow(count, stores), lines);
    }

    public IReadOnlyList<InventoryTransferRow> ListTransfers(int? storeId = null)
    {
        var stores = StoreNames();
        var query = _context.InventoryTransfers
            .AsNoTracking()
            .Include(transfer => transfer.Lines)
            .AsQueryable();

        if (storeId is not null)
        {
            query = query.Where(transfer =>
                transfer.OriginStoreId == storeId
                || transfer.DestinationStoreId == storeId);
        }

        var transfers = query
            .OrderByDescending(transfer => transfer.CreatedAt)
            .ThenByDescending(transfer => transfer.Id)
            .ToList();

        return transfers
            .Select(transfer => ToRow(transfer, stores))
            .ToList();
    }

    public InventoryTransferDetail? GetTransfer(int id)
    {
        var transfer = _context.InventoryTransfers
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefault(item => item.Id == id);

        if (transfer is null)
            return null;

        var stores = StoreNames();
        var products = ProductNames(transfer.Lines.Select(line => line.ProductSku));

        var lines = transfer.Lines
            .OrderBy(line => ProductName(products, line.ProductSku), StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.ProductSku, StringComparer.Ordinal)
            .Select(line => new InventoryTransferLineRow(
                line.ProductSku,
                ProductName(products, line.ProductSku),
                line.Quantity))
            .ToList();

        return new InventoryTransferDetail(ToRow(transfer, stores), lines);
    }

    private static InventoryCountRow ToRow(
        InventoryCount count,
        IReadOnlyDictionary<int, string> stores) =>
        new(
            count.Id,
            count.StoreId,
            stores.TryGetValue(count.StoreId, out var name) ? name : $"Tienda {count.StoreId}",
            count.Location,
            count.Status,
            count.Lines.Count,
            count.Lines.Count(line => line.CountedQuantity is not null),
            count.Lines.Sum(line => line.Difference ?? 0),
            count.CreatedAt,
            count.ClosedAt,
            count.AdjustedAt);

    private static InventoryTransferRow ToRow(
        InventoryTransfer transfer,
        IReadOnlyDictionary<int, string> stores) =>
        new(
            transfer.Id,
            transfer.OriginStoreId,
            stores.TryGetValue(transfer.OriginStoreId, out var origin) ? origin : $"Tienda {transfer.OriginStoreId}",
            transfer.DestinationStoreId,
            stores.TryGetValue(transfer.DestinationStoreId, out var destination) ? destination : $"Tienda {transfer.DestinationStoreId}",
            transfer.Status,
            transfer.Lines.Count,
            transfer.TotalUnits,
            transfer.CreatedAt,
            transfer.SentAt,
            transfer.ReceivedAt,
            transfer.CancelledAt);

    private Dictionary<int, string> StoreNames() =>
        _context.Stores
            .AsNoTracking()
            .ToDictionary(store => store.Id, store => store.Name);

    private Dictionary<string, string> ProductNames(IEnumerable<string> skus)
    {
        var list = skus.Distinct(StringComparer.Ordinal).ToList();

        return _context.Products
            .AsNoTracking()
            .Where(product => list.Contains(product.Sku))
            .ToDictionary(product => product.Sku, product => product.Name);
    }

    private static string ProductName(
        IReadOnlyDictionary<string, string> products,
        string sku) =>
        products.TryGetValue(sku, out var name) ? name : sku;
}
