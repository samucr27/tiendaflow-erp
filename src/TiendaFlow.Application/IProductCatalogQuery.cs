namespace TiendaFlow.Application;

public interface IProductCatalogQuery
{
    IReadOnlyList<ProductListItem> Search(string? term);

    ProductListItem? GetBySku(string sku);

    int Count();
}
