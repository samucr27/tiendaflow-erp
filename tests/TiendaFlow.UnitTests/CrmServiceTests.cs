using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Application rules of the CRM modules: permissions and store scope are
/// demanded before any repository runs, multi-table writes share one
/// transaction, the loyalty ledger is immutable and the CSV reuses the safe
/// writer. Clocks, ids and credentials are fixed, so the tests are portable.
/// </summary>
public class CrmServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(LocalTime.ToLocal(Now));

    private readonly Mock<ILoyaltyRepository> _movements = new();
    private readonly Mock<ICustomerQuery> _customers = new();
    private readonly Mock<IPqrsRepository> _cases = new();
    private readonly Mock<ICampaignRepository> _campaigns = new();
    private readonly InMemoryStoreRepository _stores = new(
        new Store(1, "Centro", "Calle 1"),
        new Store(2, "Norte", "Calle 2"));
    private readonly RecordingAuditLog _audit = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();
    private readonly StubDecimalParameterValues _parameters = new();
    private readonly FixedClock _clock = new(Now);
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.9");
    private readonly PqrsCase _case = Case(11);

    public CrmServiceTests()
    {
        _parameters
            .With(SystemParameterCatalog.CopPerLoyaltyPoint, 1000m)
            .With(SystemParameterCatalog.CopPerRedeemedPoint, 500m)
            .With(SystemParameterCatalog.PqrsResponseDays, 2m);

        _customers.Setup(query => query.List()).Returns([Row(7), Row(8)]);
        _customers.Setup(query => query.GetById(It.IsAny<int>())).Returns<int>(id => Row(id));

        _movements.Setup(repository => repository.ReferenceExists(
                It.IsAny<string>(), It.IsAny<LoyaltyMovementType>()))
            .Returns(false);
        _movements.Setup(repository => repository.BalanceFor(It.IsAny<int>())).Returns(100);
        _movements.Setup(repository => repository.List(
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns([]);
        _movements.Setup(repository => repository.Add(It.IsAny<LoyaltyMovement>()))
            .Callback<LoyaltyMovement>(movement => TestIds.Assign(movement, 9));

        _cases.Setup(repository => repository.List(
                It.IsAny<int?>(), It.IsAny<PqrsStatus?>()))
            .Returns([]);
        _cases.Setup(repository => repository.GetById(It.IsAny<int>()))
            .Returns<int>(id => id == 11 ? _case : Case(id));
        _cases.Setup(repository => repository.Add(It.IsAny<PqrsCase>()))
            .Callback<PqrsCase>(item => TestIds.Assign(item, 11));

        _campaigns.Setup(repository => repository.List()).Returns([]);
        _campaigns.Setup(repository => repository.ExistsName(
                It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(false);
        _campaigns.Setup(repository => repository.Add(It.IsAny<Campaign>()))
            .Callback<Campaign>(campaign => TestIds.Assign(campaign, 12));
    }

    private LoyaltyService Loyalty(
        IAccessControl? access = null,
        FakeCurrentUser? user = null) => new(
            _movements.Object, Mock.Of<ICustomerRepository>(), _customers.Object,
            _stores, access ?? new AlwaysAllowAccessControl(), _audit,
            user ?? _user, _clock, _parameters, _unitOfWork);

    private PqrsService Pqrs(
        IAccessControl? access = null,
        FakeCurrentUser? user = null) => new(
            _cases.Object, _customers.Object, _stores,
            access ?? new AlwaysAllowAccessControl(), _audit,
            user ?? _user, _clock, _parameters, _unitOfWork);

    private CampaignService Campaigns(IAccessControl? access = null) => new(
        _campaigns.Object, access ?? new AlwaysAllowAccessControl(),
        _audit, _user, _clock, _unitOfWork);

    private static AccessControl Restricted(FakeCurrentUser user) => new(user, new RecordingAuditLog());

    private static CustomerRow Row(int id) =>
        new(id, $"100000000{id}", "Ana Torres", "ana@example.com", "3001234567", "Calle 1", true);

    private static PqrsCase Case(int id) => new(
        id, 7, 1, PqrsType.Complaint, "Entrega tardía", "Llegó tarde",
        Today, Today.AddDays(20), Now);

    [Fact]
    public void LoyaltyDemandsItsPermissionBeforeTouchingTheRepository()
    {
        var user = new FakeCurrentUser(permissions: new HashSet<string>());

        Assert.Throws<AccessDeniedException>(() => Loyalty(
            Restricted(user), user).List(null, null, null, null));

        _movements.Verify(repository => repository.List(
            It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
            It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public void AStoreEmployeeCannotAskForAnotherStoresLedger()
    {
        var cashier = new FakeCurrentUser(
            role: RoleNames.Cashier, storeId: 1,
            permissions: new HashSet<string> { Permissions.CrmPointsManage });
        var service = Loyalty(Restricted(cashier), cashier);

        var rows = service.List(null, null, null, null);
        Assert.Empty(rows);
        _movements.Verify(repository => repository.List(
            null, null, 1, null), Times.Once);

        Assert.Throws<AccessDeniedException>(() => service.List(null, null, 2, null));
    }

    [Fact]
    public void ACorporateUserKeepsTheRequestedStoreFilter()
    {
        Loyalty().List(null, null, 2, null);

        _movements.Verify(repository => repository.List(
            null, null, 2, null), Times.Once);
    }

    [Fact]
    public void EarningPointsUsesTheConfiguredRateAndAuditsInOneTransaction()
    {
        var id = Loyalty().Earn(new LoyaltyEarnRequest(7, 1, 25500m, "FACT-77"));

        Assert.Equal(9, id);
        Assert.Equal(1, _unitOfWork.Executions);

        _movements.Verify(repository => repository.Add(It.Is<LoyaltyMovement>(movement =>
            movement.Type == LoyaltyMovementType.Earned
            && movement.Points == 25
            && movement.AmountCop == 25500m
            && movement.Reference == "FACT-77"
            && movement.StoreId == 1)), Times.Once);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("LoyaltyPointsEarned", entry.Action);
        Assert.Equal("crm", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.Equal("10.0.0.9", entry.Ip);
    }

    [Fact]
    public void EarningRejectsADuplicateReferenceAndAnAmountBelowOnePoint()
    {
        _movements.Setup(repository => repository.ReferenceExists("FACT-77", LoyaltyMovementType.Earned))
            .Returns(true);

        Assert.Throws<LoyaltyReferenceAlreadyUsedException>(() => Loyalty().Earn(
            new LoyaltyEarnRequest(7, 1, 25500m, "FACT-77")));
        Assert.Throws<InvalidLoyaltyMovementException>(() => Loyalty().Earn(
            new LoyaltyEarnRequest(7, 1, 500m, "FACT-78")));

        _movements.Verify(repository => repository.Add(It.IsAny<LoyaltyMovement>()), Times.Never);
    }

    [Fact]
    public void RedeemingChecksTheBalanceAndComputesTheDiscount()
    {
        var id = Loyalty().Redeem(new LoyaltyRedeemRequest(7, 1, 40, "CANJE-1"));

        Assert.Equal(9, id);
        Assert.Equal(1, _unitOfWork.Executions);
        _movements.Verify(repository => repository.Add(It.Is<LoyaltyMovement>(movement =>
            movement.Type == LoyaltyMovementType.Redeemed
            && movement.Points == 40
            && movement.AmountCop == 20000m)), Times.Once);
    }

    [Fact]
    public void RedeemingMoreThanTheBalanceIsRejected()
    {
        Assert.Throws<InvalidLoyaltyMovementException>(() => Loyalty().Redeem(
            new LoyaltyRedeemRequest(7, 1, 101, "CANJE-2")));

        _movements.Verify(repository => repository.Add(It.IsAny<LoyaltyMovement>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void BalancesComputeBothTotalsAndTheRedeemableAmount()
    {
        _movements.Setup(repository => repository.List(
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns([
                new LoyaltyMovement(1, 7, LoyaltyMovementType.Earned, 25, 25000m, 1, "FACT-1", Now),
                new LoyaltyMovement(2, 7, LoyaltyMovementType.Redeemed, 10, 5000m, 1, "CANJE-1", Now)
            ]);

        var balance = Assert.Single(Loyalty().Balances());

        Assert.Equal(25, balance.EarnedPoints);
        Assert.Equal(10, balance.RedeemedPoints);
        Assert.Equal(15, balance.Balance);
        Assert.Equal(7500m, balance.RedeemableCop);
    }

    [Fact]
    public void TheLedgerCsvUsesSpanishHeadersAndNeutralizesFormulas()
    {
        _movements.Setup(repository => repository.List(
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns([
                new LoyaltyMovement(1, 7, LoyaltyMovementType.Earned, 25, 25000m,
                    1, "=CMD()", Now)
            ]);

        var csv = Loyalty().LedgerCsv(null, null, null, null);

        Assert.Contains("Fecha,Cliente,Documento,Tipo,Puntos", csv);
        Assert.Contains("'=CMD()", csv);
        Assert.Contains("Acumulación", csv);
    }

    [Fact]
    public void PQrsDemandsItsPermissionBeforeTouchingTheRepository()
    {
        var user = new FakeCurrentUser(permissions: new HashSet<string>());

        Assert.Throws<AccessDeniedException>(() => Pqrs(
            Restricted(user), user).List(null, null));

        _cases.Verify(repository => repository.List(
            It.IsAny<int?>(), It.IsAny<PqrsStatus?>()), Times.Never);
    }

    [Fact]
    public void CreatingAPqrsUsesBusinessDaysAndAuditsInOneTransaction()
    {
        var id = Pqrs().Create(new PqrsCreateRequest(
            7, 1, PqrsType.Complaint, "Entrega tardía", "Llegó tarde"));

        Assert.Equal(11, id);
        Assert.Equal(1, _unitOfWork.Executions);
        _cases.Verify(repository => repository.Add(It.Is<PqrsCase>(item =>
            item.CreatedDate == Today
            && item.DueDate == new DateOnly(2026, 11, 20)
            && item.Status == PqrsStatus.Open)), Times.Once);
        Assert.Equal("PqrsCreated", Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public void ThePqrsDeadlineSkipsTheWeekend()
    {
        var friday = new FakeCurrentUser();
        var service = new PqrsService(
            _cases.Object, _customers.Object, _stores,
            new AlwaysAllowAccessControl(), _audit, friday,
            new FixedClock(new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc)),
            _parameters, _unitOfWork);

        service.Create(new PqrsCreateRequest(7, 1, PqrsType.Petition, "Asunto", "Descripción"));

        // Friday 20 November + 2 business days skips the weekend and lands on
        // Tuesday 24 November. Computing it from the local date keeps the test
        // portable across timezones.
        var fridayDate = DateOnly.FromDateTime(LocalTime.ToLocal(
            new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc)));
        var expected = AddBusinessDays(fridayDate, 2);

        _cases.Verify(repository => repository.Add(It.Is<PqrsCase>(item =>
            item.DueDate == expected)), Times.Once);
    }

    [Fact]
    public void AStoreManagerCannotOpenACaseFromAnotherStore()
    {
        var manager = new FakeCurrentUser(
            role: RoleNames.StoreManager, storeId: 2,
            permissions: new HashSet<string> { Permissions.CrmPqrsManage });
        var service = Pqrs(Restricted(manager), manager);

        Assert.Throws<AccessDeniedException>(() => service.Get(11));
        Assert.Throws<AccessDeniedException>(() => service.Start(11));
        Assert.Throws<AccessDeniedException>(() => service.Answer(11, "Respuesta"));
        Assert.Throws<AccessDeniedException>(() => service.Close(11));
    }

    [Fact]
    public void APqrsCaseAdvancesAndItsResponseIsReturnedInTheDetail()
    {
        var service = Pqrs();

        service.Start(11);
        service.Answer(11, "Respuesta enviada");
        service.Close(11);

        Assert.Equal(3, _unitOfWork.Executions);
        Assert.Equal(
            ["PqrsStarted", "PqrsAnswered", "PqrsClosed"],
            _audit.Entries.Select(entry => entry.Action));

        // The repository double kept the same instance, so the detail shows the
        // final state and the response without another query.
        var detail = service.Get(11);
        Assert.Equal(PqrsStatus.Closed, detail.Status);
        Assert.Equal("Respuesta enviada", detail.Response);
    }

    [Fact]
    public void ThePqrsCsvMarksOverdueCasesAndNeutralizesFormulas()
    {
        _cases.Setup(repository => repository.List(
                It.IsAny<int?>(), It.IsAny<PqrsStatus?>()))
            .Returns([new PqrsCase(
                11, 7, 1, PqrsType.Claim, "+SUM(A1)", "Descripción",
                Today.AddDays(-30), Today.AddDays(-2), Now)]);

        var csv = Pqrs().CasesCsv(null, null);

        Assert.Contains("ID,Fecha,Cliente,Documento,Tipo,Asunto", csv);
        Assert.Contains("'+SUM(A1)", csv);
        Assert.Contains("Vencida", csv);
    }

    [Fact]
    public void CampaignsDemandTheirPermissionBeforeTouchingTheRepository()
    {
        var user = new FakeCurrentUser(permissions: new HashSet<string>());

        Assert.Throws<AccessDeniedException>(() => Campaigns(Restricted(user)).List());

        _campaigns.Verify(repository => repository.List(), Times.Never);
    }

    [Fact]
    public void ACampaignIsCreatedAsADraftWithAUniqueFutureName()
    {
        var id = Campaigns().Create(CampaignRequest("Bienvenida", Today.AddDays(1)));

        Assert.Equal(12, id);
        Assert.Equal(1, _unitOfWork.Executions);
        _campaigns.Verify(repository => repository.Add(It.Is<Campaign>(campaign =>
            campaign.Status == CampaignStatus.Draft && campaign.Name == "Bienvenida")), Times.Once);
        Assert.Equal("CampaignCreated", Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public void ACampaignRejectsPastStartDatesAndDuplicateNames()
    {
        _campaigns.Setup(repository => repository.ExistsName("Repetida", null)).Returns(true);

        Assert.Throws<DuplicateCampaignNameException>(() => Campaigns().Create(
            CampaignRequest("Repetida", Today.AddDays(1))));
        Assert.Throws<InvalidCampaignException>(() => Campaigns().Create(
            CampaignRequest("Pasada", Today.AddDays(-1))));

        _campaigns.Verify(repository => repository.Add(It.IsAny<Campaign>()), Times.Never);
    }

    [Fact]
    public void CampaignStateChangesAreAuditedAndAtomic()
    {
        var campaign = NewCampaignDetail();
        _campaigns.Setup(repository => repository.GetById(12)).Returns(campaign);
        var service = Campaigns();

        service.Activate(12);
        service.Pause(12);
        service.Resume(12);
        service.Close(12);

        Assert.Equal(4, _unitOfWork.Executions);
        Assert.Equal(
            ["CampaignActivated", "CampaignPaused", "CampaignResumed", "CampaignClosed"],
            _audit.Entries.Select(entry => entry.Action));
    }

    [Fact]
    public void TheCampaignCsvUsesTheSpanishLabels()
    {
        _campaigns.Setup(repository => repository.List()).Returns([NewCampaignDetail()]);

        var csv = Campaigns().CampaignsCsv();

        Assert.Contains("Nombre,Descripción,Canal,Segmento,Inicio,Fin,Estado", csv);
        Assert.Contains("Correo", csv);
        Assert.Contains("Borrador", csv);
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

    private static CampaignCreateRequest CampaignRequest(string name, DateOnly start) =>
        new(name, "Descripción", CampaignChannel.Email, "Todos", start, start.AddDays(10));

    private static Campaign NewCampaignDetail() => new(
        12, "Bienvenida", "Descripción", CampaignChannel.Email, "Todos",
        Today.AddDays(1), Today.AddDays(30), Now);
}
