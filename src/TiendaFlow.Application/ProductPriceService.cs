using System.Globalization;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class ProductPriceService
{
    private readonly IProductRepository _repository;
    private readonly IAuditLog _auditLog;

    public ProductPriceService(IProductRepository repository, IAuditLog auditLog)
    {
        _repository = repository;
        _auditLog = auditLog;
    }

    public void UpdatePrice(string sku, decimal newPrice, string userId, DateTime date)
    {
        var product = _repository.GetBySku(sku) ?? throw new ProductNotFoundException(sku);
        var oldPrice = product.SalePrice;

        product.ChangePrice(newPrice);
        _repository.Update(product);

        var detail = string.Create(CultureInfo.InvariantCulture,
            $"Sale price changed from {oldPrice} to {newPrice}");
        _auditLog.Record(new AuditEntry("PriceChanged", sku, detail, userId, date));
    }
}
