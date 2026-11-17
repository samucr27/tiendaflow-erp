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

    /// <summary>Single-product sale; kept for the API and the tests.</summary>
    public PosSaleReceipt Register(int storeId, string sku, int quantity)
    {
        var cart = RegisterCart(storeId, new[] { new PosCartLine(sku, quantity) });
        var line = cart.Lines[0];

        return new PosSaleReceipt(
            cart.SaleId,
            cart.Code,
            line.ProductSku,
            line.ProductName,
            line.Quantity,
            line.UnitPrice,
            cart.Total,
            cart.SoldAt,
            line.RemainingStock,
            line.MinStock);
    }

    /// <summary>
    /// Sells several products in one document. Every line takes its units out
    /// of the store stock in the same transaction; if one line fails, nothing
    /// is saved.
    /// </summary>
    public PosCartReceipt RegisterCart(int storeId, IReadOnlyList<PosCartLine> lines)
    {
        _accessControl.Demand(Permissions.SalesPosUse);

        if (storeId <= 0)
            throw new InvalidPosSaleException("The cashier has no store assigned.");

        var requested = lines
            .Where(line => line.Quantity != 0 || !string.IsNullOrWhiteSpace(line.Sku))
            .GroupBy(line => (line.Sku ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new PosCartLine(group.Key, group.Sum(line => line.Quantity)))
            .ToList();

        if (requested.Count == 0)
            throw new InvalidPosSaleException("Add at least one product to the sale.");

        var products = requested
            .Select(line => _products.GetBySku(line.Sku) ?? throw new ProductNotFoundException(line.Sku))
            .ToList();

        var soldAt = _clock.UtcNow;
        var sale = new PosSale(SaleCode.Create(soldAt), storeId, _currentUser.UserId, soldAt);
        var receiptLines = new List<PosReceiptLine>();

        _unitOfWork.ExecuteInTransaction(() =>
        {
            for (var index = 0; index < requested.Count; index++)
            {
                var product = products[index];
                var quantity = requested[index].Quantity;

                var item = _stock.Get(storeId, product.Sku)
                    ?? throw new StockNotFoundException(storeId, product.Sku);

                item.RegisterSale(quantity, soldAt);
                _stock.Update(item);

                sale.AddLine(product.Sku, product.Name, quantity, product.SalePrice);
                receiptLines.Add(new PosReceiptLine(
                    product.Sku, product.Name, quantity, product.SalePrice, item.QuantityAvailable, item.MinStock));
            }

            _sales.Add(sale);

            _auditLog.Record(new AuditEntry(
                "PosSaleRegistered",
                sale.Id.ToString(),
                $"Venta {sale.Code}: {receiptLines.Count} productos, {receiptLines.Sum(line => line.Quantity)} unidades, total {sale.Total:C0}.",
                _currentUser.UserId,
                soldAt,
                module: "sales",
                ip: _currentUser.Ip));
        });

        return new PosCartReceipt(sale.Id, sale.Code, soldAt, sale.Total, receiptLines);
    }
}

public record PosCartLine(string Sku, int Quantity);

public record PosReceiptLine(
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    int RemainingStock,
    int MinStock)
{
    public decimal LineTotal => Quantity * UnitPrice;

    public bool LeftStockCritical => RemainingStock <= MinStock;
}

public record PosCartReceipt(
    int SaleId,
    string Code,
    DateTime SoldAt,
    decimal Total,
    IReadOnlyList<PosReceiptLine> Lines)
{
    public IReadOnlyList<PosReceiptLine> CriticalLines => Lines.Where(line => line.LeftStockCritical).ToList();
}
