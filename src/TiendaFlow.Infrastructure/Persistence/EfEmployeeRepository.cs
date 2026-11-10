using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfEmployeeRepository : IEmployeeRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfEmployeeRepository(TiendaFlowDbContext context) => _context = context;

    public Employee? GetById(int id) => _context.Employees.Find(id);

    public Employee? GetByIdNumber(string idNumber)
    {
        var normalized = idNumber.Trim();
        return _context.Employees.FirstOrDefault(employee => employee.IdNumber == normalized);
    }

    public IReadOnlyList<Employee> List() =>
        _context.Employees.AsNoTracking().OrderBy(employee => employee.Name).ToList();

    public void Add(Employee employee)
    {
        _context.Employees.Add(employee);
        _context.SaveChanges();
    }

    public void Update(Employee employee)
    {
        if (_context.Entry(employee).State == EntityState.Detached)
            _context.Employees.Update(employee);

        _context.SaveChanges();
    }
}
