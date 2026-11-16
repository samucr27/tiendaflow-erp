using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class CustomerServiceTests
{
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<ICustomerQuery> _query = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 6);

    private CustomerService CreateService(IAccessControl? access = null) =>
        new(_customers.Object, _query.Object, access ?? new AllowAllAccessControl(), _audit, _user);

    private CustomerService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 6, permissions: new HashSet<string>()),
            _audit));

    private static CustomerRow Row(int id) =>
        new(id, $"100{id}", "Ana Torres", "ana@example.com", "3001234567", "Calle 1", true);

    [Fact]
    public void List_ReturnsTheRowsOfTheQuery()
    {
        var rows = new[] { Row(1), Row(2) };
        _query.Setup(query => query.List()).Returns(rows);

        Assert.Equal(rows, CreateService().List());
    }

    [Fact]
    public void Get_ReturnsTheCustomer()
    {
        var row = Row(1);
        _query.Setup(query => query.GetById(1)).Returns(row);

        Assert.Equal(row, CreateService().Get(1));
    }

    [Fact]
    public void Get_WithUnknownId_Throws()
    {
        Assert.Throws<CustomerNotFoundException>(() => CreateService().Get(99));
    }

    [Fact]
    public void Create_RegistersTheCustomerAndAudits()
    {
        Customer? added = null;
        _customers.Setup(repository => repository.Add(It.IsAny<Customer>()))
            .Callback<Customer>(customer =>
            {
                TestIds.Assign(customer, 4);
                added = customer;
            });

        var id = CreateService().Create(
            new CreateCustomerRequest(" 123 ", "Ana Torres", " ana@example.com ", null, "Calle 1"));

        Assert.Equal(4, id);
        Assert.NotNull(added);
        Assert.Equal("123", added!.DocumentNumber);
        Assert.Equal("ana@example.com", added.Email);

        var entry = _audit.Entries.Single();
        Assert.Equal("CustomerCreated", entry.Action);
        Assert.Equal("crm", entry.Module);
        Assert.Equal(6, entry.UserId);
    }

    [Fact]
    public void Create_WithAnExistingDocument_ThrowsAndDoesNotSave()
    {
        _customers.Setup(repository => repository.GetByDocument("123"))
            .Returns(new Customer(1, "123", "Ana"));

        Assert.Throws<DuplicateCustomerDocumentException>(() => CreateService().Create(
            new CreateCustomerRequest("123", "Otra persona", null, null, null)));

        _customers.Verify(repository => repository.Add(It.IsAny<Customer>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Update_ChangesTheDataAndDeactivates()
    {
        var customer = new Customer(3, "123", "Ana");
        _customers.Setup(repository => repository.GetById(3)).Returns(customer);

        CreateService().Update(3, new UpdateCustomerRequest(
            "Ana María", "ana@example.com", "3001234567", "Calle 2", Active: false));

        Assert.Equal("Ana María", customer.FullName);
        Assert.False(customer.Active);
        _customers.Verify(repository => repository.Update(customer), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("CustomerUpdated", entry.Action);
        Assert.Contains("inactivo", entry.Detail);
    }

    [Fact]
    public void Update_CanReactivateACustomer()
    {
        var customer = new Customer(3, "123", "Ana", active: false);
        _customers.Setup(repository => repository.GetById(3)).Returns(customer);

        CreateService().Update(3, new UpdateCustomerRequest(
            "Ana", null, null, null, Active: true));

        Assert.True(customer.Active);
        Assert.Contains("(activo)", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void Update_WithUnknownId_Throws()
    {
        Assert.Throws<CustomerNotFoundException>(() => CreateService().Update(
            99, new UpdateCustomerRequest("Ana", null, null, null, true)));
    }

    [Fact]
    public void List_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().List());

        Assert.Equal(Permissions.CrmCustomersView, exception.Permission);
    }

    [Fact]
    public void Create_WithoutPermission_ThrowsAndDoesNotSave()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().Create(
            new CreateCustomerRequest("123", "Ana", null, null, null)));

        Assert.Equal(Permissions.CrmCustomersManage, exception.Permission);
        _customers.Verify(repository => repository.Add(It.IsAny<Customer>()), Times.Never);
    }
}
