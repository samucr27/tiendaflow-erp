namespace TiendaFlow.Application;

/// <summary>
/// Minimal persistence port for the closing modules (returns, contracts,
/// attendance, shifts, loyalty points, PQRS and campaigns). Their data sets
/// are small, so the services filter the full list in memory; the EF Core
/// implementation re-reads tracked entities before an update.
/// </summary>
public interface IEntityStore<T> where T : class
{
    T? Get(int id);

    IReadOnlyList<T> All();

    void Add(T entity);

    void Update(T entity);
}
