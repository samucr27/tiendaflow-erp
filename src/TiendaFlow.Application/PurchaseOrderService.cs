using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record CreatePurchaseOrderLine(string ProductSku, int Quantity, decimal UnitCost);

public record CreatePurchaseOrderRequest(
    int SupplierId,
    int StoreId,
    DateTime OrderDate,
    IReadOnlyList<CreatePurchaseOrderLine> Lines);

public record ReceivePurchaseOrderLine(string ProductSku, int Quantity);

/// <summary>
/// Purchase orders and reorder suggestions. Receipts go through StockService,
/// so the units entering the store follow the same inventory rules as any
/// other receipt, inside one transaction with the order state and the audit.
/// </summary>
public sealed class PurchaseOrderService
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly ISupplierRepository _suppliers;
    private readonly IStockQuery _stock;
    private readonly StockService _stockService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public PurchaseOrderService(
        IPurchaseOrderRepository orders,
        ISupplierRepository suppliers,
        IStockQuery stock,
        StockService stockService,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _orders = orders;
        _suppliers = suppliers;
        _stock = stock;
        _stockService = stockService;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Low-stock rows become a buying proposal. There is no supplier-product
    /// relation in the model, so the buyer picks the supplier on the order.
    /// </summary>
    public IReadOnlyList<ReorderSuggestion> SuggestReorder(int storeId)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        return _stock.ListByStore(storeId)
            .Where(row => row.IsBelowMinimum)
            .OrderBy(row => row.QuantityAvailable)
            .ThenBy(row => row.ProductName)
            .Select(row => new ReorderSuggestion(
                row.Sku,
                row.ProductName,
                row.QuantityAvailable,
                row.MinStock,
                Math.Max(row.MinStock * 2 - row.QuantityAvailable, 1),
                row.SalePrice))
            .ToList();
    }

    public int Create(CreatePurchaseOrderRequest request)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        var supplier = _suppliers.GetById(request.SupplierId)
            ?? throw new SupplierNotFoundException(request.SupplierId);

        if (!supplier.Active)
            throw new InvalidPurchaseOrderException("The supplier is inactive.");

        if (request.Lines.Count == 0)
            throw new InvalidPurchaseOrderException("The order needs at least one line.");

        var order = new PurchaseOrder(0, supplier.Id, request.StoreId, request.OrderDate);

        foreach (var line in request.Lines)
            order.AddLine(line.ProductSku, line.Quantity, line.UnitCost);

        _orders.Add(order);
        Record("PurchaseOrderCreated", order.Id, $"Orden creada con {order.Lines.Count} líneas y total {order.Total:0}.");

        return order.Id;
    }

    public void SubmitForApproval(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        Transition(id, order => order.SubmitForApproval(), "PurchaseOrderSubmitted", "enviada a aprobación");
    }

    public void Approve(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersApprove);

        Transition(id, order => order.Approve(), "PurchaseOrderApproved", "aprobada");
    }

    public void Send(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersSend);

        Transition(id, order => order.Send(), "PurchaseOrderSent", "enviada al proveedor");
    }

    /// <summary>
    /// Records the supplier confirmation. It uses the receiving permission
    /// because the person who tracks the delivery is the one who knows that
    /// the supplier accepted the order.
    /// </summary>
    public void Confirm(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersReceive);

        Transition(id, order => order.Confirm(), "PurchaseOrderConfirmed", "confirmada por el proveedor");
    }

    public void Cancel(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCancel);

        Transition(id, order => order.Cancel(), "PurchaseOrderCancelled", "cancelada");
    }

    /// <summary>
    /// Receives the units of one or more lines. Each line increases its stock
    /// row through StockService and the whole delivery shares one transaction
    /// with the order status and the audit entry.
    /// </summary>
    public void Receive(
        int id,
        IReadOnlyList<ReceivePurchaseOrderLine> lines,
        DateTime date)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersReceive);

        if (lines.Count == 0)
            throw new InvalidPurchaseOrderException("Select at least one line to receive.");

        var order = FindOrThrow(id);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            foreach (var line in lines)
            {
                order.ReceiveLine(line.ProductSku, line.Quantity);
                _stockService.ReceiveStock(
                    order.StoreId,
                    line.ProductSku,
                    line.Quantity,
                    location: "Recepción",
                    minStock: 0,
                    date);
            }

            _orders.Update(order);

            var units = lines.Sum(line => line.Quantity);
            Record(
                "PurchaseOrderReceived",
                order.Id,
                $"{units} unidades recibidas; estado {order.Status}.");
        });
    }

    private void Transition(
        int id,
        Action<PurchaseOrder> change,
        string action,
        string description)
    {
        var order = FindOrThrow(id);

        change(order);
        _orders.Update(order);
        Record(action, order.Id, $"Orden {description}.");
    }

    private PurchaseOrder FindOrThrow(int id) =>
        _orders.GetById(id) ?? throw new PurchaseOrderNotFoundException(id);

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "purchasing",
            ip: _currentUser.Ip));
}
