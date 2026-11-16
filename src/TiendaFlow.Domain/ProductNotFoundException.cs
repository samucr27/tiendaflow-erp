namespace TiendaFlow.Domain;

public class ProductNotFoundException : Exception
{
    public ProductNotFoundException(string sku)
        : base($"Product with SKU {sku} was not found.") { }
}
