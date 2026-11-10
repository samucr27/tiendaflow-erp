namespace TiendaFlow.Application;

/// <summary>
/// Runs a unit of work atomically. Application services depend on this
/// abstraction so they never reference a persistence technology.
/// </summary>
public interface IUnitOfWork
{
    void ExecuteInTransaction(Action action);
}
