using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public class EfStockRepository : IStockRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfStockRepository(TiendaFlowDbContext context)
    {
        _context = context;
    }

    public StockItem? Get(int storeId, string sku)
    {
        return _context.StockItems
            .FirstOrDefault(s => s.StoreId == storeId && s.Sku == sku);
    }

    public void Add(StockItem item)
    {
        _context.StockItems.Add(item);
        _context.SaveChanges();
    }

    public void Update(StockItem item)
    {
        _context.StockItems.Update(item);
        _context.SaveChanges();
    }
}