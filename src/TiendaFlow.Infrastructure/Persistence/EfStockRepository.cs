using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public class EfStockRepository : IStockRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfStockRepository(TiendaFlowDbContext context) => _context = context;

    public StockItem? Get(int storeId, string sku) =>
        _context.Stocks.FirstOrDefault(item => item.StoreId == storeId && item.Sku == sku);

    public void Add(StockItem item)
    {
        _context.Stocks.Add(item);
        _context.SaveChanges();
    }

    public void Update(StockItem item)
    {
        if (_context.Entry(item).State == EntityState.Detached)
            _context.Stocks.Update(item);

        _context.SaveChanges();
    }
}
