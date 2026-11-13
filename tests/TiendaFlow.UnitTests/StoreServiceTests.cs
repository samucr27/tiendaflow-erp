using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class StoreServiceTests
{
    private readonly InMemoryStoreRepository _stores = new(
        new Store(1, "Tienda Centro", "Calle 10 # 20-30"));

    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly InMemoryAppUserRepository _users = new();
    private readonly FakeStoreUsageQuery _usage = new();
    private readonly RecordingAuditLog _audit = new();

    private StoreService CreateService(IAccessControl? accessControl = null) =>
        new(
            _stores,
            _employees,
            _users,
            _usage,
            NoOpUnitOfWork.Instance,
            accessControl ?? new AlwaysAllowAccessControl(),
            _audit,
            new FakeCurrentUser());

    [Fact]
    public void CreateStore_PersistsAndAudits()
    {
        var id = CreateService().CreateStore(new CreateStoreRequest(
            "  Tienda Norte  ",
            "  Carrera 15 # 80-20  "));

        Assert.Equal(2, id);
        var store = _stores.GetById(id);
        Assert.NotNull(store);
        Assert.Equal("Tienda Norte", store!.Name);
        Assert.Equal("Carrera 15 # 80-20", store.Address);
        Assert.True(store.Active);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("StoreCreated", entry.Action);
        Assert.Equal("security", entry.Module);
        Assert.Equal(1, entry.UserId);
        Assert.Equal("127.0.0.1", entry.Ip);
    }

    [Fact]
    public void CreateStore_WithDuplicateName_IsRejectedIgnoringCase()
    {
        Assert.Throws<DuplicateStoreNameException>(() =>
            CreateService().CreateStore(new CreateStoreRequest(
                "tienda centro",
                "Otra dirección")));

        Assert.Single(_stores.List());
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void CreateStore_WithoutTheManagePermission_IsRejectedBeforeTouchingRepositories()
    {
        Assert.Throws<AccessDeniedException>(() =>
            CreateService(new AlwaysDenyAccessControl()).CreateStore(new CreateStoreRequest(
                "Tienda Norte",
                "Carrera 15 # 80-20")));

        Assert.Single(_stores.List());
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void UpdateStore_RenamesChangesAddressAndAudits()
    {
        CreateService().UpdateStore(1, new UpdateStoreRequest(
            "Tienda Centro Renovada",
            "Calle 11 # 21-31"));

        var store = _stores.GetById(1)!;
        Assert.Equal("Tienda Centro Renovada", store.Name);
        Assert.Equal("Calle 11 # 21-31", store.Address);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("StoreUpdated", entry.Action);
        Assert.Contains("Tienda Centro Renovada", entry.Detail);
    }

    [Fact]
    public void UpdateStore_WithAnotherStoresName_IsRejected()
    {
        _stores.Add(new Store(2, "Tienda Norte", "Carrera 15 # 80-20"));

        Assert.Throws<DuplicateStoreNameException>(() =>
            CreateService().UpdateStore(2, new UpdateStoreRequest(
                "Tienda Centro",
                "Carrera 15 # 80-20")));

        Assert.Equal("Tienda Norte", _stores.GetById(2)!.Name);
    }

    [Fact]
    public void UpdateStore_WithItsOwnName_IsAllowed()
    {
        CreateService().UpdateStore(1, new UpdateStoreRequest(
            "Tienda Centro",
            "Calle 99 # 1-2"));

        Assert.Equal("Calle 99 # 1-2", _stores.GetById(1)!.Address);
    }

    [Fact]
    public void UpdateStore_WithUnknownId_IsRejected()
    {
        Assert.Throws<StoreNotFoundException>(() =>
            CreateService().UpdateStore(99, new UpdateStoreRequest("Tienda", "Dirección")));
    }

    [Fact]
    public void DeactivateStore_WithStock_IsRejected()
    {
        _usage.Stock = true;

        Assert.Throws<StoreHasStockException>(() => CreateService().DeactivateStore(1));

        Assert.True(_stores.GetById(1)!.Active);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void DeactivateStore_WithOpenPurchaseOrders_IsRejected()
    {
        _usage.OpenOrders = true;

        Assert.Throws<StoreHasOpenOrdersException>(() => CreateService().DeactivateStore(1));

        Assert.True(_stores.GetById(1)!.Active);
    }

    [Fact]
    public void DeactivateStore_WithActiveUsers_IsRejected()
    {
        _usage.ActiveUsers = true;

        Assert.Throws<StoreHasActiveUsersException>(() => CreateService().DeactivateStore(1));

        Assert.True(_stores.GetById(1)!.Active);
    }

    [Fact]
    public void DeactivateStore_WithoutBlockers_DeactivatesAndAudits()
    {
        CreateService().DeactivateStore(1);

        Assert.False(_stores.GetById(1)!.Active);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("StoreDeactivated", entry.Action);
    }

    [Fact]
    public void DeactivateStore_AlreadyInactive_DoesNotAuditAgain()
    {
        var store = _stores.GetById(1)!;
        store.Deactivate();

        CreateService().DeactivateStore(1);

        Assert.False(store.Active);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void ActivateStore_ReactivatesAndAudits()
    {
        var store = _stores.GetById(1)!;
        store.Deactivate();

        CreateService().ActivateStore(1);

        Assert.True(store.Active);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("StoreActivated", entry.Action);
    }

    [Fact]
    public void ActivateStore_AlreadyActive_DoesNotAuditAgain()
    {
        CreateService().ActivateStore(1);

        Assert.True(_stores.GetById(1)!.Active);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void DeactivateStore_WithoutTheManagePermission_IsRejected()
    {
        Assert.Throws<AccessDeniedException>(() =>
            CreateService(new AlwaysDenyAccessControl()).DeactivateStore(1));

        Assert.True(_stores.GetById(1)!.Active);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void AssignEmployeeToStore_MovesTheEmployeeAndAudits()
    {
        _stores.Add(new Store(2, "Tienda Norte", "Carrera 15 # 80-20"));
        _employees.Add(new Employee(0, 1, "900000001", "Ana", "Cajero", "Indefinido", 0m));
        var employee = _employees.GetById(1)!;

        CreateService().AssignEmployeeToStore(employee.Id, 2);

        Assert.Equal(2, employee.StoreId);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("StoreEmployeeAssigned", entry.Action);
        Assert.Contains("Empleado 1", entry.Detail);
    }

    [Fact]
    public void AssignUserToStore_MovesTheLinkedEmployeeAndAudits()
    {
        _stores.Add(new Store(2, "Tienda Norte", "Carrera 15 # 80-20"));
        var employee = new Employee(0, 1, "900000002", "Luis", "Cajero", "Indefinido", 0m);
        _employees.Add(employee);
        _users.Add(new AppUser(employee.Id, 4, "luis@tiendaflow.local", "hash"));

        CreateService().AssignUserToStore(1, 2);

        Assert.Equal(2, employee.StoreId);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("StoreUserAssigned", entry.Action);
        Assert.Contains("Usuario 1", entry.Detail);
    }

    [Fact]
    public void AssignEmployeeToInactiveStore_IsRejected()
    {
        _stores.Add(new Store(3, "Tienda Cerrada", "Calle 1 # 2-3", active: false));
        var employee = new Employee(0, 1, "900000003", "Sara", "Cajero", "Indefinido", 0m);
        _employees.Add(employee);

        Assert.Throws<InvalidStoreException>(() =>
            CreateService().AssignEmployeeToStore(employee.Id, 3));

        Assert.Equal(1, employee.StoreId);
    }

    [Fact]
    public void AssignUserWithoutEmployee_IsRejected()
    {
        _users.Add(new AppUser(null, 4, "sin.empleado@tiendaflow.local", "hash"));

        Assert.Throws<InvalidStoreException>(() => CreateService().AssignUserToStore(1, 1));
    }

    [Fact]
    public void AssignEmployeeToItsCurrentStore_DoesNotAudit()
    {
        var employee = new Employee(0, 1, "900000004", "Mario", "Cajero", "Indefinido", 0m);
        _employees.Add(employee);

        CreateService().AssignEmployeeToStore(employee.Id, 1);

        Assert.Empty(_audit.Entries);
    }
}

public class StoreDirectoryServiceTests
{
    [Fact]
    public void List_ReturnsRowsWhenTheUserMayView()
    {
        var query = new FakeStoreDirectoryQuery
        {
            Rows = new[]
            {
                new StoreListItem(1, "Tienda Centro", "Calle 10", true, 2, 3, 40, 0)
            }
        };
        var service = new StoreDirectoryService(query, new AlwaysAllowAccessControl());

        var rows = service.List();

        Assert.Single(rows);
        Assert.True(query.WasQueried);
    }

    [Fact]
    public void List_WithoutTheViewPermission_IsRejectedWithoutQuerying()
    {
        var query = new FakeStoreDirectoryQuery();
        var service = new StoreDirectoryService(query, new AlwaysDenyAccessControl());

        Assert.Throws<AccessDeniedException>(() => service.List());
        Assert.False(query.WasQueried);
    }

    [Fact]
    public void Get_WithUnknownStore_ThrowsNotFound()
    {
        var query = new FakeStoreDirectoryQuery();
        var service = new StoreDirectoryService(query, new AlwaysAllowAccessControl());

        Assert.Throws<StoreNotFoundException>(() => service.Get(99));
    }
}
