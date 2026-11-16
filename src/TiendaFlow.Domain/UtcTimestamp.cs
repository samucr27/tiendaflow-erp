namespace TiendaFlow.Domain;

/// <summary>
/// Every business timestamp is stored in UTC. Values that arrive without a
/// kind are treated as UTC because all application callers already send UTC.
/// </summary>
public static class UtcTimestamp
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
