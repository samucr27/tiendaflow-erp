using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfUserDirectoryQuery : IUserDirectoryQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfUserDirectoryQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<UserListItem> List()
    {
        var users = _context.AppUsers.AsNoTracking().OrderBy(user => user.Email).ToList();
        var roles = _context.Roles.AsNoTracking().ToDictionary(role => role.Id);
        var employees = _context.Employees.AsNoTracking().ToDictionary(employee => employee.Id);
        var stores = _context.Stores.AsNoTracking().ToDictionary(store => store.Id);

        return users.Select(user =>
        {
            var employee = user.EmployeeId is int id && employees.TryGetValue(id, out var found)
                ? found
                : null;
            var store = employee is not null && stores.TryGetValue(employee.StoreId, out var foundStore)
                ? foundStore
                : null;
            var role = roles[user.RoleId];

            return new UserListItem(
                user.Id,
                user.Email,
                employee?.Name ?? user.Email,
                employee?.IdNumber ?? string.Empty,
                employee?.StoreId,
                store?.Name ?? "Sin tienda",
                user.RoleId,
                role.Name,
                user.Active);
        }).ToList();
    }

    public UserDetail? Get(int id)
    {
        var user = _context.AppUsers.AsNoTracking().FirstOrDefault(candidate => candidate.Id == id);
        if (user is null)
            return null;

        var role = _context.Roles.AsNoTracking().First(candidate => candidate.Id == user.RoleId);
        var employee = user.EmployeeId is int employeeId
            ? _context.Employees.AsNoTracking().FirstOrDefault(candidate => candidate.Id == employeeId)
            : null;
        var store = employee is null
            ? null
            : _context.Stores.AsNoTracking().FirstOrDefault(candidate => candidate.Id == employee.StoreId);

        return new UserDetail(
            user.Id,
            user.Email,
            employee?.Name ?? user.Email,
            employee?.IdNumber ?? string.Empty,
            employee?.Position ?? string.Empty,
            employee?.StoreId,
            store?.Name ?? "Sin tienda",
            user.RoleId,
            role.Name,
            user.Active);
    }
}

public sealed class EfAuditTrailQuery : IAuditTrailQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfAuditTrailQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<AuditTrailRow> Recent(int take)
    {
        var entries = _context.AuditEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.Id)
            .Take(take)
            .ToList();

        var userIds = entries
            .Where(entry => entry.UserId is not null)
            .Select(entry => entry.UserId!.Value)
            .Distinct()
            .ToList();

        var users = _context.AppUsers
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionary(user => user.Id);

        var employeeIds = users.Values
            .Where(user => user.EmployeeId is not null)
            .Select(user => user.EmployeeId!.Value)
            .Distinct()
            .ToList();

        var employees = _context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .ToDictionary(employee => employee.Id);

        return entries.Select(entry =>
        {
            var userName = entry.UserId is int userId && users.TryGetValue(userId, out var user)
                ? user.EmployeeId is int employeeId && employees.TryGetValue(employeeId, out var employee)
                    ? employee.Name
                    : user.Email
                : null;

            return new AuditTrailRow(
                entry.Id,
                entry.Action,
                entry.Module,
                entry.EntityId,
                entry.Detail,
                entry.UserId,
                userName,
                entry.Ip,
                entry.Date);
        }).ToList();
    }
}
