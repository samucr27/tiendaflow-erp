using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record SetProductSupplierRequest(
    string ProductSku,
    int SupplierId,
    decimal LastCost,
    bool IsDefault);

/// <summary>
/// Maintains the product-supplier relation. The first supplier recorded for a
/// product becomes the default automatically; after that, making a pair the
/// default clears the previous one for the same SKU inside one transaction.
/// </summary>
public sealed class ProductSupplierService
{
    private readonly IProductSupplierRepository _relations;
    private readonly ISupplierRepository _suppliers;
    private readonly IProductCatalogQuery _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public ProductSupplierService(
        IProductSupplierRepository relations,
        ISupplierRepository suppliers,
        IProductCatalogQuery products,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _relations = relations;
        _suppliers = suppliers;
        _products = products;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public IReadOnlyList<ProductSupplierRow> List()
    {
        _accessControl.Demand(Permissions.PurchasingSuppliersView);

        var products = _products.Search(null)
            .ToDictionary(product => product.Sku, product => product.Name, StringComparer.Ordinal);
        var suppliers = _suppliers.List()
            .ToDictionary(supplier => supplier.Id);

        return _relations.List()
            .OrderByDescending(relation => relation.IsDefault)
            .ThenBy(relation => relation.ProductSku)
            .ThenBy(relation => suppliers.TryGetValue(relation.SupplierId, out var supplier)
                ? supplier.BusinessName
                : relation.SupplierId.ToString())
            .Select(relation =>
            {
                suppliers.TryGetValue(relation.SupplierId, out var supplier);

                return new ProductSupplierRow(
                    relation.ProductSku,
                    products.TryGetValue(relation.ProductSku, out var name)
                        ? name
                        : relation.ProductSku,
                    relation.SupplierId,
                    supplier?.BusinessName ?? $"Proveedor {relation.SupplierId}",
                    relation.LastCost,
                    relation.IsDefault,
                    supplier?.Active ?? false);
            })
            .ToList();
    }

    /// <summary>
    /// Last known cost of the default supplier, used by the manual order form
    /// to prefill the unit cost. It returns null when no relation exists.
    /// </summary>
    public decimal? DefaultCost(string productSku)
    {
        _accessControl.Demand(Permissions.PurchasingOrdersCreate);

        var sku = productSku.Trim();
        return _relations.List()
            .Where(relation => relation.ProductSku == sku && relation.IsDefault)
            .Select(relation => (decimal?)relation.LastCost)
            .FirstOrDefault();
    }

    public void Set(SetProductSupplierRequest request)
    {
        _accessControl.Demand(Permissions.PurchasingSuppliersManage);

        var sku = request.ProductSku.Trim();
        if (_products.GetBySku(sku) is null)
            throw new ProductNotFoundException(sku);

        var supplier = _suppliers.GetById(request.SupplierId)
            ?? throw new SupplierNotFoundException(request.SupplierId);

        if (!supplier.Active)
            throw new InvalidProductSupplierException("The supplier is inactive.");

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var siblings = _relations.List()
                .Where(relation => relation.ProductSku == sku)
                .ToList();
            var relation = siblings.FirstOrDefault(candidate =>
                candidate.SupplierId == request.SupplierId);

            var makeDefault = request.IsDefault || siblings.Count == 0;

            if (relation is null)
            {
                relation = new ProductSupplier(
                    sku,
                    supplier.Id,
                    request.LastCost,
                    makeDefault);
                _relations.Add(relation);
            }
            else
            {
                relation.ChangeCost(request.LastCost);

                if (makeDefault)
                    relation.MarkAsDefault();
                else
                    relation.ClearDefault();

                _relations.Update(relation);
            }

            if (makeDefault)
            {
                foreach (var sibling in siblings.Where(candidate =>
                    candidate.SupplierId != supplier.Id && candidate.IsDefault))
                {
                    sibling.ClearDefault();
                    _relations.Update(sibling);
                }
            }

            Record(
                "ProductSupplierSet",
                $"{sku}:{supplier.Id}",
                $"Producto {sku} con proveedor {supplier.BusinessName}, costo {request.LastCost:0} y predeterminado {(makeDefault ? "sí" : "no")}.");
        });
    }

    private void Record(string action, string entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId,
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "purchasing",
            ip: _currentUser.Ip));
}
