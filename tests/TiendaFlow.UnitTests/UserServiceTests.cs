using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class UserServiceTests
{
    private const int StoreId = 1;
    private const int CashierRoleId = 4;

    private readonly InMemoryAppUserRepository _users = new();
    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly InMemoryStoreRepository _stores =
        new(new Store(StoreId, "Tienda Centro", "Calle 10 # 20-30"));
    private readonly InMemoryRoleRepository _roles =
        new(RoleNames.All.Select(name => new Role(SystemRoles.IdFor(name), name)).ToArray());
    private readonly FakePasswordHasher _hasher = new();
    private readonly RecordingAuditLog _audit = new();

    private UserService CreateService(
        IAccessControl? accessControl = null,
        ICurrentUser? currentUser = null) =>
        new(
            _users,
            _employees,
            _stores,
            _roles,
            _hasher,
            NoOpUnitOfWork.Instance,
            accessControl ?? new AlwaysAllowAccessControl(),
            _audit,
            currentUser ?? new FakeCurrentUser());

    [Fact]
    public void CreateUser_PersistsEmployeeAndHashedPasswordAndAudits()
    {
        var id = CreateService().CreateUser(new CreateUserRequest(
            "nuevo@tiendaflow.local",
            "Nuevo Empleado",
            "900000001",
            "Cajero",
            StoreId,
            CashierRoleId,
            "Secreta123"));

        Assert.True(id > 0);

        var user = _users.GetById(id);
        Assert.NotNull(user);
        Assert.Equal("nuevo@tiendaflow.local", user!.Email);
        Assert.Equal(CashierRoleId, user.RoleId);
        Assert.Equal("hashed::Secreta123", user.PasswordHash);
        Assert.NotNull(user.EmployeeId);

        var employee = _employees.GetById(user.EmployeeId!.Value);
        Assert.Equal("Nuevo Empleado", employee!.Name);
        Assert.Equal("900000001", employee.IdNumber);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("UserCreated", entry.Action);
        Assert.Equal("security", entry.Module);
        Assert.Equal(1, entry.UserId);
        Assert.DoesNotContain("nuevo@tiendaflow.local", entry.Detail);
    }

    [Fact]
    public void CreateUser_WithoutTheManagePermission_IsRejected()
    {
        Assert.Throws<AccessDeniedException>(() =>
            CreateService(new AlwaysDenyAccessControl()).CreateUser(new CreateUserRequest(
                "nuevo@tiendaflow.local",
                "Nuevo Empleado",
                "900000002",
                "Cajero",
                StoreId,
                CashierRoleId,
                "Secreta123")));

        Assert.Empty(_users.List());
        Assert.Empty(_employees.List());
    }

    [Fact]
    public void CreateUser_WithDuplicateEmail_IsRejected()
    {
        _users.Add(new AppUser(null, CashierRoleId, "repetido@tiendaflow.local", "hash"));

        Assert.Throws<DuplicateEmailException>(() =>
            CreateService().CreateUser(new CreateUserRequest(
                "repetido@tiendaflow.local",
                "Otro Empleado",
                "900000003",
                "Cajero",
                StoreId,
                CashierRoleId,
                "Secreta123")));
    }

    [Fact]
    public void CreateUser_WithDuplicateIdNumber_IsRejected()
    {
        var employee = new Employee(0, StoreId, "900000004", "Existente", "Cajero", "Indefinido", 0m);
        _employees.Add(employee);

        Assert.Throws<DuplicateIdNumberException>(() =>
            CreateService().CreateUser(new CreateUserRequest(
                "otro@tiendaflow.local",
                "Otro Empleado",
                "900000004",
                "Cajero",
                StoreId,
                CashierRoleId,
                "Secreta123")));
    }

    [Fact]
    public void UpdateUser_CannotDeactivateTheCurrentUser()
    {
        var employee = new Employee(0, StoreId, "900000005", "Admin", "Administrador", "Indefinido", 0m);
        _employees.Add(employee);
        var user = new AppUser(employee.Id, 1, "admin@tiendaflow.local", "hash");
        _users.Add(user);

        Assert.Throws<InvalidUserException>(() =>
            CreateService().UpdateUser(user.Id, new UpdateUserRequest(
                "admin@tiendaflow.local",
                "Admin",
                "900000005",
                "Administrador",
                StoreId,
                1,
                Active: false)));
    }

    [Fact]
    public void UpdateUser_ChangesTheRoleAndThePassword()
    {
        var employee = new Employee(0, StoreId, "900000006", "Empleado", "Cajero", "Indefinido", 0m);
        _employees.Add(employee);
        var user = new AppUser(employee.Id, CashierRoleId, "empleado@tiendaflow.local", "hash");
        _users.Add(user);

        CreateService().UpdateUser(user.Id, new UpdateUserRequest(
            "empleado@tiendaflow.local",
            "Empleado Actualizado",
            "900000006",
            "Jefe de tienda",
            StoreId,
            SystemRoles.IdFor(RoleNames.StoreManager),
            Active: true,
            NewPassword: "NuevaClave123"));

        Assert.Equal(SystemRoles.IdFor(RoleNames.StoreManager), user.RoleId);
        Assert.Equal("hashed::NuevaClave123", user.PasswordHash);

        var entry = _audit.Entries.Last();
        Assert.Equal("UserUpdated", entry.Action);
        Assert.Contains("Store Manager", entry.Detail);
    }

    [Fact]
    public void ResetPassword_StoresOnlyTheHash()
    {
        _users.Add(new AppUser(null, CashierRoleId, "reset@tiendaflow.local", "old-hash"));
        var user = _users.GetByEmail("reset@tiendaflow.local")!;

        CreateService().ResetPassword(user.Id, "OtraClave123");

        Assert.Equal("hashed::OtraClave123", user.PasswordHash);
        Assert.DoesNotContain("OtraClave123", _audit.Entries.Last().Detail);
    }
}
