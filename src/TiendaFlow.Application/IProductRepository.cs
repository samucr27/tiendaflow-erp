using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface IProductRepository
{
    bool ExistsBySku(string sku);
    void Add(Product product);
}
