namespace TiendaFlow.Domain;

public class InsufficientStockException : Exception
{
    public InsufficientStockException(string sku, int available, int requested)
        : base($"Insufficient stock for {sku}: {available} available, {requested} requested.") { }
}
