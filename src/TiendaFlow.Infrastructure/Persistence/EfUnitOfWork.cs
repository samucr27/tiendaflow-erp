using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly TiendaFlowDbContext _context;

    public EfUnitOfWork(TiendaFlowDbContext context) => _context = context;

    public void ExecuteInTransaction(Action action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var transaction = _context.Database.BeginTransaction();

            try
            {
                action();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();

                // The rollback undid the rows, but the tracker still holds the
                // entities saved inside the transaction (with ids that no
                // longer exist) and the failed changes. Forget all of it so
                // the long-lived Blazor context does not reuse them.
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
