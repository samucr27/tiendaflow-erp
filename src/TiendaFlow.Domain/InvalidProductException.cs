namespace TiendaFlow.Domain;

public class InvalidProductException : Exception
{
    public InvalidProductException(string message) : base(message) { }
}
