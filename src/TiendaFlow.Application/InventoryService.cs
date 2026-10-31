using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class InventoryService
{
    private readonly IProductRepository _repository;

    public InventoryService(IProductRepository repository) => _repository = repository;

    public void CreateProduct(Product product)
    {
        if (_repository.ExistsBySku(product.Sku))
            throw new DuplicateSkuException(product.Sku);

        _repository.Add(product);
    }
}
