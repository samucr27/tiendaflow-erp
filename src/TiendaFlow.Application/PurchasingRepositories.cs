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
