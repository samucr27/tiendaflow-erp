using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfCustomerRepository(TiendaFlowDbContext context) => _context = context;

    public Customer? GetById(int id) => _context.Customers.Find(id);

    public Customer? GetByDocument(string documentNumber) =>
        _context.Customers
            .AsNoTracking()
            .FirstOrDefault(customer => customer.DocumentNumber == documentNumber);

    public void Add(Customer customer)
    {
        _context.Customers.Add(customer);
        _context.SaveChanges();
    }

    public void Update(Customer customer)
    {
        if (_context.Entry(customer).State == EntityState.Detached)
            _context.Customers.Update(customer);

        _context.SaveChanges();
    }
}

public sealed class EfCustomerQuery : ICustomerQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfCustomerQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<CustomerRow> List() =>
        Read().OrderBy(row => row.FullName).ToList();

    public CustomerRow? GetById(int id) =>
        Read().FirstOrDefault(row => row.Id == id);

    private IQueryable<CustomerRow> Read() =>
        from customer in _context.Customers.AsNoTracking()
        select new CustomerRow(
            customer.Id,
            customer.DocumentNumber,
            customer.FullName,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.Active);
}
