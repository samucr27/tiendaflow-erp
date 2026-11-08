namespace TiendaFlow.Application;

public interface IStockQuery
{
    IReadOnlyList<StockRow> ListByStore(int storeId);

    IReadOnlyList<StockRow> ListBySku(string sku);
}
