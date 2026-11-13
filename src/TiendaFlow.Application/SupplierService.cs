using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record CreateSupplierRequest(
    string Nit,
    string BusinessName,
    string Address,
    decimal Rating = 100m);

public record UpdateSupplierRequest(
    string BusinessName,
    string Address,
    bool Active);

/// <summary>
/// Supplier registry, evaluation and ranking. Every operation demands its
/// purchasing permission so the page and the buttons are not the only guard.
/// </summary>
public sealed class SupplierService
{
    private readonly ISupplierRepository _suppliers;
    private readonly ISupplierQuery _query;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public SupplierService(
        ISupplierRepository suppliers,
        ISupplierQuery query,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _suppliers = suppliers;
        _query = query;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public IReadOnlyList<SupplierRow> List()
    {
        _accessControl.Demand(Permissions.PurchasingSuppliersView);
        return _query.List();
    }

    public IReadOnlyList<SupplierRow> Ranking()
    {
        _accessControl.Demand(Permissions.PurchasingReportsView);
        return _query.List()
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.BusinessName)
            .ToList();
    }

    /// <summary>
    /// CSV export of the ranking with the manual rating and the two objective
    /// indicators. CsvReport neutralizes cells that a spreadsheet could
    /// interpret as a formula.
    /// </summary>
    public string RankingCsv()
    {
        _accessControl.Demand(Permissions.PurchasingReportsView);

        var ranking = _query.List()
            .OrderByDescending(row => row.Rating)
            .ThenBy(row => row.BusinessName)
            .ToList();

        return CsvReport.Build(
            ["Posicion", "Proveedor", "NIT", "Calificacion", "Ordenes", "Comprado", "Puntualidad", "Cumplimiento", "Estado"],
            ranking.Select((row, index) => new[]
            {
                (index + 1).ToString(),
                row.BusinessName,
                row.Nit,
                row.RatingLabel,
                row.OrderCount.ToString(),
                CsvReport.Number(row.PurchasedTotal),
                row.PunctualityLabel,
                row.FulfillmentLabel,
                row.Active ? "Activo" : "Inactivo"
            }));
    }

    public SupplierRow Get(int id)
    {
        _accessControl.Demand(Permissions.PurchasingSuppliersView);
        return _query.GetById(id) ?? throw new SupplierNotFoundException(id);
    }

    public int Create(CreateSupplierRequest request)
    {
        _accessControl.Demand(Permissions.PurchasingSuppliersManage);

        var nit = request.Nit.Trim();
        if (_suppliers.GetByNit(nit) is not null)
            throw new DuplicateNitException(nit);

        var supplier = new Supplier(
            id: 0,
            nit,
            request.BusinessName,
            request.Address,
            request.Rating);

        _suppliers.Add(supplier);
        Record("SupplierCreated", supplier.Id, $"Proveedor {supplier.BusinessName} registrado.");

        return supplier.Id;
    }

    public void Update(int id, UpdateSupplierRequest request)
    {
        _accessControl.Demand(Permissions.PurchasingSuppliersManage);

        var supplier = FindOrThrow(id);
        supplier.UpdateDetails(request.BusinessName, request.Address);

        if (request.Active)
            supplier.Activate();
        else
            supplier.Deactivate();

        _suppliers.Update(supplier);
        Record(
            "SupplierUpdated",
            supplier.Id,
            $"Proveedor {supplier.BusinessName} actualizado ({(supplier.Active ? "activo" : "inactivo")}).");
    }

    public void Evaluate(int id, decimal rating)
    {
        _accessControl.Demand(Permissions.PurchasingEvaluationsManage);

        var supplier = FindOrThrow(id);
        supplier.ChangeRating(rating);
        _suppliers.Update(supplier);
        Record(
            "SupplierEvaluated",
            supplier.Id,
            $"Proveedor {supplier.BusinessName} evaluado con {supplier.Rating:0} %.");
    }

    private Supplier FindOrThrow(int id) =>
        _suppliers.GetById(id) ?? throw new SupplierNotFoundException(id);

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "purchasing",
            ip: _currentUser.Ip));
}
