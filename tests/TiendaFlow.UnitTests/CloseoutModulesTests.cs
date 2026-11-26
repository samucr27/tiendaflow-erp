using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Crm;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>In-memory IEntityStore that assigns ids the way EF Core does.</summary>
internal sealed class InMemoryEntityStore<T> : IEntityStore<T> where T : class
{
    private readonly List<T> _items = new();

    public T? Get(int id) => _items.FirstOrDefault(item => IdOf(item) == id);

    public IReadOnlyList<T> All() => _items.ToList();

    public void Add(T entity)
    {
        TestIds.Assign(entity, _items.Count == 0 ? 1 : _items.Max(IdOf) + 1);
        _items.Add(entity);
    }

    public void Update(T entity)
    {
    }

    public void Remove(T entity) => _items.Remove(entity);

    private static int IdOf(T entity) => (int)typeof(T).GetProperty("Id")!.GetValue(entity)!;
}

internal sealed class InMemoryRolePermissionStore : IRolePermissionStore
{
    private readonly Dictionary<int, IReadOnlySet<string>> _stored = new();

    public IReadOnlySet<string>? StoredFor(int roleId) => _stored.GetValueOrDefault(roleId);

    public void Replace(int roleId, IReadOnlySet<string> permissions) => _stored[roleId] = permissions;

    public void Clear(int roleId) => _stored.Remove(roleId);
}

public class RoleEditorServiceTests
{
    private readonly InMemoryRolePermissionStore _store = new();
    private readonly RecordingAuditLog _audit = new();

    private RoleEditorService Service(IAccessControl? access = null) =>
        new(_store, NoOpUnitOfWork.Instance, access ?? new AllowAllAccessControl(), _audit, new FakeCurrentUser());

    [Fact]
    public void Update_StoresThePermissionsAndTheMatrixAndSignInUseThem()
    {
        var cashier = RolePermissions.ForRole(RoleNames.Cashier).ToHashSet(StringComparer.Ordinal);
        cashier.Add(Permissions.InventoryAlertsView);

        Service().Update(new RolePermissionChange(RoleNames.Cashier, cashier));

        Assert.Contains(Permissions.InventoryAlertsView, EffectivePermissions.For(RoleNames.Cashier, _store));
        var matrix = new RoleCatalogService(new AllowAllAccessControl(), _store).GetMatrix();
        Assert.True(matrix.Rows.Single(row => row.Permission == Permissions.InventoryAlertsView)
            .AllowedByRole[RoleNames.Cashier]);
        Assert.Equal("RolePermissionsUpdated", _audit.Entries.Single().Action);
        Assert.Contains("inventory.alerts.view", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void Update_CannotRemoveUserOrRoleManagementFromTheAdministrator()
    {
        var admin = RolePermissions.ForRole(RoleNames.Administrator).ToHashSet(StringComparer.Ordinal);
        admin.Remove(Permissions.SecurityRolesManage);

        Assert.Throws<InvalidUserException>(() =>
            Service().Update(new RolePermissionChange(RoleNames.Administrator, admin)));
        Assert.Null(_store.StoredFor(SystemRoles.IdFor(RoleNames.Administrator)));
    }

    [Fact]
    public void Update_RejectsUnknownPermissionsAndEmptyRoles()
    {
        Assert.Throws<InvalidUserException>(() => Service().Update(new RolePermissionChange(
            RoleNames.Cashier, new HashSet<string> { "inventario.todo" })));
        Assert.Throws<InvalidUserException>(() => Service().Update(new RolePermissionChange(
            RoleNames.Cashier, new HashSet<string>())));
    }

    [Fact]
    public void Reset_ReturnsTheRoleToTheDefaultMatrix()
    {
        Service().Update(new RolePermissionChange(RoleNames.Cashier, new HashSet<string> { Permissions.SalesPosUse }));
        Service().Reset(RoleNames.Cashier);

        Assert.Equal(RolePermissions.ForRole(RoleNames.Cashier), EffectivePermissions.For(RoleNames.Cashier, _store));
    }

    [Fact]
    public void Update_WithoutTheManagePermission_IsRejected()
    {
        Assert.Throws<AccessDeniedException>(() => Service(new AlwaysDenyAccessControl()).Update(
            new RolePermissionChange(RoleNames.Cashier, new HashSet<string> { Permissions.SalesPosUse })));
    }
}

public class HumanResourcesRecordsServiceTests
{
    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly InMemoryEntityStore<EmploymentContract> _contracts = new();
    private readonly InMemoryEntityStore<AttendanceRecord> _attendance = new();
    private readonly InMemoryEntityStore<WorkShift> _shifts = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly Employee _employee;

    public HumanResourcesRecordsServiceTests()
    {
        _employee = new Employee(0, 1, "1010", "Ana Torres", "Cajera", "Indefinido", 1_750_905m);
        _employees.Add(_employee);
    }

    private HumanResourcesRecordsService Service(IAccessControl? access = null) =>
        new(_employees, _contracts, _attendance, _shifts, NoOpUnitOfWork.Instance,
            access ?? new AllowAllAccessControl(), _audit, new FakeCurrentUser());

    [Fact]
    public void AddContract_UpdatesTheEmployeeTypeAndSalaryForPayroll()
    {
        Service().AddContract(new NewContractRequest(_employee.Id, ContractType.FixedTerm,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 2_100_000m, null));

        Assert.Equal("Término fijo", _employee.ContractType);
        Assert.Equal(2_100_000m, _employee.Salary);
        Assert.Equal("ContractCreated", _audit.Entries.Single().Action);
        Assert.DoesNotContain("Ana", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void AddContract_RejectsASecondActiveContractAndAFixedTermWithoutEnd()
    {
        Service().AddContract(new NewContractRequest(_employee.Id, ContractType.Indefinite,
            new DateOnly(2026, 1, 1), null, 2_000_000m, null));

        Assert.Throws<InvalidHrRecordException>(() => Service().AddContract(new NewContractRequest(
            _employee.Id, ContractType.Indefinite, new DateOnly(2026, 2, 1), null, 2_000_000m, null)));
        Assert.Throws<InvalidHrRecordException>(() => new EmploymentContract(
            _employee.Id, ContractType.FixedTerm, new DateOnly(2026, 1, 1), null, 1m));
    }

    [Fact]
    public void Contract_ExtendsAndEndsWithItsRules()
    {
        var contract = new EmploymentContract(1, ContractType.FixedTerm, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), 1m);

        contract.Extend(new DateOnly(2026, 12, 31));
        Assert.Equal(1, contract.Extensions);
        Assert.Throws<InvalidHrRecordException>(() => contract.Extend(new DateOnly(2026, 7, 1)));

        contract.End(new DateOnly(2026, 8, 15));
        Assert.Equal(EmploymentContractStatus.Ended, contract.Status);
        Assert.False(contract.IsActiveOn(new DateOnly(2026, 8, 20)));
        Assert.Throws<InvalidHrRecordException>(() => contract.End(new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void ExpiringContracts_ListsTheActiveOnesEndingSoon()
    {
        Service().AddContract(new NewContractRequest(_employee.Id, ContractType.FixedTerm,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 30), 1m, null));

        var expiring = Service().ExpiringContracts(new DateOnly(2026, 11, 16), 30);

        Assert.Single(expiring);
    }

    [Fact]
    public void RecordAttendance_AllowsOneRecordPerDayAndComputesHours()
    {
        var day = new DateOnly(2026, 11, 10);
        Service().RecordAttendance(new AttendanceRequest(_employee.Id, day, AttendanceType.Worked,
            new TimeOnly(7, 0), new TimeOnly(15, 30), null));

        Assert.Throws<InvalidHrRecordException>(() => Service().RecordAttendance(new AttendanceRequest(
            _employee.Id, day, AttendanceType.Absence, null, null, null)));
        Assert.Equal(8.5m, Service().Attendance(day, day).Single().HoursWorked);
        Assert.Throws<InvalidHrRecordException>(() => new AttendanceRecord(
            1, day, AttendanceType.Worked, new TimeOnly(15, 0), new TimeOnly(7, 0)));
    }

    [Fact]
    public void UnpaidAbsences_ReduceTheDaysOfThePayrollRun()
    {
        Service().RecordAttendance(new AttendanceRequest(_employee.Id, new DateOnly(2026, 11, 3), AttendanceType.Absence, null, null, null));
        Service().RecordAttendance(new AttendanceRequest(_employee.Id, new DateOnly(2026, 11, 4), AttendanceType.Absence, null, null, null));
        Service().RecordAttendance(new AttendanceRequest(_employee.Id, new DateOnly(2026, 11, 5), AttendanceType.SickLeave, null, null, null));

        var absences = Service().UnpaidAbsences(2026, 11);
        Assert.Equal(2, absences[_employee.Id]);

        var engine = new Mock<IPayrollEngine>();
        engine.Setup(item => item.Calculate(It.IsAny<PayrollInput>()))
            .Returns<PayrollInput>(input => new PayrollResult
            {
                EmployeeId = input.EmployeeId,
                ContractType = input.ContractType,
                DaysWorked = input.DaysWorked,
                GrossEarnings = input.DaysWorked * 1000m
            });
        var run = new PayrollRunService(_employees, engine.Object, new AllowAllAccessControl(), _audit, new FakeCurrentUser())
            .Calculate(30, 1, absences);

        Assert.Equal(28, run.Lines.Single().DaysApplied);
        engine.Verify(item => item.Calculate(It.Is<PayrollInput>(input => input.DaysWorked == 28)), Times.Once);
    }

    [Fact]
    public void AssignShift_RejectsOverlappingShiftsOfTheSameEmployee()
    {
        var day = new DateOnly(2026, 11, 17);
        Service().AssignShift(new ShiftRequest(_employee.Id, day, new TimeOnly(6, 0), new TimeOnly(14, 0)));

        Assert.Throws<InvalidHrRecordException>(() => Service().AssignShift(
            new ShiftRequest(_employee.Id, day, new TimeOnly(13, 0), new TimeOnly(21, 0))));
        Service().AssignShift(new ShiftRequest(_employee.Id, day, new TimeOnly(14, 0), new TimeOnly(22, 0)));

        Assert.Equal(2, Service().Shifts(day, day).Count);
        Assert.Equal(16m, Service().Shifts(day, day).Sum(row => row.Hours));
    }

    [Fact]
    public void EveryOperation_DemandsItsHrPermission()
    {
        var denied = Service(new AlwaysDenyAccessControl());

        Assert.Throws<AccessDeniedException>(() => denied.Contracts());
        Assert.Throws<AccessDeniedException>(() => denied.Attendance(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        Assert.Throws<AccessDeniedException>(() => denied.Shifts(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
    }
}

public class CrmRecordsTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryEntityStore<LoyaltyPointsEntry> _ledger = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<ICustomerQuery> _customerQuery = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly Customer _customer = new(7, "1020304050", "Camila Suárez");

    public CrmRecordsTests()
    {
        _customers.Setup(repository => repository.GetByDocument("1020304050")).Returns(_customer);
        _customers.Setup(repository => repository.GetById(7)).Returns(_customer);
        _customerQuery.Setup(query => query.List()).Returns(new[]
        {
            new CustomerRow(7, "1020304050", "Camila Suárez", null, null, null, true)
        });
    }

    private LoyaltyService Loyalty() =>
        new(_ledger, _customers.Object, _customerQuery.Object, new AllowAllAccessControl(), _audit,
            new FakeCurrentUser(), new FixedClock(Now));

    private static SalesInvoice Invoice(string document, decimal price)
    {
        var invoice = new SalesInvoice(0, "Cliente", document, Now, storeId: 1);
        TestIds.Assign(invoice, 40);
        invoice.AddLine("SKU-1", "Producto", 1, price);
        return invoice;
    }

    [Fact]
    public void Loyalty_EarnsOnePointPerThousandAndReversesOnCancel()
    {
        var invoice = Invoice("1020304050", 40_500m);

        Loyalty().EarnForInvoice(invoice);
        Assert.Equal(40, Loyalty().Balances().Single().Balance);

        Loyalty().ReverseForInvoice(invoice);
        Assert.Equal(0, Loyalty().Balances().Single().Balance);
        Assert.Equal(2, Loyalty().Movements(7).Count);
    }

    [Fact]
    public void Loyalty_IgnoresInvoicesOfUnregisteredCustomers()
    {
        Loyalty().EarnForInvoice(Invoice("999", 50_000m));

        Assert.Empty(_ledger.All());
    }

    [Fact]
    public void Redeem_CannotExceedTheBalance()
    {
        Loyalty().EarnForInvoice(Invoice("1020304050", 30_000m));

        Assert.Throws<InvalidCrmRecordException>(() => Loyalty().Redeem(7, 31, "Canje"));
        Loyalty().Redeem(7, 10, "Canje");

        Assert.Equal(20, Loyalty().Balances().Single().Balance);
        Assert.Equal("LoyaltyPointsRedeemed", _audit.Entries.Single().Action);
    }

    [Fact]
    public void InvoiceService_EarnsAndReversesPointsThroughTheLoyaltyProgram()
    {
        var stock = new Mock<IStockRepository>();
        stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(new StockItem("SKU-1", 1, 10, "Pasillo"));
        var invoices = new Mock<IInvoiceRepository>();
        SalesInvoice? saved = null;
        invoices.Setup(repository => repository.Add(It.IsAny<SalesInvoice>()))
            .Callback<SalesInvoice>(invoice => { TestIds.Assign(invoice, 50); saved = invoice; });
        invoices.Setup(repository => repository.GetById(50)).Returns(() => saved);

        var service = new SalesInvoiceService(invoices.Object, new Mock<IInvoiceQuery>().Object, stock.Object,
            NoOpUnitOfWork.Instance, new AllowAllAccessControl(), _audit, new FakeCurrentUser(), Loyalty());

        service.Create(new CreateInvoiceRequest("Camila", "1020304050", Now,
            new[] { new InvoiceLineRequest("SKU-1", "Producto", 2, 6_000m) }));
        Assert.Equal(12, Loyalty().Balances().Single().Balance);

        service.Cancel(50);
        Assert.Equal(0, Loyalty().Balances().Single().Balance);
    }

    [Fact]
    public void Pqrs_GetsADueDateAndClosesWithAResponse()
    {
        var store = new InMemoryEntityStore<PqrsCase>();
        var service = new PqrsService(store, _customerQuery.Object, new AllowAllAccessControl(), _audit,
            new FakeCurrentUser(), new FixedClock(Now));

        var id = service.File(new NewPqrsRequest(PqrsType.Complaint, "Producto vencido", "Detalle", 7, null));
        var detail = service.Get(id);
        Assert.Equal(new DateOnly(2026, 12, 1), detail.Row.DueDate);
        Assert.Equal("Camila Suárez", detail.Row.Requester);
        Assert.StartsWith("PQRS-2026-", detail.Row.FilingNumber);

        Assert.Throws<InvalidCrmRecordException>(() => service.Close(id, " "));
        service.Assign(id, "Jefe de tienda");
        service.Close(id, "Se retiró el producto y se ofreció cambio.");

        Assert.Equal(PqrsStatus.Closed, service.Get(id).Row.Status);
        Assert.Throws<InvalidCrmRecordException>(() => service.Assign(id, "Otro"));
    }

    [Fact]
    public void Pqrs_IsOverdueAfterItsDueDate()
    {
        var item = new PqrsCase(PqrsType.Claim, "Cobro doble", "Detalle", null, "300", Now);

        Assert.False(item.IsOverdue(new DateOnly(2026, 12, 1)));
        Assert.True(item.IsOverdue(new DateOnly(2026, 12, 2)));
        Assert.Throws<InvalidCrmRecordException>(() => new PqrsCase(PqrsType.Claim, "x", "y", null, null, Now));
    }

    [Fact]
    public void Campaign_ValidatesItsSegmentAndDates()
    {
        Assert.Throws<InvalidCrmRecordException>(() => new MarketingCampaign("x", "D", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "m"));
        Assert.Throws<InvalidCrmRecordException>(() => new MarketingCampaign("x", "A", new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1), "m"));

        var campaign = new MarketingCampaign("Doble puntos", "A", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "Mensaje");
        campaign.Cancel();
        Assert.True(campaign.Cancelled);
    }

    [Fact]
    public void CampaignService_MeasuresTheInvoicesOfTheSegmentInThePeriod()
    {
        var invoiceQuery = new Mock<IInvoiceQuery>();
        invoiceQuery.Setup(query => query.List()).Returns(new[]
        {
            new InvoiceRow(1, "Camila", "1020304050", Now.AddDays(-3), InvoiceStatus.Issued, 1, 40_000m),
            new InvoiceRow(2, "Camila", "1020304050", Now.AddDays(-60), InvoiceStatus.Issued, 1, 10_000m)
        });
        var segmentation = new CustomerSegmentationService(_customerQuery.Object, invoiceQuery.Object, new AllowAllAccessControl());
        var store = new InMemoryEntityStore<MarketingCampaign>();
        var service = new CampaignService(store, segmentation, invoiceQuery.Object, new AllowAllAccessControl(), _audit, new FakeCurrentUser());

        service.Create(new NewCampaignRequest("Doble puntos", "A", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "Mensaje"));
        var row = service.List().Single();

        Assert.Equal(1, row.TargetCustomers);
        Assert.Equal(1, row.InvoicesInPeriod);
        Assert.Equal(40_000m, row.SalesInPeriod);
    }
}

public class SalesReturnServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly InMemoryEntityStore<SalesReturn> _returns = new();
    private readonly Mock<IStockRepository> _stock = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly StockItem _item = new("SKU-1", 1, 5, "Pasillo 2");
    private readonly SalesInvoice _invoice;

    public SalesReturnServiceTests()
    {
        _invoice = new SalesInvoice(0, "Cliente", "123", Now, storeId: 1);
        TestIds.Assign(_invoice, 9);
        _invoice.AddLine("SKU-1", "Azúcar 1 kg", 4, 4_600m);
        _invoices.Setup(repository => repository.GetById(9)).Returns(_invoice);
        _stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(_item);
    }

    private SalesReturnService Service(IAccessControl? access = null) =>
        new(_invoices.Object, _returns, _stock.Object, NoOpUnitOfWork.Instance,
            access ?? new AllowAllAccessControl(), _audit, new FakeCurrentUser(), new FixedClock(Now));

    [Fact]
    public void Register_ReturnsUnitsToTheStockAndLimitsToWhatWasInvoiced()
    {
        Service().Register(9, "Producto en mal estado", new[] { new SalesReturnLineRequest("SKU-1", 3) });

        Assert.Equal(8, _item.QuantityAvailable);
        Assert.Equal(MovementType.Return, _item.LastMovement!.Type);
        Assert.Equal(13_800m, _returns.All().Single().Total);
        Assert.Equal(1, Service().Returnable(9).Single().Returnable);

        var error = Assert.Throws<InvalidSalesReturnException>(() =>
            Service().Register(9, "Otra", new[] { new SalesReturnLineRequest("SKU-1", 2) }));
        Assert.Contains("only 1 units", error.Message);
    }

    [Fact]
    public void Register_RejectsCancelledInvoicesAndEmptyReturns()
    {
        Assert.Throws<InvalidSalesReturnException>(() =>
            Service().Register(9, "Motivo", new[] { new SalesReturnLineRequest("SKU-1", 0) }));

        _invoice.Cancel();
        Assert.Throws<InvalidSalesReturnException>(() =>
            Service().Register(9, "Motivo", new[] { new SalesReturnLineRequest("SKU-1", 1) }));
    }

    [Fact]
    public void Register_RequiresTheReturnsPermission()
    {
        Assert.Throws<AccessDeniedException>(() => Service(new AlwaysDenyAccessControl())
            .Register(9, "Motivo", new[] { new SalesReturnLineRequest("SKU-1", 1) }));
    }

    [Fact]
    public void SalesReport_SubtractsTheReturnsFromTheNetTotal()
    {
        Service().Register(9, "Error en la venta", new[] { new SalesReturnLineRequest("SKU-1", 1) });
        var invoiceQuery = new Mock<IInvoiceQuery>();
        invoiceQuery.Setup(query => query.List()).Returns(new[]
        {
            new InvoiceRow(9, "Cliente", "123", Now, InvoiceStatus.Issued, 1, 18_400m)
        });

        var report = new SalesReportService(invoiceQuery.Object, new AllowAllAccessControl(), null, _returns)
            .Get(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 16));

        Assert.Equal(4_600m, report.ReturnsTotal);
        Assert.Equal(13_800m, report.NetTotal);
    }
}

public class PosCartTests
{
    [Fact]
    public void RegisterCart_SellsSeveralProductsInOneDocument()
    {
        var products = new InMemoryProductRepository();
        products.Add(new Product("SKU-1", "7701", "Agua", 2_500m));
        products.Add(new Product("SKU-2", "7702", "Pan", 5_500m));
        var water = new StockItem("SKU-1", 1, 10, "Nevera", 2);
        var bread = new StockItem("SKU-2", 1, 3, "Pasillo", 2);
        var stock = new Mock<IStockRepository>();
        stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(water);
        stock.Setup(repository => repository.Get(1, "SKU-2")).Returns(bread);
        var saved = new List<PosSale>();
        var sales = new Mock<IPosSaleRepository>();
        sales.Setup(repository => repository.Add(It.IsAny<PosSale>())).Callback<PosSale>(saved.Add);

        var service = new PosSaleService(stock.Object, products, sales.Object, NoOpUnitOfWork.Instance,
            new AllowAllAccessControl(), new RecordingAuditLog(), new FakeCurrentUser(),
            new FixedClock(new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc)));

        var receipt = service.RegisterCart(1, new[]
        {
            new PosCartLine("SKU-1", 2), new PosCartLine("SKU-2", 1), new PosCartLine("sku-1", 1)
        });

        Assert.Equal(2, receipt.Lines.Count);
        Assert.Equal(13_000m, receipt.Total);
        Assert.Equal(7, water.QuantityAvailable);
        Assert.Equal(2, bread.QuantityAvailable);
        Assert.Equal("SKU-2", Assert.Single(receipt.CriticalLines).ProductSku);
        Assert.Equal(2, Assert.Single(saved).Lines.Count);
        Assert.Throws<InvalidPosSaleException>(() => service.RegisterCart(1, Array.Empty<PosCartLine>()));
    }
}

public class SecurityReportServiceTests
{
    [Fact]
    public void Get_CountsTheSecurityEventsOfTheRange()
    {
        var day = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
        var audit = new Mock<IAuditTrailQuery>();
        audit.Setup(query => query.Between(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[]
        {
            new AuditTrailRow(1, "LoginSucceeded", "security", "1", "ok", 1, "Admin", null, day),
            new AuditTrailRow(2, "AccessDenied", "security", "security.users.view", "Intento", 4, "Ana", null, day),
            new AuditTrailRow(3, "AccessDenied", "security", "hr.payroll.manage", "Intento", 4, "Ana", null, day)
        });

        var report = new SecurityReportService(audit.Object, new AllowAllAccessControl())
            .Get(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 16));

        Assert.Equal(1, report.Count("LoginSucceeded"));
        Assert.Equal(2, report.Count("AccessDenied"));
        Assert.Equal(2, report.DeniedAccesses.Count);
        Assert.Contains("Accesos denegados", report.ToCsv());
    }
}

public class PayrollHistoryServiceTests
{
    [Fact]
    public void Save_KeepsOneRunPerMonthWithTheCalculatedEmployees()
    {
        var store = new InMemoryEntityStore<PayrollRunRecord>();
        var service = new PayrollHistoryService(store, new AllowAllAccessControl(), new RecordingAuditLog(),
            new FakeCurrentUser(), new FixedClock(new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc)));
        var run = new PayrollRun(30, 1, new[]
        {
            new PayrollRunLine(1, "Ana", "Cajera", "Indefinido",
                new PayrollResult { GrossEarnings = 2_099_095m, EmployeeHealth = 74_000m, EmployeePension = 74_000m }, null, 30),
            new PayrollRunLine(2, "Sin salario", "Cajero", "Indefinido", null, "Sin salario")
        });

        service.Save(run, 2026, 11);

        var saved = Assert.Single(service.List());
        Assert.Equal("2026-11", saved.Period);
        Assert.Equal(1, saved.EmployeeCount);
        Assert.Equal(1_951_095m, saved.TotalNet);
        Assert.Throws<InvalidHrRecordException>(() => service.Save(run, 2026, 11));
    }
}
