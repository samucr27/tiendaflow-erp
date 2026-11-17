namespace TiendaFlow.Domain;

public class InvalidSalesReturnException : Exception
{
    public InvalidSalesReturnException(string message) : base(message) { }
}

public class SalesReturnNotFoundException : Exception
{
    public SalesReturnNotFoundException(int id) : base($"Sales return {id} was not found.") { }
}
