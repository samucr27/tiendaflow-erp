namespace TiendaFlow.Application;

/// <summary>
/// Default unit of work for in-memory repositories and tests: it only runs
/// the action, preserving the behaviour existing callers already expect.
/// </summary>
internal sealed class NoOpUnitOfWork : IUnitOfWork
{
    public static readonly NoOpUnitOfWork Instance = new();

    private NoOpUnitOfWork() { }

    public void ExecuteInTransaction(Action action) => action();
}
