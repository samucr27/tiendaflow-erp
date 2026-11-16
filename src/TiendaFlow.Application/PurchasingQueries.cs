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

public record ProductConsumption(string ProductSku, int Quantity);

/// <summary>
/// Units sold per SKU in an inclusive local-date range, read from issued
/// invoices. Cancelled invoices do not feed the replenishment average.
/// </summary>
public interface IConsumptionQuery
{
    IReadOnlyList<ProductConsumption> Get(DateOnly from, DateOnly to);
}
