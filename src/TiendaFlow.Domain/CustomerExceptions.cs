namespace TiendaFlow.Domain;

public class InvalidCustomerException : Exception
{
    public InvalidCustomerException(string message) : base(message) { }
}

public class DuplicateCustomerDocumentException : Exception
{
    public DuplicateCustomerDocumentException(string documentNumber)
        : base($"Customer document {documentNumber} is already registered.") { }
}

public class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(int id) : base($"Customer {id} was not found.") { }
}
