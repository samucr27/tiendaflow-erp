namespace TiendaFlow.Domain;

public class DuplicateSkuException : Exception
{
    public DuplicateSkuException(string sku)
        : base($"El SKU {sku} ya esta registrado.") { }
}
