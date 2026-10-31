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

    public void UpdatePrice(string sku, decimal newPrice, string userId, DateTime date) =>
        throw new NotImplementedException();
}
