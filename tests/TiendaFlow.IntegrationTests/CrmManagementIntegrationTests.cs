using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Real SQL Server coverage for the CRM tables: the immutable loyalty ledger
/// and its unique reference, DateOnly persistence for PQRS and campaigns, the
/// forward-only workflow and the audit trail written by the services.
/// Authorization is covered by unit and structural tests, so these scenarios
/// allow every operation.
/// </summary>
public class CrmManagementIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(LocalTime.ToLocal(Now));

    private readonly SqlServerDatabaseFixture _database;

    public CrmManagementIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private TiendaFlowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TiendaFlowDbContext>()
            .UseSqlServer(_database.ConnectionString!)
            .Options);

    private static LoyaltyService Loyalty(TiendaFlowDbContext context) => new(
        new EfLoyaltyRepository(context),
        new EfCustomerRepository(context),
        new EfCustomerQuery(context),
        new EfStoreRepository(context),
        new AllowAllAccessControl(),
        new EfAuditLog(context),
        new TestCurrentUser(),
        new TestClock(Now),
        new ParameterValues(new EfSystemParameterRepository(context), new TestClock(Now)),
        new EfUnitOfWork(context));

    private static PqrsService Pqrs(TiendaFlowDbContext context) => new(
        new EfPqrsRepository(context),
        new EfCustomerQuery(context),
        new EfStoreRepository(context),
        new AllowAllAccessControl(),
        new EfAuditLog(context),
        new TestCurrentUser(),
        new TestClock(Now),
        new ParameterValues(new EfSystemParameterRepository(context), new TestClock(Now)),
        new EfUnitOfWork(context));

    private static CampaignService Campaigns(TiendaFlowDbContext context) => new(
        new EfCampaignRepository(context),
        new AllowAllAccessControl(),
        new EfAuditLog(context),
        new TestCurrentUser(),
        new TestClock(Now),
        new EfUnitOfWork(context));

    private static Customer NewCustomer(TiendaFlowDbContext context)
    {
        var customer = new Customer(
            0,
            $"CC-{Guid.NewGuid():N}"[..12],
            "Cliente de integración CRM");
        context.Customers.Add(customer);
        context.SaveChanges();
        return customer;
    }

    [SqlServerFact]
    public async Task EarnAndRedeemPersistAnImmutableLedgerAndTheBalance()
    {
        await using var context = NewContext();
        var customer = NewCustomer(context);
        var loyalty = Loyalty(context);
        var reference = $"FACT-{Guid.NewGuid():N}"[..20];

        var earnedId = loyalty.Earn(new LoyaltyEarnRequest(
            customer.Id, 1, 25500m, reference));
        var redeemedId = loyalty.Redeem(new LoyaltyRedeemRequest(
            customer.Id, 1, 10, $"CANJE-{Guid.NewGuid():N}"[..20]));

        await using var check = NewContext();
        var movements = await check.LoyaltyMovements
            .Where(row => row.Id == earnedId || row.Id == redeemedId)
            .OrderBy(row => row.Id)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.Equal(LoyaltyMovementType.Earned, movements[0].Type);
        Assert.Equal(25, movements[0].Points);
        Assert.Equal(LoyaltyMovementType.Redeemed, movements[1].Type);
        Assert.Equal(10, movements[1].Points);

        var balance = Assert.Single(loyalty.Balances(), row => row.CustomerId == customer.Id);
        Assert.Equal(15, balance.Balance);
        Assert.Equal(7500m, balance.RedeemableCop);

        var actions = await check.AuditEntries
            .Where(entry => entry.Module == "crm"
                && (entry.Action == "LoyaltyPointsEarned" || entry.Action == "LoyaltyPointsRedeemed"))
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Action)
            .ToListAsync();
        Assert.Contains("LoyaltyPointsEarned", actions);
        Assert.Contains("LoyaltyPointsRedeemed", actions);
    }

    [SqlServerFact]
    public void ADuplicateLoyaltyReferenceIsRejectedByTheDatabase()
    {
        using var context = NewContext();
        var customer = NewCustomer(context);
        var reference = $"DUP-{Guid.NewGuid():N}"[..20];

        Loyalty(context).Earn(new LoyaltyEarnRequest(customer.Id, 1, 5000m, reference));

        using var other = NewContext();
        other.LoyaltyMovements.Add(new LoyaltyMovement(
            0, customer.Id, LoyaltyMovementType.Earned, 5, 5000m, 1, reference, Now));

        Assert.Throws<DbUpdateException>(() => other.SaveChanges());
    }

    [SqlServerFact]
    public async Task APqrsCasePersistsBusinessDatesAndTheForwardOnlyWorkflow()
    {
        await using var context = NewContext();
        var customer = NewCustomer(context);
        var pqrs = Pqrs(context);
        var subject = $"Caso {Guid.NewGuid():N}"[..20];

        var id = pqrs.Create(new PqrsCreateRequest(
            customer.Id, 1, PqrsType.Claim, subject, "Descripción del reclamo"));

        await using var check = NewContext();
        var stored = await check.PqrsCases.SingleAsync(row => row.Id == id);
        Assert.Equal(Today, stored.CreatedDate);
        // The seeded parameter is 15 business days; weekends are skipped.
        Assert.Equal(AddBusinessDays(Today, 15), stored.DueDate);
        Assert.Equal(PqrsStatus.Open, stored.Status);

        pqrs.Start(id);
        pqrs.Answer(id, "Respuesta dada al cliente");
        pqrs.Close(id);

        await using var final = NewContext();
        var closed = await final.PqrsCases.SingleAsync(row => row.Id == id);
        Assert.Equal(PqrsStatus.Closed, closed.Status);
        Assert.Equal("Respuesta dada al cliente", closed.Response);
        Assert.NotNull(closed.AnsweredAt);

        var detail = pqrs.Get(id);
        Assert.Equal("Respuesta dada al cliente", detail.Response);

        var actions = await final.AuditEntries
            .Where(entry => entry.Module == "crm"
                && entry.Action.StartsWith("Pqrs"))
            .Select(entry => entry.Action)
            .ToListAsync();
        Assert.Contains("PqrsCreated", actions);
        Assert.Contains("PqrsAnswered", actions);
        Assert.Contains("PqrsClosed", actions);
    }

    [SqlServerFact]
    public async Task ACampaignKeepsDateOnlyColumnsAndAUniqueName()
    {
        await using var context = NewContext();
        var campaigns = Campaigns(context);
        var name = $"Campaña {Guid.NewGuid():N}"[..25];

        var id = campaigns.Create(new CampaignCreateRequest(
            name, "Descripción", CampaignChannel.Email, "Todos",
            Today.AddDays(1), Today.AddDays(20)));

        await using var check = NewContext();
        var stored = await check.Campaigns.SingleAsync(row => row.Id == id);
        Assert.Equal(Today.AddDays(1), stored.StartDate);
        Assert.Equal(Today.AddDays(20), stored.EndDate);
        Assert.Equal(CampaignStatus.Draft, stored.Status);
        Assert.False(stored.IsCurrentlyActive(Today.AddDays(2)));

        Assert.Throws<DuplicateCampaignNameException>(() => campaigns.Create(
            new CampaignCreateRequest(
                name, "Otra", CampaignChannel.Sms, "A",
                Today.AddDays(1), Today.AddDays(20))));

        campaigns.Activate(id);
        Assert.Contains(await check.Campaigns.AsNoTracking()
            .Where(row => row.Id == id && row.Status == CampaignStatus.Active)
            .ToListAsync(), row => row.Id == id);
    }

    private static DateOnly AddBusinessDays(DateOnly start, int days)
    {
        var result = start;
        var remaining = days;

        while (remaining > 0)
        {
            result = result.AddDays(1);

            if (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            remaining--;
        }

        return result;
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
