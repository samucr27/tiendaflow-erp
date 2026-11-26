namespace TiendaFlow.Domain;

public enum QuotationStatus
{
    Pending,
    Approved,
    Rejected,
    Ordered
}

/// <summary>
/// A request for a supplier's price on one product. The buyer records the
/// quoted unit price, a store manager approves or rejects it, and an approved
/// quotation becomes a purchase order. Only a pending quotation can change or
/// be deleted; once approved it is the basis of an order.
/// </summary>
public class QuotationRequest
{
    public QuotationRequest(
        int supplierId,
        string productSku,
        int quantity,
        decimal unitPrice,
        DateTime requestedAt,
        DateOnly? validUntil = null,
        string? notes = null)
    {
        ProductSku = "";
        Apply(supplierId, productSku, quantity, unitPrice, validUntil, notes);
        RequestedAt = UtcTimestamp.Normalize(requestedAt);
        Status = QuotationStatus.Pending;
    }

    private QuotationRequest()
    {
        ProductSku = "";
    }

    public int Id { get; private set; }

    public int SupplierId { get; private set; }

    public string ProductSku { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal EstimatedAmount => Quantity * UnitPrice;

    public DateTime RequestedAt { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    public string? Notes { get; private set; }

    public QuotationStatus Status { get; private set; }

    /// <summary>User account (linked to an employee) that approved or rejected it.</summary>
    public int? DecidedByUserId { get; private set; }

    public string? DecidedByName { get; private set; }

    public DateTime? DecidedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public int? PurchaseOrderId { get; private set; }

    public bool CanChange => Status == QuotationStatus.Pending;

    public bool CanDelete => Status is QuotationStatus.Pending or QuotationStatus.Rejected;

    public void Update(int supplierId, string productSku, int quantity, decimal unitPrice,
        DateOnly? validUntil, string? notes)
    {
        RequireStatus(QuotationStatus.Pending, "Only a pending quotation can be edited.");
        Apply(supplierId, productSku, quantity, unitPrice, validUntil, notes);
    }

    public void Approve(int? userId, string approverName, DateTime at)
    {
        RequireStatus(QuotationStatus.Pending, "Only a pending quotation can be approved.");
        Decide(userId, approverName, at);
        Status = QuotationStatus.Approved;
    }

    public void Reject(int? userId, string approverName, string reason, DateTime at)
    {
        RequireStatus(QuotationStatus.Pending, "Only a pending quotation can be rejected.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidQuotationException("The rejection reason is required.");

        Decide(userId, approverName, at);
        RejectionReason = reason.Trim();
        Status = QuotationStatus.Rejected;
    }

    public void MarkOrdered(int purchaseOrderId)
    {
        RequireStatus(QuotationStatus.Approved, "Only an approved quotation becomes a purchase order.");
        if (purchaseOrderId <= 0)
            throw new InvalidQuotationException("The purchase order is required.");

        PurchaseOrderId = purchaseOrderId;
        Status = QuotationStatus.Ordered;
    }

    public void EnsureCanDelete()
    {
        if (!CanDelete)
            throw new InvalidQuotationException("Only a pending or rejected quotation can be deleted.");
    }

    private void Apply(int supplierId, string productSku, int quantity, decimal unitPrice,
        DateOnly? validUntil, string? notes)
    {
        if (supplierId <= 0)
            throw new InvalidQuotationException("A supplier is required.");
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidQuotationException("A product is required.");
        if (quantity <= 0)
            throw new InvalidQuotationException("The quantity must be greater than zero.");
        if (unitPrice <= 0)
            throw new InvalidQuotationException("The quoted unit price must be greater than zero.");

        SupplierId = supplierId;
        ProductSku = productSku.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        ValidUntil = validUntil;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private void Decide(int? userId, string approverName, DateTime at)
    {
        DecidedByUserId = userId;
        DecidedByName = string.IsNullOrWhiteSpace(approverName) ? null : approverName.Trim();
        DecidedAt = UtcTimestamp.Normalize(at);
    }

    private void RequireStatus(QuotationStatus expected, string message)
    {
        if (Status != expected)
            throw new InvalidQuotationException(message);
    }
}

public class InvalidQuotationException : Exception
{
    public InvalidQuotationException(string message) : base(message) { }
}

public class QuotationNotFoundException : Exception
{
    public QuotationNotFoundException(int id) : base($"Quotation {id} was not found.") { }
}
