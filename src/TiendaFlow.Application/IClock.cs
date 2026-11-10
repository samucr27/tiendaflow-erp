namespace TiendaFlow.Application;

/// <summary>
/// Source of the current UTC instant. Screens and services never call
/// DateTime.Now for business timestamps.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
