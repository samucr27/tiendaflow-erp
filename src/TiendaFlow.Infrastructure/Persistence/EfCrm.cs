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

    public IReadOnlyList<CustomerRow> List() => Read(orderByName: true).ToList();

    public CustomerRow? GetById(int id) => Read(id: id).FirstOrDefault();

    private IQueryable<CustomerRow> Read(int? id = null, bool orderByName = false)
    {
        var customers = _context.Customers.AsNoTracking();

        if (id is int customerId)
            customers = customers.Where(customer => customer.Id == customerId);

        if (orderByName)
            customers = customers.OrderBy(customer => customer.FullName);

        return customers.Select(customer => new CustomerRow(
            customer.Id,
            customer.DocumentNumber,
            customer.FullName,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.Active));
    }
}
