namespace TiendaFlow.Domain;

public class InvalidPurchaseOrderException : Exception
{
    public InvalidPurchaseOrderException(string message) : base(message) { }
}

public class PurchaseOrderNotFoundException : Exception
{
    public PurchaseOrderNotFoundException(int id) : base($"Purchase order {id} was not found.") { }
}
