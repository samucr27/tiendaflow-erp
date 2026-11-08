using System.Globalization;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class ProductPriceService
{
    private readonly IProductRepository _repository;
    private readonly IAuditLog _auditLog;
    private readonly IUnitOfWork _unitOfWork;

    public ProductPriceService(
        IProductRepository repository,
        IAuditLog auditLog,
        IUnitOfWork? unitOfWork = null)
    {
        _repository = repository;
        _auditLog = auditLog;
        _unitOfWork = unitOfWork ?? NoOpUnitOfWork.Instance;
    }

    public void UpdatePrice(string sku, decimal newPrice, int? userId, DateTime date)
    {
        _unitOfWork.ExecuteInTransaction(() =>
        {
            var product = _repository.GetBySku(sku) ?? throw new ProductNotFoundException(sku);
            var oldPrice = product.SalePrice;

            product.ChangePrice(newPrice);
            _repository.Update(product);

            var detail = string.Create(CultureInfo.InvariantCulture,
                $"Sale price changed from {oldPrice} to {newPrice}");
            _auditLog.Record(new AuditEntry("PriceChanged", sku, detail, userId, date));
        });
    }
}
