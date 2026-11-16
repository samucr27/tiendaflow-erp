namespace TiendaFlow.Domain;

public class InvalidEmployeeException : Exception
{
    public InvalidEmployeeException(string message) : base(message) { }
}

public class EmployeeNotFoundException : Exception
{
    public EmployeeNotFoundException(int id) : base($"Employee {id} was not found.") { }
}
