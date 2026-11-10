namespace TiendaFlow.Application;

public interface IAuditLog
{
    void Record(AuditEntry entry);
}
