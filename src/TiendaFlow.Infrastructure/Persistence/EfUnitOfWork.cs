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
                throw;
            }
        });
    }
}
