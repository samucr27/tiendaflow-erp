using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfSupplierRepository : ISupplierRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfSupplierRepository(TiendaFlowDbContext context) => _context = context;

    public Supplier? GetById(int id) => _context.Suppliers.Find(id);

    public Supplier? GetByNit(string nit) =>
        _context.Suppliers.AsNoTracking().FirstOrDefault(supplier => supplier.Nit == nit);

    public IReadOnlyList<Supplier> List() =>
        _context.Suppliers.AsNoTracking().OrderBy(supplier => supplier.BusinessName).ToList();

    public void Add(Supplier supplier)
    {
        _context.Suppliers.Add(supplier);
        _context.SaveChanges();
    }

    public void Update(Supplier supplier)
    {
        if (_context.Entry(supplier).State == EntityState.Detached)
            _context.Suppliers.Update(supplier);

        _context.SaveChanges();
    }
}

public sealed class EfPurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfPurchaseOrderRepository(TiendaFlowDbContext context) => _context = context;

    public PurchaseOrder? GetById(int id) =>
        _context.PurchaseOrders
            .Include(order => order.Lines)
            .FirstOrDefault(order => order.Id == id);

    public IReadOnlyList<PurchaseOrder> List() =>
        _context.PurchaseOrders
            .AsNoTracking()
            .Include(order => order.Lines)
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .ToList();

    public void Add(PurchaseOrder order)
    {
        _context.PurchaseOrders.Add(order);
        _context.SaveChanges();
    }

    public void Update(PurchaseOrder order)
    {
        if (_context.Entry(order).State == EntityState.Detached)
            _context.PurchaseOrders.Update(order);

        _context.SaveChanges();
    }
}
