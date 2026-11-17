using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Real SQL Server coverage for the contract registry: DateOnly mapping, the
/// renewal history, the attachment bytes and the read model the screens use.
/// </summary>
public class HrContractsIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public HrContractsIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private TiendaFlowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TiendaFlowDbContext>()
            .UseSqlServer(_database.ConnectionString!)
            .Options);

    private static EmploymentContractService Service(TiendaFlowDbContext context) =>
        new(
            new EfEmploymentContractRepository(context),
            new EfEmploymentContractQuery(context),
            new EfEmployeeRepository(context),
            new ParameterValues(new EfSystemParameterRepository(context), new TestClock(Now)),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            new TestClock(Now));

    private static Employee NewEmployee(TiendaFlowDbContext context)
    {
        var employee = new Employee(
            0,
            1,
            $"CC-{Guid.NewGuid():N}"[..12],
            "Empleado de integración",
            "Cajero",
            "Indefinido",
            2_200_000m);

        new EfEmployeeRepository(context).Add(employee);
        return employee;
    }

    [SqlServerFact]
    public async Task ACreatedContractCanBeRenewedAndTerminatedWithItsHistory()
    {
        await using var context = NewContext();
        var service = Service(context);
        var employee = NewEmployee(context);

        var firstId = service.Create(new CreateContractRequest(
            employee.Id,
            ContractType.FixedTerm,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            2_200_000m));

        var renewedId = service.Renew(firstId, new RenewContractRequest(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 12, 31),
            2_400_000m));

        var detail = service.Get(renewedId);

        Assert.Equal(2, detail.History.Count);
        Assert.Equal(EmploymentContractStatus.Active, detail.Contract.Status);
        Assert.Equal(firstId, detail.Contract.PreviousContractId);
        Assert.Contains(
            detail.History,
            contract => contract.Id == firstId
                && contract.Status == EmploymentContractStatus.Extended);
        Assert.Equal(new DateOnly(2026, 7, 1), detail.Contract.StartDate);
        Assert.Equal(2_400_000m, detail.Contract.BaseSalary);

        service.Terminate(renewedId);

        var terminated = service.Get(renewedId).Contract;
        Assert.Equal(EmploymentContractStatus.Terminated, terminated.Status);
        Assert.NotNull(terminated.TerminatedAt);

        var audit = await context.AuditEntries
            .Where(entry => entry.Module == "hr"
                && !entry.Action.StartsWith("ContractAttachment")
                && (entry.EntityId == firstId.ToString()
                    || entry.EntityId == renewedId.ToString()))
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Action)
            .ToListAsync();

        Assert.Equal(
            new[] { "ContractCreated", "ContractRenewed", "ContractTerminated" },
            audit);
    }

    [SqlServerFact]
    public async Task AnAttachmentIsStoredInSqlAndDownloadedWithAnAuditEntry()
    {
        await using var context = NewContext();
        var service = Service(context);
        var employee = NewEmployee(context);

        var contractId = service.Create(new CreateContractRequest(
            employee.Id,
            ContractType.Indefinite,
            new DateOnly(2026, 3, 1),
            null,
            2_200_000m));

        var content = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var attachmentId = service.Attach(contractId, new ContractAttachmentUpload(
            "contrato-firmado.pdf",
            "application/pdf",
            content));

        var detail = service.Get(contractId);
        var row = Assert.Single(detail.Attachments);
        Assert.Equal("contrato-firmado.pdf", row.FileName);
        Assert.Equal(content.Length, row.SizeBytes);

        var download = service.DownloadAttachment(contractId, attachmentId);

        Assert.Equal(content, download.Content);
        Assert.Equal("application/pdf", download.ContentType);

        var actions = await context.AuditEntries
            .Where(entry => entry.Module == "hr"
                && ((entry.Action.StartsWith("ContractAttachment")
                        && entry.EntityId == attachmentId.ToString())
                    || (!entry.Action.StartsWith("ContractAttachment")
                        && entry.EntityId == contractId.ToString())))
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Action)
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                "ContractCreated",
                "ContractAttachmentUploaded",
                "ContractAttachmentDownloaded"
            },
            actions);
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
