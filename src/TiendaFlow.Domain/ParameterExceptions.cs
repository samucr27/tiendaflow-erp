namespace TiendaFlow.Domain;

public class InvalidParameterException : Exception
{
    public InvalidParameterException(string message) : base(message) { }
}

public class ParameterNotFoundException : Exception
{
    public ParameterNotFoundException(string key)
        : base($"Parameter {key} was not found.") { }
}
