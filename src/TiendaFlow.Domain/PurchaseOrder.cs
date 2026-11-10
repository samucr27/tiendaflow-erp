namespace TiendaFlow.Domain;

/// <summary>
/// A purchase order follows the same flow the supplier module describes:
/// draft, optional approval, sent to the supplier, confirmed by the supplier
/// and received (totally or partially). A cancelled order keeps its lines for
/// the history but can no longer change.
/// </summary>
public class PurchaseOrder
{
    public PurchaseOrder(int id, int supplierId, int storeId, DateTime orderDate)
    {
        if (supplierId <= 0)
            throw new InvalidPurchaseOrderException("A supplier is required.");
        if (storeId <= 0)
            throw new InvalidPurchaseOrderException("A store is required.");

        Id = id;
        SupplierId = supplierId;
        StoreId = storeId;
        OrderDate = UtcTimestamp.Normalize(orderDate);
        Status = PurchaseOrderStatus.Draft;
        Total = 0m;
    }

    public int Id { get; private set; }

    public int SupplierId { get; private set; }

    public int StoreId { get; private set; }

    public DateTime OrderDate { get; private set; }

    public PurchaseOrderStatus Status { get; private set; }

    public decimal Total { get; private set; }

    public List<PurchaseOrderLine> Lines { get; } = new();

    public void AddLine(string productSku, int quantity, decimal unitCost)
    {
        RequireStatus(PurchaseOrderStatus.Draft, "add lines");

        if (Lines.Any(line => line.ProductSku == productSku.Trim()))
            throw new InvalidPurchaseOrderException($"{productSku} is already part of the order.");

        Lines.Add(new PurchaseOrderLine(Id, productSku, quantity, unitCost));
        RecalculateTotal();
    }

    public void SubmitForApproval() =>
        MoveTo(PurchaseOrderStatus.Draft, PurchaseOrderStatus.PendingApproval, "submit for approval");

    /// <summary>
    /// The store manager may approve a draft directly, which is the usual path
    /// for orders created from low-stock suggestions.
    /// </summary>
    public void Approve()
    {
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.PendingApproval))
            throw InvalidTransition("approve");

        Status = PurchaseOrderStatus.Approved;
    }

    public void Send() =>
        MoveTo(PurchaseOrderStatus.Approved, PurchaseOrderStatus.Sent, "send to the supplier");

    public void Confirm() =>
        MoveTo(PurchaseOrderStatus.Sent, PurchaseOrderStatus.Confirmed, "confirm");

    public void ReceiveLine(string productSku, int quantity)
    {
        if (Status is not (PurchaseOrderStatus.Sent
            or PurchaseOrderStatus.Confirmed
            or PurchaseOrderStatus.PartiallyReceived))
        {
            throw InvalidTransition("receive");
        }

        var line = Lines.FirstOrDefault(item => item.ProductSku == productSku)
            ?? throw new InvalidPurchaseOrderException($"{productSku} is not part of the order.");

        line.Receive(quantity);

        Status = Lines.All(item => item.PendingQuantity == 0)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
    }

    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
            throw InvalidTransition("cancel");
        if (Lines.Any(line => line.ReceivedQuantity > 0))
            throw new InvalidPurchaseOrderException("An order with received units cannot be cancelled.");

        Status = PurchaseOrderStatus.Cancelled;
    }

    private void MoveTo(
        PurchaseOrderStatus from,
        PurchaseOrderStatus to,
        string action)
    {
        if (Status != from)
            throw InvalidTransition(action);

        Status = to;
    }

    private void RequireStatus(PurchaseOrderStatus required, string action)
    {
        if (Status != required)
            throw InvalidTransition(action);
    }

    private InvalidPurchaseOrderException InvalidTransition(string action) =>
        new($"Cannot {action} an order in status {Status}.");

    private void RecalculateTotal() =>
        Total = Lines.Sum(line => line.LineTotal);
}
