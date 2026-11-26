using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record QuotationInput(
    int SupplierId,
    string ProductSku,
    int Quantity,
    decimal UnitPrice,
    DateOnly? ValidUntil,
    string? Notes);

public record QuotationRow(
    int Id,
    string Code,
    int SupplierId,
    string SupplierName,
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    DateTime RequestedAt,
    DateOnly? ValidUntil,
    string? Notes,
    QuotationStatus Status,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? RejectionReason,
    int? PurchaseOrderId,
    bool CanChange,
    bool CanDelete);

/// <summary>
/// Requests for quotation (CRUD) and their approval. The buyer (create
/// permission) records and edits pending requests; the store manager (approve
/// permission) approves or rejects them; an approved quotation becomes a
/// purchase order draft through <see cref="PurchaseOrderService"/>, in the
/// same transaction that marks the quotation as ordered.
/// </summary>
public sealed class QuotationService
{
    private readonly IEntityStore<QuotationRequest> _quotations;
    private readonly ISupplierRepository _suppliers;
    private readonly IProductCatalogQuery _products;
    private readonly PurchaseOrderService _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public QuotationService(
        IEntityStore<QuotationRequest> quotations,
        ISupplierRepository suppliers,
        IProductCatalogQuery products,
        PurchaseOrderService orders,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _quotations = quotations;
        _suppliers = suppliers;
        _products = products;
        _orders = orders;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public static string CodeOf(int id) => $"COT-{id:00000}";

    public IReadOnlyList<QuotationRow> List()
    {
        _accessControl.Demand(Permissions.PurchasingOrdersView);
        var suppliers = _suppliers.List().ToDictionary(supplier => supplier.Id, supplier => supplier.BusinessName);

        return _quotations.All()
            .OrderBy(item => item.Status switch
            {
                QuotationStatus.Pending => 0,
                QuotationStatus.Approved => 1,
                _ => 2
            })
            .ThenByDescending(item => item.RequestedAt)
            .Select(item => ToRow(item, suppliers))
            .ToList();
    }

    public QuotationRow Get(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersView);
        var item = Find(id);
        var suppliers = _suppliers.List().ToDictionary(supplier => supplier.Id, supplier => supplier.BusinessName);
        return ToRow(item, suppliers);
    }

    public int Create(QuotationInput input)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);
        Validate(input);

        var item = new QuotationRequest(input.SupplierId, input.ProductSku, input.Quantity,
            input.UnitPrice, _clock.UtcNow, input.ValidUntil, input.Notes);
        _quotations.Add(item);
        Audit("QuotationCreated", item.Id,
            $"{CodeOf(item.Id)}: {item.Quantity} u. de {item.ProductSku} a {item.UnitPrice:0} c/u");
        return item.Id;
    }

    public void Update(int id, QuotationInput input)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);
        Validate(input);

        var item = Find(id);
        item.Update(input.SupplierId, input.ProductSku, input.Quantity, input.UnitPrice,
            input.ValidUntil, input.Notes);
        _quotations.Update(item);
        Audit("QuotationUpdated", item.Id, $"{CodeOf(item.Id)} actualizada");
    }

    public void Delete(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        var item = Find(id);
        item.EnsureCanDelete();
        _quotations.Remove(item);
        Audit("QuotationDeleted", id, $"{CodeOf(id)} eliminada");
    }

    public void Approve(int id)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersApprove);

        var item = Find(id);
        item.Approve(_currentUser.UserId, _currentUser.Name, _clock.UtcNow);
        _quotations.Update(item);
        Audit("QuotationApproved", item.Id, $"{CodeOf(item.Id)} aprobada");
    }

    public void Reject(int id, string reason)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersApprove);

        var item = Find(id);
        item.Reject(_currentUser.UserId, _currentUser.Name, reason, _clock.UtcNow);
        _quotations.Update(item);
        Audit("QuotationRejected", item.Id, $"{CodeOf(item.Id)} rechazada");
    }

    /// <summary>
    /// Turns an approved quotation into a purchase order draft for the given
    /// store, with one line at the quoted price.
    /// </summary>
    public int GenerateOrder(int id, int storeId)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        if (storeId <= 0)
            throw new InvalidQuotationException("A store is required to create the order.");

        var orderId = 0;
        _unitOfWork.ExecuteInTransaction(() =>
        {
            var item = Find(id);
            if (item.Status != QuotationStatus.Approved)
                throw new InvalidQuotationException("Only an approved quotation becomes a purchase order.");

            orderId = _orders.Create(new CreatePurchaseOrderRequest(
                item.SupplierId,
                storeId,
                _clock.UtcNow,
                [new CreatePurchaseOrderLine(item.ProductSku, item.Quantity, item.UnitPrice)]));

            item.MarkOrdered(orderId);
            _quotations.Update(item);
            Audit("QuotationOrdered", item.Id, $"{CodeOf(item.Id)} generó la orden {orderId}");
        });

        return orderId;
    }

    private void Validate(QuotationInput input)
    {
        var supplier = _suppliers.GetById(input.SupplierId)
            ?? throw new SupplierNotFoundException(input.SupplierId);
        if (!supplier.Active)
            throw new InvalidQuotationException("The supplier is inactive.");
        if (string.IsNullOrWhiteSpace(input.ProductSku) || _products.GetBySku(input.ProductSku.Trim()) is null)
            throw new InvalidQuotationException("A product is required.");
    }

    private QuotationRequest Find(int id) => _quotations.Get(id) ?? throw new QuotationNotFoundException(id);

    private QuotationRow ToRow(QuotationRequest item, IReadOnlyDictionary<int, string> suppliers) =>
        new(item.Id,
            CodeOf(item.Id),
            item.SupplierId,
            suppliers.GetValueOrDefault(item.SupplierId, $"Proveedor {item.SupplierId}"),
            item.ProductSku,
            _products.GetBySku(item.ProductSku)?.Name ?? item.ProductSku,
            item.Quantity,
            item.UnitPrice,
            item.EstimatedAmount,
            item.RequestedAt,
            item.ValidUntil,
            item.Notes,
            item.Status,
            item.DecidedByName,
            item.DecidedAt,
            item.RejectionReason,
            item.PurchaseOrderId,
            item.CanChange,
            item.CanDelete);

    private void Audit(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(action, entityId.ToString(), detail, _currentUser.UserId,
            _clock.UtcNow, module: "purchasing", ip: _currentUser.Ip));
}
