using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface IPosSaleRepository
{
    void Add(PosSale sale);
}

public interface IPosSaleQuery
{
    /// <summary>POS sales whose UTC timestamp falls in [fromUtc, toUtcExclusive).</summary>
    IReadOnlyList<PosSaleRow> List(DateTime fromUtc, DateTime toUtcExclusive);
}

public record PosSaleRow(
    int Id,
    string Code,
    int StoreId,
    string StoreName,
    string CashierName,
    DateTime SoldAt,
    int Units,
    decimal Total);

/// <summary>
/// What the POS screen shows after a sale. RemainingStock and MinStock let it
/// raise the HU-05 critical stock alert on the receipt itself.
/// </summary>
public record PosSaleReceipt(
    int SaleId,
    string Code,
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    DateTime SoldAt,
    int RemainingStock,
    int MinStock)
{
    public bool LeftStockCritical => RemainingStock <= MinStock;
}

/// <summary>
/// Registers a point-of-sale sale: it takes the units out of the store stock
/// and saves the sale document in one transaction, so the dashboard, the sales
/// report and the replenishment consumption count every unit the cashier sold.
/// The price comes from the catalog, never from the screen.
/// </summary>
public sealed class PosSaleService
{
    private readonly IStockRepository _stock;
    private readonly IProductRepository _products;
    private readonly IPosSaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public PosSaleService(
        IStockRepository stock,
        IProductRepository products,
        IPosSaleRepository sales,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _stock = stock;
        _products = products;
        _sales = sales;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public PosSaleReceipt Register(int storeId, string sku, int quantity)
    {
        _accessControl.Demand(Permissions.SalesPosUse);

        if (storeId <= 0)
            throw new InvalidPosSaleException("The cashier has no store assigned.");

        var product = _products.GetBySku((sku ?? string.Empty).Trim())
            ?? throw new ProductNotFoundException(sku ?? string.Empty);

        var soldAt = _clock.UtcNow;
        PosSale? sale = null;
        StockItem? item = null;

        _unitOfWork.ExecuteInTransaction(() =>
        {
            item = _stock.Get(storeId, product.Sku)
                ?? throw new StockNotFoundException(storeId, product.Sku);

            item.RegisterSale(quantity, soldAt);
            _stock.Update(item);

            sale = new PosSale(SaleCode.Create(soldAt), storeId, _currentUser.UserId, soldAt);
            sale.AddLine(product.Sku, product.Name, quantity, product.SalePrice);
            _sales.Add(sale);

            _auditLog.Record(new AuditEntry(
                "PosSaleRegistered",
                sale.Id.ToString(),
                $"Venta {sale.Code}: {quantity} x {product.Sku}; quedan {item.QuantityAvailable} (mínimo {item.MinStock}).",
                _currentUser.UserId,
                soldAt,
                module: "sales",
                ip: _currentUser.Ip));
        });

        return new PosSaleReceipt(
            sale!.Id,
            sale.Code,
            product.Sku,
            product.Name,
            quantity,
            product.SalePrice,
            sale.Total,
            soldAt,
            item!.QuantityAvailable,
            item.MinStock);
    }
}
