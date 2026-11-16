using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Builds replenishment proposals from the real stock level and the units sold
/// inside the configured window. Draft orders are grouped by the default
/// supplier of each product and always stay in Draft: the buyer still reviews
/// and sends them. The whole generation shares one transaction.
/// </summary>
public sealed class ReplenishmentService
{
    private readonly IStockQuery _stock;
    private readonly IConsumptionQuery _consumption;
    private readonly IProductSupplierRepository _relations;
    private readonly ISupplierRepository _suppliers;
    private readonly IPurchaseOrderRepository _orders;
    private readonly IParameterValues _parameters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ReplenishmentService(
        IStockQuery stock,
        IConsumptionQuery consumption,
        IProductSupplierRepository relations,
        ISupplierRepository suppliers,
        IPurchaseOrderRepository orders,
        IParameterValues parameters,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _stock = stock;
        _consumption = consumption;
        _relations = relations;
        _suppliers = suppliers;
        _orders = orders;
        _parameters = parameters;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public ReplenishmentPreview Preview(int storeId)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        if (storeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(storeId), "A store is required.");

        var windowDays = _parameters.GetInt(
            SystemParameterCatalog.ReplenishmentConsumptionWindowDays,
            storeId);
        var coverageDays = _parameters.GetInt(
            SystemParameterCatalog.ReplenishmentCoverageDays,
            storeId);

        var today = Today();
        var from = today.AddDays(-(windowDays - 1));
        var sold = _consumption.Get(from, today)
            .GroupBy(row => row.ProductSku, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.Quantity),
                StringComparer.Ordinal);

        var defaults = _relations.List()
            .Where(relation => relation.IsDefault)
            .GroupBy(relation => relation.ProductSku, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var supplierNames = _suppliers.List()
            .ToDictionary(supplier => supplier.Id, supplier => supplier.BusinessName);

        var rows = new List<ReplenishmentSuggestionRow>();

        foreach (var stockRow in _stock.ListByStore(storeId))
        {
            sold.TryGetValue(stockRow.Sku, out var soldUnits);
            var average = soldUnits / (decimal)windowDays;
            var target = (int)Math.Ceiling(average * coverageDays);
            var suggested = Math.Max(
                stockRow.MinStock - stockRow.QuantityAvailable,
                target - stockRow.QuantityAvailable);

            if (suggested <= 0)
                continue;

            defaults.TryGetValue(stockRow.Sku, out var relation);

            rows.Add(new ReplenishmentSuggestionRow(
                stockRow.Sku,
                stockRow.ProductName,
                stockRow.QuantityAvailable,
                stockRow.MinStock,
                Math.Round(average, 2),
                target,
                suggested,
                relation?.SupplierId,
                relation is not null && supplierNames.TryGetValue(relation.SupplierId, out var name)
                    ? name
                    : null,
                relation?.LastCost));
        }

        return new ReplenishmentPreview(
            windowDays,
            coverageDays,
            rows
                .OrderByDescending(row => row.SuggestedQuantity)
                .ThenBy(row => row.ProductName)
                .ToList());
    }

    public ReplenishmentGenerationResult GenerateDrafts(int storeId)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        if (storeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(storeId), "A store is required.");

        var preview = Preview(storeId);
        var ready = preview.Rows.Where(row => row.HasSupplier && row.SupplierId is not null).ToList();
        var skipped = preview.Rows.Count - ready.Count;
        var drafts = new List<ReplenishmentDraft>();

        if (ready.Count == 0)
            return new ReplenishmentGenerationResult(drafts, skipped);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            foreach (var group in ready.GroupBy(row => row.SupplierId!.Value))
            {
                var supplier = _suppliers.GetById(group.Key);

                if (supplier is null || !supplier.Active)
                {
                    skipped += group.Count();
                    continue;
                }

                var order = new PurchaseOrder(
                    0,
                    supplier.Id,
                    storeId,
                    _clock.UtcNow);

                foreach (var row in group)
                    order.AddLine(row.Sku, row.SuggestedQuantity, row.LastCost!.Value);

                _orders.Add(order);
                drafts.Add(new ReplenishmentDraft(
                    order.Id,
                    supplier.Id,
                    supplier.BusinessName,
                    order.Lines.Count,
                    order.Total));

                Record(
                    "ReplenishmentDraftCreated",
                    order.Id,
                    $"Borrador con {order.Lines.Count} líneas para {supplier.BusinessName}, total {order.Total:0}.");
            }

            Record(
                "ReplenishmentDraftsGenerated",
                storeId,
                $"{drafts.Count} borradores agrupados por proveedor; {skipped} filas sin proveedor activo.");
        });

        return new ReplenishmentGenerationResult(drafts, skipped);
    }

    private DateOnly Today() =>
        DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "purchasing",
            ip: _currentUser.Ip));
}
