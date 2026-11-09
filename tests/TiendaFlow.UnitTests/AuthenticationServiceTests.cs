using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class AuthenticationServiceTests
{
    private readonly InMemoryAppUserRepository _users = new();
    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly InMemoryStoreRepository _stores =
        new(new Store(1, "Tienda Centro", "Calle 10 # 20-30"));
    private readonly FakePasswordHasher _hasher = new();
    private readonly RecordingAuditLog _audit = new();

    private AuthenticationService CreateService() =>
        new(_users, _employees, _stores, _hasher, _audit);

    private AppUser GivenCashier(bool active = true)
    {
        var employee = new Employee(0, 1, "900000010", "Ana Torres", "Cajero", "Indefinido", 0m);
        _employees.Add(employee);

        var user = new AppUser(
            employee.Id,
            SystemRoles.IdFor(RoleNames.Cashier),
            "ana@tiendaflow.local",
            _hasher.Hash("Secreta123"),
            active: active);

        _users.Add(user);
        return user;
    }

    [Fact]
    public void SignIn_WithValidCredentials_ReturnsTheUserRoleStoreAndPermissions()
    {
        var user = GivenCashier();

        var authenticated = CreateService().SignIn("ana@tiendaflow.local", "Secreta123", "127.0.0.1");

        Assert.NotNull(authenticated);
        Assert.Equal(user.Id, authenticated!.Id);
        Assert.Equal(RoleNames.Cashier, authenticated.RoleName);
        Assert.Equal(1, authenticated.StoreId);
        Assert.Equal("Tienda Centro", authenticated.StoreName);
        Assert.Equal("Ana Torres", authenticated.FullName);
        Assert.Equal(
            RolePermissions.ForRole(RoleNames.Cashier).OrderBy(item => item),
            authenticated.Permissions.OrderBy(item => item));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("LoginSucceeded", entry.Action);
        Assert.Equal(user.Id, entry.UserId);
        Assert.Equal("security", entry.Module);
    }

    [Fact]
    public void SignIn_WithWrongPassword_ReturnsNullAndAuditsTheFailure()
    {
        GivenCashier();

        var authenticated = CreateService().SignIn(
            "ana@tiendaflow.local",
            "ClaveEquivocada",
            "127.0.0.1");

        Assert.Null(authenticated);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("LoginFailed", entry.Action);
        Assert.Null(entry.UserId);
        Assert.Equal("ana@tiendaflow.local", entry.EntityId);
    }

    [Fact]
    public void SignIn_WithAnInactiveUser_ReturnsNull()
    {
        GivenCashier(active: false);

        var authenticated = CreateService().SignIn("ana@tiendaflow.local", "Secreta123", null);

        Assert.Null(authenticated);
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public void SignIn_WithAnUnknownEmail_ReturnsNull()
    {
        Assert.Null(CreateService().SignIn("nadie@tiendaflow.local", "Secreta123", null));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("LoginFailed", entry.Action);
    }
}
