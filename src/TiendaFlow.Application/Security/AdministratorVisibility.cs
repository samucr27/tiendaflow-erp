using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Hides the employee records that belong to Administrator users from every
/// caller who is not an Administrator.
/// </summary>
public sealed class AdministratorVisibility
{
    private readonly IAppUserRepository _users;
    private readonly ICurrentUser _currentUser;

    public AdministratorVisibility(IAppUserRepository users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public bool CallerIsAdministrator => _currentUser.Role == RoleNames.Administrator;

    public IReadOnlySet<int> HiddenEmployeeIds()
    {
        if (CallerIsAdministrator)
            return new HashSet<int>();

        var administratorRoleId = SystemRoles.IdFor(RoleNames.Administrator);

        return _users.List()
            .Where(user => user.RoleId == administratorRoleId && user.EmployeeId is not null)
            .Select(user => user.EmployeeId!.Value)
            .ToHashSet();
    }

    public IEnumerable<Employee> Filter(IEnumerable<Employee> employees)
    {
        var hidden = HiddenEmployeeIds();
        return hidden.Count == 0
            ? employees
            : employees.Where(employee => !hidden.Contains(employee.Id));
    }
}