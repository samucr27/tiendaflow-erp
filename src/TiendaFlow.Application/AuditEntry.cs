namespace TiendaFlow.Application;

public record AuditEntry(string Action, string EntityId, string Detail, string UserId, DateTime Date);
