using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class InventoryService
{
    private readonly IProductRepository _repository;
    private readonly IAccessControl _accessControl;

    public InventoryService(IProductRepository repository, IAccessControl accessControl)
    {
        _repository = repository;
        _accessControl = accessControl;
    }

    public void CreateProduct(Product product)
    {
        _accessControl.Demand(Permissions.InventoryProductsManage);

        if (_repository.ExistsBySku(product.Sku))
            throw new DuplicateSkuException(product.Sku);

        if (_repository.ExistsByBarcode(product.Barcode))
            throw new DuplicateBarcodeException(product.Barcode);

        _repository.Add(product);
    }
}
