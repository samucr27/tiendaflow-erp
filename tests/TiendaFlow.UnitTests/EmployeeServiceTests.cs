using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class EmployeeServiceTests
{
    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly InMemoryStoreRepository _stores = new(
        new Store(1, "Tienda Centro", "Calle 1"),
        new Store(2, "Tienda Norte", "Calle 2"));
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5);

    private EmployeeService CreateService(IAccessControl? access = null) =>
        new(_employees, _stores, access ?? new AllowAllAccessControl(), _audit, _user);

    private EmployeeService CreateViewOnlyService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(
                userId: 5,
                permissions: new HashSet<string> { Permissions.HrEmployeesView }),
            _audit));

    private EmployeeService CreateDeniedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 5, permissions: new HashSet<string>()),
            _audit));

    private Employee AddEmployee(
        string idNumber = "100",
        string name = "Ana",
        string position = "Cajera",
        string contract = "Indefinido",
        decimal salary = 2_000_000m,
        bool active = true,
        int storeId = 1)
    {
        var employee = new Employee(0, storeId, idNumber, name, position, contract, salary, active);
        _employees.Add(employee);
        return employee;
    }

    private static CreateEmployeeRequest CreateRequest(
        string idNumber = "100",
        string name = "Ana",
        string position = "Cajera",
        int storeId = 1,
        string contract = "Indefinido",
        decimal salary = 2_000_000m) =>
        new(idNumber, name, position, storeId, contract, salary);

    private static UpdateEmployeeRequest UpdateRequest(
        string idNumber = "100",
        string name = "Ana",
        string position = "Cajera",
        int storeId = 1,
        string contract = "Indefinido",
        decimal salary = 2_000_000m,
        bool active = true) =>
        new(idNumber, name, position, storeId, contract, salary, active);

    [Fact]
    public void List_WithoutManagePermission_HidesTheSalary()
    {
        AddEmployee(salary: 3_000_000m);

        var row = Assert.Single(CreateViewOnlyService().List());

        Assert.Null(row.Salary);
        Assert.Equal("Tienda Centro", row.StoreName);
    }

    [Fact]
    public void Get_WithoutManagePermission_HidesTheSalary()
    {
        var employee = AddEmployee(salary: 3_000_000m);

        var row = CreateViewOnlyService().Get(employee.Id);

        Assert.Null(row.Salary);
    }

    [Fact]
    public void List_WithManagePermission_ShowsTheSalary()
    {
        AddEmployee(salary: 3_000_000m);

        var row = Assert.Single(CreateService().List());

        Assert.Equal(3_000_000m, row.Salary);
    }

    [Fact]
    public void List_OrdersEmployeesByName()
    {
        AddEmployee(idNumber: "1", name: "Zeta");
        AddEmployee(idNumber: "2", name: "Alfa");

        Assert.Equal(new[] { "Alfa", "Zeta" }, CreateService().List().Select(row => row.Name));
    }

    [Fact]
    public void Get_WithUnknownId_Throws()
    {
        Assert.Throws<EmployeeNotFoundException>(() => CreateService().Get(99));
    }

    [Fact]
    public void ListStores_ReturnsOnlyActiveStoresOrderedByName()
    {
        var service = new EmployeeService(
            _employees,
            new InMemoryStoreRepository(
                new Store(3, "Zeta", "Calle 3"),
                new Store(1, "Alfa", "Calle 1"),
                new Store(2, "Beta", "Calle 2", active: false)),
            new AllowAllAccessControl(),
            _audit,
            _user);

        Assert.Equal(new[] { "Alfa", "Zeta" }, service.ListStores().Select(store => store.Name));
    }

    [Fact]
    public void Create_TrimsTheTextAndStoresTheCanonicalContractLabel()
    {
        var id = CreateService().Create(CreateRequest(
            idNumber: " 100 ",
            name: " Ana ",
            position: " Cajera ",
            contract: "Termino fijo",
            salary: 1_500_000m));

        var employee = _employees.GetById(id)!;
        Assert.Equal("100", employee.IdNumber);
        Assert.Equal("Ana", employee.Name);
        Assert.Equal("Cajera", employee.Position);
        Assert.Equal("Término fijo", employee.ContractType);
        Assert.Equal(1_500_000m, employee.Salary);
        Assert.True(employee.Active);
    }

    [Fact]
    public void Create_AuditsWithoutPersonalDataOrAmounts()
    {
        CreateService().Create(CreateRequest(name: "Ana María", salary: 1_500_000m));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("EmployeeCreated", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.DoesNotContain("Ana", entry.Detail);
        Assert.DoesNotContain("1500000", entry.Detail);
        Assert.DoesNotContain("1.500.000", entry.Detail);
    }

    [Theory]
    [InlineData("", "Ana", "Cajera", 1, "Indefinido", 1_000_000)]
    [InlineData("100", " ", "Cajera", 1, "Indefinido", 1_000_000)]
    [InlineData("100", "Ana", "", 1, "Indefinido", 1_000_000)]
    [InlineData("100", "Ana", "Cajera", 0, "Indefinido", 1_000_000)]
    [InlineData("100", "Ana", "Cajera", 99, "Indefinido", 1_000_000)]
    [InlineData("100", "Ana", "Cajera", 1, "Contrato raro", 1_000_000)]
    [InlineData("100", "Ana", "Cajera", 1, "Indefinido", -1)]
    public void Create_WithInvalidData_ThrowsAndDoesNotSave(
        string idNumber,
        string name,
        string position,
        int storeId,
        string contract,
        int salary)
    {
        Assert.Throws<InvalidEmployeeException>(() => CreateService().Create(
            CreateRequest(idNumber, name, position, storeId, contract, salary)));

        Assert.Empty(_employees.List());
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Create_WithAnInactiveStore_Throws()
    {
        var service = new EmployeeService(
            _employees,
            new InMemoryStoreRepository(new Store(1, "Tienda Centro", "Calle 1", active: false)),
            new AllowAllAccessControl(),
            _audit,
            _user);

        Assert.Throws<InvalidEmployeeException>(() => service.Create(CreateRequest()));
        Assert.Empty(_employees.List());
    }

    [Fact]
    public void Create_WithADuplicateDocument_ThrowsAndDoesNotSave()
    {
        AddEmployee(idNumber: "100");

        Assert.Throws<DuplicateIdNumberException>(
            () => CreateService().Create(CreateRequest(idNumber: "100")));

        Assert.Single(_employees.List());
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Update_ChangesTheDataAndAuditsTheDeactivation()
    {
        var employee = AddEmployee();

        CreateService().Update(employee.Id, UpdateRequest(
            name: "Ana María",
            position: "Supervisora",
            contract: "Termino fijo",
            salary: 2_500_000m,
            active: false));

        Assert.Equal("Ana María", employee.Name);
        Assert.Equal("Supervisora", employee.Position);
        Assert.Equal("Término fijo", employee.ContractType);
        Assert.Equal(2_500_000m, employee.Salary);
        Assert.False(employee.Active);

        Assert.Equal(
            new[] { "EmployeeUpdated", "EmployeeDeactivated" },
            _audit.Entries.Select(entry => entry.Action));
        Assert.All(_audit.Entries, entry => Assert.Equal("hr", entry.Module));
        Assert.DoesNotContain("Ana", _audit.Entries[0].Detail);
        Assert.DoesNotContain("2.500.000", _audit.Entries[0].Detail);
    }

    [Fact]
    public void Update_CanReactivateAnEmployee()
    {
        var employee = AddEmployee(active: false);

        CreateService().Update(employee.Id, UpdateRequest(active: true));

        Assert.True(employee.Active);
        Assert.Equal(
            new[] { "EmployeeUpdated", "EmployeeActivated" },
            _audit.Entries.Select(entry => entry.Action));
    }

    [Fact]
    public void Update_KeepingTheSameDocument_IsAllowed()
    {
        var employee = AddEmployee(idNumber: "100");

        CreateService().Update(employee.Id, UpdateRequest(idNumber: "100", name: "Ana María"));

        Assert.Equal("Ana María", employee.Name);
    }

    [Fact]
    public void Update_WithADocumentUsedByAnotherEmployee_Throws()
    {
        var employee = AddEmployee(idNumber: "100");
        AddEmployee(idNumber: "200", name: "Beto");

        Assert.Throws<DuplicateIdNumberException>(
            () => CreateService().Update(employee.Id, UpdateRequest(idNumber: "200")));

        Assert.Equal("100", employee.IdNumber);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Update_WithUnknownId_Throws()
    {
        Assert.Throws<EmployeeNotFoundException>(
            () => CreateService().Update(99, UpdateRequest()));
    }

    [Fact]
    public void EveryOperation_RequiresItsPermissionAndAuditsTheDenial()
    {
        var service = CreateDeniedService();

        AssertDenied(Permissions.HrEmployeesView, () => service.List());
        AssertDenied(Permissions.HrEmployeesView, () => service.Get(1));
        AssertDenied(Permissions.HrEmployeesView, () => service.ListStores());
        AssertDenied(Permissions.HrEmployeesManage, () => service.Create(CreateRequest()));
        AssertDenied(Permissions.HrEmployeesManage, () => service.Update(1, UpdateRequest()));

        Assert.Equal(5, _audit.Entries.Count);
        Assert.All(_audit.Entries, entry =>
        {
            Assert.Equal("AccessDenied", entry.Action);
            Assert.Equal("security", entry.Module);
        });
    }

    private static void AssertDenied(string permission, Action action)
    {
        var exception = Assert.Throws<AccessDeniedException>(action);
        Assert.Equal(permission, exception.Permission);
    }
}
