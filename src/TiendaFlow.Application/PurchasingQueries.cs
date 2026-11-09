namespace TiendaFlow.Application;

public interface ISupplierQuery
{
    IReadOnlyList<SupplierRow> List();
    SupplierRow? GetById(int id);
}

public interface IPurchaseOrderQuery
{
    IReadOnlyList<PurchaseOrderRow> List();
    IReadOnlyList<PurchaseOrderRow> ListBySupplier(int supplierId);
    PurchaseOrderDetail? GetById(int id);
}
