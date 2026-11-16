using System.Reflection;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.UnitTests;

internal static class TestIds
{
    /// <summary>
    /// EF Core assigns identity values through the private setter. The
    /// in-memory test doubles mimic that behavior.
    /// </summary>
    public static void Assign(object entity, int id)
    {
        var field = entity.GetType().GetField(
            "<Id>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field is not null)
        {
            field.SetValue(entity, id);
            return;
        }

        var property = entity.GetType().GetProperty(
            "Id",
            BindingFlags.Public | BindingFlags.Instance);

        var setter = property!.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"{entity.GetType().Name}.Id has no setter.");

        setter.Invoke(entity, new object[] { id });
    }
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(
        int? userId = 1,
        string role = RoleNames.Administrator,
        IReadOnlySet<string>? permissions = null,
        bool isAuthenticated = true,
        int? storeId = 1,
        string storeName = "Tienda Centro",
        string? ip = "127.0.0.1")
    {
        UserId = userId;
        Role = role;
        Permissions = permissions
            ?? TiendaFlow.Application.Security.Permissions.All.ToHashSet(StringComparer.Ordinal);
        IsAuthenticated = isAuthenticated;
        StoreId = storeId;
        StoreName = storeName;
        Ip = ip;
    }

    public bool IsAuthenticated { get; }

    public int? UserId { get; }

    public string Name => "Usuario de prueba";

    public string Email => "prueba@tiendaflow.local";

    public string Role { get; }

    public string RoleLabel => SystemRoles.Label(Role);

    public int? StoreId { get; }

    public string StoreName { get; }

    public string? Ip { get; }

    public IReadOnlySet<string> Permissions { get; }
}

internal sealed class RecordingAuditLog : IAuditLog
{
    public List<AuditEntry> Entries { get; } = new();

    public void Record(AuditEntry entry) => Entries.Add(entry);
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string passwordHash, string password) =>
        passwordHash == $"hashed::{password}";
}

internal sealed class AlwaysAllowAccessControl : IAccessControl
{
    public bool Has(string permission) => true;

    public void Demand(string permission)
    {
    }
}

internal sealed class AlwaysDenyAccessControl : IAccessControl
{
    public bool Has(string permission) => false;

    public void Demand(string permission) => throw new AccessDeniedException(permission);
}

internal sealed class InMemoryAppUserRepository : IAppUserRepository
{
    private readonly List<AppUser> _users = new();
    private int _nextId = 1;

    public AppUser? GetById(int id) => _users.FirstOrDefault(user => user.Id == id);

    public AppUser? GetByEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _users.FirstOrDefault(user => user.Email == normalized);
    }

    public IReadOnlyList<AppUser> List() => _users;

    public bool ExistsEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _users.Any(user => user.Email == normalized);
    }

    public void Add(AppUser user)
    {
        TestIds.Assign(user, _nextId++);
        _users.Add(user);
    }

    public void Update(AppUser user)
    {
        if (!_users.Contains(user))
            _users.Add(user);
    }
}

internal sealed class InMemoryEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _employees = new();
    private int _nextId = 1;

    public Employee? GetById(int id) => _employees.FirstOrDefault(employee => employee.Id == id);

    public Employee? GetByIdNumber(string idNumber)
    {
        var normalized = idNumber.Trim();
        return _employees.FirstOrDefault(employee => employee.IdNumber == normalized);
    }

    public IReadOnlyList<Employee> List() => _employees;

    public void Add(Employee employee)
    {
        TestIds.Assign(employee, _nextId++);
        _employees.Add(employee);
    }

    public void Update(Employee employee)
    {
        if (!_employees.Contains(employee))
            _employees.Add(employee);
    }
}

internal sealed class InMemoryStoreRepository : IStoreRepository
{
    private readonly List<Store> _stores = new();

    public InMemoryStoreRepository(params Store[] stores) => _stores.AddRange(stores);

    public Store? GetById(int id) => _stores.FirstOrDefault(store => store.Id == id);

    public IReadOnlyList<Store> List() => _stores;

    public bool ExistsName(string name, int? exceptStoreId = null)
    {
        var normalized = name.Trim();

        return _stores.Any(store =>
            string.Equals(store.Name, normalized, StringComparison.OrdinalIgnoreCase)
            && (exceptStoreId is null || store.Id != exceptStoreId));
    }

    public int NextId() => _stores.Count == 0 ? 1 : _stores.Max(store => store.Id) + 1;

    public void Add(Store store) => _stores.Add(store);

    public void Update(Store store)
    {
        if (!_stores.Contains(store))
            _stores.Add(store);
    }
}

internal sealed class InMemoryRoleRepository : IRoleRepository
{
    private readonly List<Role> _roles = new();

    public InMemoryRoleRepository(params Role[] roles) => _roles.AddRange(roles);

    public Role? GetById(int id) => _roles.FirstOrDefault(role => role.Id == id);

    public IReadOnlyList<Role> List() => _roles;

    public void Add(Role role) => _roles.Add(role);
}

internal sealed class FakeStoreUsageQuery : IStoreUsageQuery
{
    public bool Stock { get; set; }

    public bool OpenOrders { get; set; }

    public bool ActiveUsers { get; set; }

    public bool HasStock(int storeId) => Stock;

    public bool HasOpenPurchaseOrders(int storeId) => OpenOrders;

    public bool HasActiveUsers(int storeId) => ActiveUsers;
}

internal sealed class FakeStoreDirectoryQuery : IStoreDirectoryQuery
{
    public bool WasQueried { get; private set; }

    public IReadOnlyList<StoreListItem> Rows { get; set; } = Array.Empty<StoreListItem>();

    public StoreDetail? Detail { get; set; }

    public IReadOnlyList<StoreListItem> List()
    {
        WasQueried = true;
        return Rows;
    }

    public StoreDetail? Get(int id)
    {
        WasQueried = true;
        return Detail;
    }
}
