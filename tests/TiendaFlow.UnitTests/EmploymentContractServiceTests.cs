using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class EmploymentContractServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 16);

    private readonly InMemoryEmploymentContractRepository _contracts = new();
    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly InMemoryEmploymentContractQuery _query;
    private readonly StubParameterValues _parameters = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.9");
    private readonly RecordingUnitOfWork _unitOfWork = new();

    public EmploymentContractServiceTests() => _query = new InMemoryEmploymentContractQuery(_contracts, _employees);

    private EmploymentContractService CreateService(IAccessControl? access = null) =>
        new(
            _contracts,
            _query,
            _employees,
            _parameters,
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            _user,
            new FixedClock(Now));

    private EmploymentContractService CreateDeniedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 5, permissions: new HashSet<string>()),
            _audit));

    private Employee AddEmployee(
        string idNumber = "100",
        string name = "Ana",
        bool active = true,
        decimal salary = 2_000_000m)
    {
        var employee = new Employee(
            0,
            1,
            idNumber,
            name,
            "Cajera",
            "Indefinido",
            salary,
            active);

        _employees.Add(employee);
        return employee;
    }

    private EmploymentContract AddContract(
        int employeeId,
        ContractType type = ContractType.Indefinite,
        DateOnly? start = null,
        DateOnly? end = null,
        decimal salary = 2_000_000m)
    {
        var contract = new EmploymentContract(
            0,
            employeeId,
            type,
            start ?? new DateOnly(2026, 1, 1),
            end,
            salary,
            Now);

        _contracts.Add(contract);
        return contract;
    }

    [Fact]
    public void List_ReturnsTheRegisteredContracts()
    {
        var employee = AddEmployee();
        AddContract(employee.Id);

        var row = Assert.Single(CreateService().List());

        Assert.Equal("Ana", row.EmployeeName);
        Assert.Equal("Vigente", row.StatusLabel);
        Assert.Equal("Indefinido", row.ContractTypeLabel);
    }

    [Fact]
    public void Get_WithUnknownId_Throws()
    {
        Assert.Throws<EmploymentContractNotFoundException>(() => CreateService().Get(99));
    }

    [Fact]
    public void Employees_MarkWhoAlreadyHasAnActiveContract()
    {
        var withContract = AddEmployee(idNumber: "100", name: "Ana");
        AddEmployee(idNumber: "200", name: "Beto");
        AddContract(withContract.Id);

        var options = CreateService().Employees();

        Assert.True(options.Single(option => option.IdNumber == "100").HasActiveContract);
        Assert.False(options.Single(option => option.IdNumber == "200").HasActiveContract);
    }

    [Fact]
    public void Create_RegistersTheContractForAnActiveEmployeeAndAudits()
    {
        var employee = AddEmployee(salary: 2_500_000m);

        var id = CreateService().Create(new CreateContractRequest(
            employee.Id,
            ContractType.FixedTerm,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            2_500_000m));

        var contract = _contracts.GetById(id)!;
        Assert.Equal(employee.Id, contract.EmployeeId);
        Assert.Equal(ContractType.FixedTerm, contract.ContractType);
        Assert.Equal(EmploymentContractStatus.Active, contract.Status);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("ContractCreated", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.Equal("10.0.0.9", entry.Ip);
        Assert.DoesNotContain("2.500.000", entry.Detail);
    }

    [Fact]
    public void Create_RejectsAnEmployeeWithAnActiveContract()
    {
        var employee = AddEmployee();
        AddContract(employee.Id);

        Assert.Throws<InvalidEmploymentContractException>(() => CreateService().Create(
            new CreateContractRequest(
                employee.Id,
                ContractType.FixedTerm,
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 7, 31),
                2_000_000m)));

        Assert.Single(_contracts.All());
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Create_RejectsAnInactiveOrUnknownEmployee()
    {
        var inactive = AddEmployee(idNumber: "100", active: false);

        Assert.Throws<InvalidEmploymentContractException>(() => CreateService().Create(
            new CreateContractRequest(
                inactive.Id,
                ContractType.Indefinite,
                new DateOnly(2026, 1, 1),
                null,
                2_000_000m)));

        Assert.Throws<InvalidEmploymentContractException>(() => CreateService().Create(
            new CreateContractRequest(
                999,
                ContractType.Indefinite,
                new DateOnly(2026, 1, 1),
                null,
                2_000_000m)));

        Assert.Empty(_contracts.All());
    }

    [Fact]
    public void Renew_ExtendsThePreviousContractAndCreatesTheNewOneAtomically()
    {
        var employee = AddEmployee();
        var current = AddContract(
            employee.Id,
            ContractType.FixedTerm,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        TestIds.Assign(current, 4);

        var id = CreateService().Renew(current.Id, new RenewContractRequest(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 12, 31),
            2_400_000m));

        var renewed = _contracts.GetById(id)!;
        Assert.Equal(EmploymentContractStatus.Extended, current.Status);
        Assert.Equal(EmploymentContractStatus.Active, renewed.Status);
        Assert.Equal(current.Id, renewed.PreviousContractId);
        Assert.Equal(1, _unitOfWork.Executions);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("ContractRenewed", entry.Action);
        Assert.Equal("hr", entry.Module);
    }

    [Fact]
    public void Renew_WithAnUnknownContract_ThrowsWithoutTransaction()
    {
        Assert.Throws<EmploymentContractNotFoundException>(() =>
            CreateService().Renew(99, new RenewContractRequest(
                new DateOnly(2027, 1, 1),
                null,
                2_000_000m)));

        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Terminate_LocksTheContractAndAudits()
    {
        var employee = AddEmployee();
        var contract = AddContract(employee.Id);

        CreateService().Terminate(contract.Id);

        Assert.Equal(EmploymentContractStatus.Terminated, contract.Status);
        Assert.NotNull(contract.TerminatedAt);
        Assert.Equal("ContractTerminated", Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public void ExpiringAlerts_UseTheConfigurableWindowAndOnlyActiveContracts()
    {
        var employee = AddEmployee();
        AddContract(
            employee.Id,
            ContractType.FixedTerm,
            Today.AddMonths(-6),
            Today.AddDays(10));
        AddContract(
            employee.Id,
            ContractType.FixedTerm,
            Today.AddMonths(-6),
            Today.AddDays(60));
        _parameters.Integers[SystemParameterCatalog.ContractExpiryAlertDays] = 30;

        var alert = Assert.Single(CreateService().ExpiringAlerts());

        Assert.Equal(Today.AddDays(10), alert.EndDate);
    }

    [Fact]
    public void Attach_StoresTheFileAndAuditsTheUploader()
    {
        var employee = AddEmployee();
        var contract = AddContract(employee.Id);

        var id = CreateService().Attach(contract.Id, new ContractAttachmentUpload(
            "contrato.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3, 4 }));

        var attachment = _contracts.GetAttachment(id)!;
        Assert.Equal(contract.Id, attachment.ContractId);
        Assert.Equal(5, attachment.UploadedByUserId);
        Assert.Equal(4, attachment.SizeBytes);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("ContractAttachmentUploaded", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.DoesNotContain("application/pdf", entry.Detail);
    }

    [Fact]
    public void Attach_WithAnUnknownContract_Throws()
    {
        Assert.Throws<EmploymentContractNotFoundException>(() =>
            CreateService().Attach(99, new ContractAttachmentUpload(
                "contrato.pdf",
                "application/pdf",
                new byte[] { 1 })));

        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void DownloadAttachment_ReturnsTheBytesAndAudits()
    {
        var employee = AddEmployee();
        var contract = AddContract(employee.Id);
        var id = CreateService().Attach(contract.Id, new ContractAttachmentUpload(
            "contrato.pdf",
            "application/pdf",
            new byte[] { 9, 8, 7 }));
        _audit.Entries.Clear();

        var download = CreateService().DownloadAttachment(contract.Id, id);

        Assert.Equal("contrato.pdf", download.FileName);
        Assert.Equal(new byte[] { 9, 8, 7 }, download.Content);
        Assert.Equal("ContractAttachmentDownloaded", Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public void DownloadAttachment_RejectsAFileFromAnotherContract()
    {
        var employee = AddEmployee();
        var contract = AddContract(employee.Id);
        var other = AddContract(employee.Id, start: new DateOnly(2025, 1, 1));
        var id = CreateService().Attach(contract.Id, new ContractAttachmentUpload(
            "contrato.pdf",
            "application/pdf",
            new byte[] { 1 }));
        _audit.Entries.Clear();

        Assert.Throws<ContractAttachmentNotFoundException>(() =>
            CreateService().DownloadAttachment(other.Id, id));

        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void EveryOperation_DemandsTheContractPermission()
    {
        var service = CreateDeniedService();
        var upload = new ContractAttachmentUpload("contrato.pdf", "application/pdf", new byte[] { 1 });

        AssertDenied(() => service.List());
        AssertDenied(() => service.Get(1));
        AssertDenied(() => service.Employees());
        AssertDenied(() => service.ExpiringAlerts());
        AssertDenied(() => service.DownloadAttachment(1, 1));
        AssertDenied(() => service.Create(new CreateContractRequest(
            1,
            ContractType.Indefinite,
            new DateOnly(2026, 1, 1),
            null,
            1_000_000m)));
        AssertDenied(() => service.Renew(1, new RenewContractRequest(
            new DateOnly(2027, 1, 1),
            null,
            1_000_000m)));
        AssertDenied(() => service.Terminate(1));
        AssertDenied(() => service.Attach(1, upload));

        Assert.Equal(9, _audit.Entries.Count);
        Assert.All(_audit.Entries, entry =>
        {
            Assert.Equal("AccessDenied", entry.Action);
            Assert.Equal("security", entry.Module);
        });
    }

    private static void AssertDenied(Action action)
    {
        var exception = Assert.Throws<AccessDeniedException>(action);
        Assert.Equal(Permissions.HrContractsManage, exception.Permission);
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Executions { get; private set; }

        public void ExecuteInTransaction(Action action)
        {
            Executions++;
            action();
        }
    }

    private sealed class StubParameterValues : IParameterValues
    {
        public Dictionary<string, int> Integers { get; } = new(StringComparer.Ordinal);

        public int GetInt(string key, int? storeId = null) => Integers[key];

        public decimal GetDecimal(string key, int? storeId = null) => GetInt(key, storeId);

        public string GetText(string key, int? storeId = null) => GetInt(key, storeId).ToString();

        public bool GetBoolean(string key, int? storeId = null) => GetInt(key, storeId) != 0;
    }
}

internal sealed class InMemoryEmploymentContractRepository : IEmploymentContractRepository
{
    private readonly List<EmploymentContract> _contracts = new();
    private readonly List<ContractAttachment> _attachments = new();
    private int _nextContractId = 1;
    private int _nextAttachmentId = 1;

    public IReadOnlyList<EmploymentContract> All() => _contracts;

    public EmploymentContract? GetById(int id) =>
        _contracts.FirstOrDefault(contract => contract.Id == id);

    public IReadOnlyList<EmploymentContract> ListByEmployee(int employeeId) =>
        _contracts
            .Where(contract => contract.EmployeeId == employeeId)
            .OrderByDescending(contract => contract.StartDate)
            .ToList();

    public EmploymentContract? GetActiveByEmployee(int employeeId) =>
        _contracts
            .Where(contract => contract.EmployeeId == employeeId
                && contract.Status == EmploymentContractStatus.Active)
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefault();

    public void Add(EmploymentContract contract)
    {
        TestIds.Assign(contract, _nextContractId++);
        _contracts.Add(contract);
    }

    public void Update(EmploymentContract contract)
    {
        if (!_contracts.Contains(contract))
            _contracts.Add(contract);
    }

    public void AddAttachment(ContractAttachment attachment)
    {
        TestIds.Assign(attachment, _nextAttachmentId++);
        _attachments.Add(attachment);
    }

    public ContractAttachment? GetAttachment(int id) =>
        _attachments.FirstOrDefault(attachment => attachment.Id == id);

    public IReadOnlyList<ContractAttachment> Attachments => _attachments;
}

internal sealed class InMemoryEmploymentContractQuery : IEmploymentContractQuery
{
    private readonly InMemoryEmploymentContractRepository _contracts;
    private readonly InMemoryEmployeeRepository _employees;

    public InMemoryEmploymentContractQuery(
        InMemoryEmploymentContractRepository contracts,
        InMemoryEmployeeRepository employees)
    {
        _contracts = contracts;
        _employees = employees;
    }

    public IReadOnlyList<ContractRow> List() =>
        BuildRows(_contracts.All()
            .OrderByDescending(contract => contract.StartDate)
            .ThenByDescending(contract => contract.Id)
            .ToList());

    public ContractDetail? GetById(int id)
    {
        var contract = _contracts.All().FirstOrDefault(item => item.Id == id);
        if (contract is null)
            return null;

        var attachments = _contracts.Attachments
            .Where(attachment => attachment.ContractId == id)
            .OrderByDescending(attachment => attachment.UploadedAt)
            .Select(attachment => new ContractAttachmentRow(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.UploadedAt,
                "Usuario de prueba"))
            .ToList();

        var history = BuildRows(_contracts.ListByEmployee(contract.EmployeeId));

        return new ContractDetail(BuildRows(new[] { contract })[0], attachments, history);
    }

    public IReadOnlyList<EmployeeContractOption> ListEmployees()
    {
        var active = _contracts.All()
            .Where(contract => contract.Status == EmploymentContractStatus.Active)
            .Select(contract => contract.EmployeeId)
            .ToHashSet();

        return _employees.List()
            .OrderBy(employee => employee.Name)
            .Select(employee => new EmployeeContractOption(
                employee.Id,
                employee.Name,
                employee.IdNumber,
                employee.Position,
                active.Contains(employee.Id)))
            .ToList();
    }

    private IReadOnlyList<ContractRow> BuildRows(IReadOnlyList<EmploymentContract> contracts)
    {
        var employees = _employees.List().ToDictionary(employee => employee.Id);

        return contracts.Select(contract =>
        {
            employees.TryGetValue(contract.EmployeeId, out var employee);

            return new ContractRow(
                contract.Id,
                contract.EmployeeId,
                employee?.Name ?? "—",
                employee?.IdNumber ?? "—",
                employee?.Position ?? "—",
                contract.ContractType,
                contract.StartDate,
                contract.EndDate,
                contract.BaseSalary,
                contract.Status,
                contract.PreviousContractId,
                _contracts.Attachments.Count(attachment => attachment.ContractId == contract.Id),
                contract.CreatedAt,
                contract.TerminatedAt);
        }).ToList();
    }
}
