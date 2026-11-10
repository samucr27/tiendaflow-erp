namespace TiendaFlow.Domain;

public class DuplicateSkuException : Exception
{
    public DuplicateSkuException(string sku)
        : base($"SKU {sku} is already registered.") { }
}
