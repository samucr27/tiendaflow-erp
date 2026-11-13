using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfSystemParameterRepository : ISystemParameterRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfSystemParameterRepository(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<SystemParameter> List() =>
        _context.SystemParameters
            .AsNoTracking()
            .OrderBy(parameter => parameter.Key)
            .ThenBy(parameter => parameter.Scope)
            .ThenBy(parameter => parameter.StoreId)
            .ThenBy(parameter => parameter.EffectiveFrom)
            .ToList();

    public IReadOnlyList<SystemParameter> ListForKey(string key)
    {
        var normalized = (key ?? string.Empty).Trim();

        return _context.SystemParameters
            .AsNoTracking()
            .Where(parameter => parameter.Key == normalized)
            .OrderBy(parameter => parameter.EffectiveFrom)
            .ThenBy(parameter => parameter.Version)
            .ToList();
    }

    public SystemParameter? GetById(int id) => _context.SystemParameters.Find(id);

    public SystemParameter? GetLatest(
        string key,
        ParameterScope scope,
        int? storeId,
        DateOnly asOf)
    {
        var normalized = (key ?? string.Empty).Trim();

        return _context.SystemParameters
            .AsNoTracking()
            .Where(parameter =>
                parameter.Key == normalized
                && parameter.Scope == scope
                && parameter.StoreId == storeId
                && parameter.EffectiveFrom <= asOf)
            .OrderByDescending(parameter => parameter.EffectiveFrom)
            .ThenByDescending(parameter => parameter.Version)
            .FirstOrDefault();
    }

    public void Add(SystemParameter parameter)
    {
        _context.SystemParameters.Add(parameter);
        _context.SaveChanges();
    }
}

/// <summary>
/// Resolves the author display name without exposing the whole user directory
/// to the parameter screens.
/// </summary>
public sealed class EfParameterAuthorQuery : IParameterAuthorQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfParameterAuthorQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyDictionary<int, string> Names()
    {
        var users = _context.AppUsers
            .AsNoTracking()
            .Select(user => new { user.Id, user.EmployeeId, user.Email })
            .ToList();

        var employeeIds = users
            .Where(user => user.EmployeeId is not null)
            .Select(user => user.EmployeeId!.Value)
            .Distinct()
            .ToList();

        var employees = _context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .ToDictionary(employee => employee.Id, employee => employee.Name);

        return users.ToDictionary(
            user => user.Id,
            user => user.EmployeeId is int employeeId
                && employees.TryGetValue(employeeId, out var name)
                    ? name
                    : user.Email);
    }
}
