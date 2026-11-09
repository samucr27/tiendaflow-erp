namespace TiendaFlow.Domain;

/// <summary>
/// A person allowed to sign in to TiendaFlow. Each user has exactly one role
/// and belongs to one employee record, which supplies the store and full name.
/// </summary>
public class AppUser
{
    public AppUser(
        int? employeeId,
        int roleId,
        string email,
        string passwordHash,
        bool twoFactor = false,
        bool active = true)
    {
        if (employeeId is < 0)
            throw new InvalidUserException("Employee id must be positive.");
        if (roleId <= 0)
            throw new InvalidUserException("Role is required.");
        RequireEmail(email);
        RequireHash(passwordHash);

        EmployeeId = employeeId;
        RoleId = roleId;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        TwoFactor = twoFactor;
        Active = active;
    }

    public int Id { get; private set; }

    public int? EmployeeId { get; private set; }

    public int RoleId { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public bool TwoFactor { get; private set; }

    public bool Active { get; private set; }

    public void AssignEmployee(int? employeeId)
    {
        if (employeeId is < 0)
            throw new InvalidUserException("Employee id must be positive.");

        EmployeeId = employeeId;
    }

    public void AssignRole(int roleId)
    {
        if (roleId <= 0)
            throw new InvalidUserException("Role is required.");

        RoleId = roleId;
    }

    public void ChangeEmail(string email)
    {
        RequireEmail(email);
        Email = email.Trim().ToLowerInvariant();
    }

    public void ChangePassword(string passwordHash)
    {
        RequireHash(passwordHash);
        PasswordHash = passwordHash;
    }

    public void EnableTwoFactor() => TwoFactor = true;

    public void DisableTwoFactor() => TwoFactor = false;

    public void Deactivate() => Active = false;

    public void Activate() => Active = true;

    private static void RequireEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new InvalidUserException("A valid email is required.");
    }

    private static void RequireHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new InvalidUserException("A password hash is required.");
    }
}
