using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface ISupplierRepository
{
    Supplier? GetById(int id);
    Supplier? GetByNit(string nit);
    IReadOnlyList<Supplier> List();
    void Add(Supplier supplier);
    void Update(Supplier supplier);
}

public interface IPurchaseOrderRepository
{
    PurchaseOrder? GetById(int id);
    IReadOnlyList<PurchaseOrder> List();
    void Add(PurchaseOrder order);
    void Update(PurchaseOrder order);
}

/// <summary>
/// Product-supplier pairs. The pair is the key, so a product may have several
/// suppliers and only one default.
/// </summary>
public interface IProductSupplierRepository
{
    ProductSupplier? Get(string productSku, int supplierId);
    IReadOnlyList<ProductSupplier> List();
    void Add(ProductSupplier relation);
    void Update(ProductSupplier relation);
}
