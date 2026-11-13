namespace TiendaFlow.Domain;

public class InvalidSupplierException : Exception
{
    public InvalidSupplierException(string message) : base(message) { }
}

public class SupplierNotFoundException : Exception
{
    public SupplierNotFoundException(int id) : base($"Supplier {id} was not found.") { }
}

public class DuplicateNitException : Exception
{
    public DuplicateNitException(string nit) : base($"A supplier with NIT {nit} already exists.") { }
}

public class InvalidProductSupplierException : Exception
{
    public InvalidProductSupplierException(string message) : base(message) { }
}
