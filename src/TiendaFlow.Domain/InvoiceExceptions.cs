namespace TiendaFlow.Domain;

public class InvalidInvoiceException : Exception
{
    public InvalidInvoiceException(string message) : base(message) { }
}

public class InvoiceNotFoundException : Exception
{
    public InvoiceNotFoundException(int id) : base($"Invoice {id} was not found.") { }
}
