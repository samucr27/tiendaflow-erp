using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public sealed record AuditEntry
{
    public AuditEntry(
        string action,
        string entityId,
        string detail,
        int? userId,
        DateTime date,
        string module = "inventory",
        string? ip = null)
    {
        Action = action;
        EntityId = entityId;
        Detail = detail;
        UserId = userId;
        Date = UtcTimestamp.Normalize(date);
        Module = module;
        Ip = ip;
    }

    public long Id { get; private set; }

    public string Action { get; }

    public string EntityId { get; }

    public string Detail { get; }

    public int? UserId { get; }

    public string Module { get; }

    public string? Ip { get; }

    public DateTime Date { get; }
}
