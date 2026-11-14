using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfInventoryCountRepository : IInventoryCountRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfInventoryCountRepository(TiendaFlowDbContext context) => _context = context;

    public InventoryCount? GetById(int id) =>
        _context.InventoryCounts
            .Include(count => count.Lines)
            .FirstOrDefault(count => count.Id == id);

    public IReadOnlyList<InventoryCount> List(int? storeId = null)
    {
        var query = _context.InventoryCounts
            .AsNoTracking()
            .Include(count => count.Lines)
            .AsQueryable();

        if (storeId is not null)
            query = query.Where(count => count.StoreId == storeId);

        return query
            .OrderByDescending(count => count.CreatedAt)
            .ThenByDescending(count => count.Id)
            .ToList();
    }

    public void Add(InventoryCount count)
    {
        _context.InventoryCounts.Add(count);
        _context.SaveChanges();
    }

    public void Update(InventoryCount count)
    {
        if (_context.Entry(count).State == EntityState.Detached)
            _context.InventoryCounts.Update(count);

        _context.SaveChanges();
    }
}

public sealed class EfInventoryTransferRepository : IInventoryTransferRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfInventoryTransferRepository(TiendaFlowDbContext context) => _context = context;

    public InventoryTransfer? GetById(int id) =>
        _context.InventoryTransfers
            .Include(transfer => transfer.Lines)
            .FirstOrDefault(transfer => transfer.Id == id);

    public IReadOnlyList<InventoryTransfer> List(int? storeId = null)
    {
        var query = _context.InventoryTransfers
            .AsNoTracking()
            .Include(transfer => transfer.Lines)
            .AsQueryable();

        if (storeId is not null)
        {
            query = query.Where(transfer =>
                transfer.OriginStoreId == storeId
                || transfer.DestinationStoreId == storeId);
        }

        return query
            .OrderByDescending(transfer => transfer.CreatedAt)
            .ThenByDescending(transfer => transfer.Id)
            .ToList();
    }

    public void Add(InventoryTransfer transfer)
    {
        _context.InventoryTransfers.Add(transfer);
        _context.SaveChanges();
    }

    public void Update(InventoryTransfer transfer)
    {
        if (_context.Entry(transfer).State == EntityState.Detached)
            _context.InventoryTransfers.Update(transfer);

        _context.SaveChanges();
    }
}
