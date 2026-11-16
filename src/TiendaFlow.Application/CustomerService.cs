using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record CreateCustomerRequest(
    string DocumentNumber,
    string FullName,
    string? Email,
    string? Phone,
    string? Address);

public record UpdateCustomerRequest(
    string FullName,
    string? Email,
    string? Phone,
    string? Address,
    bool Active);

/// <summary>
/// Customer registry for the CRM. Every operation demands its CRM permission
/// so the page and its buttons are not the only guard.
/// </summary>
public sealed class CustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerQuery _query;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public CustomerService(
        ICustomerRepository customers,
        ICustomerQuery query,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _customers = customers;
        _query = query;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public IReadOnlyList<CustomerRow> List()
    {
        _accessControl.Demand(Permissions.CrmCustomersView);
        return _query.List();
    }

    public CustomerRow Get(int id)
    {
        _accessControl.Demand(Permissions.CrmCustomersView);
        return _query.GetById(id) ?? throw new CustomerNotFoundException(id);
    }

    public int Create(CreateCustomerRequest request)
    {
        _accessControl.Demand(Permissions.CrmCustomersManage);

        var document = request.DocumentNumber.Trim();
        if (_customers.GetByDocument(document) is not null)
            throw new DuplicateCustomerDocumentException(document);

        var customer = new Customer(
            id: 0,
            document,
            request.FullName,
            request.Email,
            request.Phone,
            request.Address);

        _customers.Add(customer);
        Record("CustomerCreated", customer.Id, $"Cliente {customer.FullName} registrado.");
        return customer.Id;
    }

    public void Update(int id, UpdateCustomerRequest request)
    {
        _accessControl.Demand(Permissions.CrmCustomersManage);

        var customer = _customers.GetById(id) ?? throw new CustomerNotFoundException(id);

        customer.UpdateDetails(request.FullName, request.Email, request.Phone, request.Address);

        if (request.Active)
            customer.Activate();
        else
            customer.Deactivate();

        _customers.Update(customer);
        Record(
            "CustomerUpdated",
            customer.Id,
            $"Cliente {customer.FullName} actualizado ({(customer.Active ? "activo" : "inactivo")}).");
    }

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "crm",
            ip: _currentUser.Ip));
}
