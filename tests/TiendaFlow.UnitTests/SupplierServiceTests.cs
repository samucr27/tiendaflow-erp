using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SupplierServiceTests
{
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<ISupplierQuery> _query = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5);

    private SupplierService CreateService(IAccessControl? access = null) =>
        new(_suppliers.Object, _query.Object, access ?? new AllowAllAccessControl(), _audit, _user);

    private SupplierService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 5, permissions: new HashSet<string>()),
            _audit));

    private static SupplierRow Row(
        int id,
        string name,
        decimal rating,
        int measuredOrders = 0,
        int onTimeOrders = 0,
        int orderedUnits = 0,
        int receivedUnits = 0) =>
        new(
            id,
            $"90000{id}",
            name,
            "Calle 1",
            rating,
            true,
            0,
            0m,
            0,
            measuredOrders,
            onTimeOrders,
            orderedUnits,
            receivedUnits);

    [Fact]
    public void List_ReturnsTheRowsOfTheQuery()
    {
        var rows = new[] { Row(1, "Alfa", 90m), Row(2, "Beta", 80m) };
        _query.Setup(q => q.List()).Returns(rows);

        Assert.Equal(rows, CreateService().List());
    }

    [Fact]
    public void Ranking_OrdersByRatingThenByName()
    {
        _query.Setup(q => q.List()).Returns(new[]
        {
            Row(1, "Beta", 80m),
            Row(2, "Alfa", 95m),
            Row(3, "Gamma", 95m)
        });

        var ranking = CreateService().Ranking();

        Assert.Equal(new[] { 2, 3, 1 }, ranking.Select(row => row.Id));
    }

    [Fact]
    public void Get_ReturnsTheSupplier()
    {
        var row = Row(1, "Alfa", 90m);
        _query.Setup(q => q.GetById(1)).Returns(row);

        Assert.Equal(row, CreateService().Get(1));
    }

    [Fact]
    public void Get_WithUnknownId_Throws()
    {
        Assert.Throws<SupplierNotFoundException>(() => CreateService().Get(99));
    }

    [Fact]
    public void Create_RegistersTheSupplierWithATrimmedNitAndAudits()
    {
        Supplier? added = null;
        _suppliers.Setup(r => r.Add(It.IsAny<Supplier>())).Callback<Supplier>(s => added = s);

        CreateService().Create(new CreateSupplierRequest(" 900123 ", "Andina", "Calle 1", 90m));

        Assert.NotNull(added);
        Assert.Equal("900123", added!.Nit);
        Assert.Equal(90m, added.Rating);

        var entry = _audit.Entries.Single();
        Assert.Equal("SupplierCreated", entry.Action);
        Assert.Equal("purchasing", entry.Module);
        Assert.Equal(5, entry.UserId);
    }

    [Fact]
    public void Create_WithAnExistingNit_ThrowsAndDoesNotSave()
    {
        _suppliers.Setup(r => r.GetByNit("900123"))
            .Returns(new Supplier(1, "900123", "Andina", "Calle 1"));

        Assert.Throws<DuplicateNitException>(
            () => CreateService().Create(new CreateSupplierRequest("900123", "Otro", "Calle 2")));

        _suppliers.Verify(r => r.Add(It.IsAny<Supplier>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Update_ChangesDetailsAndDeactivates()
    {
        var supplier = new Supplier(3, "900123", "Andina", "Calle 1");
        _suppliers.Setup(r => r.GetById(3)).Returns(supplier);

        CreateService().Update(3, new UpdateSupplierRequest("Andina SAS", "Carrera 2", false));

        Assert.Equal("Andina SAS", supplier.BusinessName);
        Assert.False(supplier.Active);
        _suppliers.Verify(r => r.Update(supplier), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("SupplierUpdated", entry.Action);
        Assert.Contains("inactivo", entry.Detail);
    }

    [Fact]
    public void Update_CanReactivateASupplier()
    {
        var supplier = new Supplier(3, "900123", "Andina", "Calle 1", active: false);
        _suppliers.Setup(r => r.GetById(3)).Returns(supplier);

        CreateService().Update(3, new UpdateSupplierRequest("Andina", "Calle 1", true));

        Assert.True(supplier.Active);
        Assert.Contains("(activo)", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void Update_WithUnknownId_Throws()
    {
        Assert.Throws<SupplierNotFoundException>(
            () => CreateService().Update(99, new UpdateSupplierRequest("X", "Y", true)));
    }

    [Fact]
    public void Evaluate_ChangesTheRatingAndAudits()
    {
        var supplier = new Supplier(3, "900123", "Andina", "Calle 1");
        _suppliers.Setup(r => r.GetById(3)).Returns(supplier);

        CreateService().Evaluate(3, 85m);

        Assert.Equal(85m, supplier.Rating);
        _suppliers.Verify(r => r.Update(supplier), Times.Once);
        Assert.Equal("SupplierEvaluated", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Evaluate_WithAnInvalidRating_ThrowsAndDoesNotSave()
    {
        var supplier = new Supplier(3, "900123", "Andina", "Calle 1");
        _suppliers.Setup(r => r.GetById(3)).Returns(supplier);

        Assert.Throws<InvalidSupplierException>(() => CreateService().Evaluate(3, 150m));

        _suppliers.Verify(r => r.Update(It.IsAny<Supplier>()), Times.Never);
    }

    [Fact]
    public void Evaluate_WithUnknownId_Throws()
    {
        Assert.Throws<SupplierNotFoundException>(() => CreateService().Evaluate(99, 80m));
    }

    [Fact]
    public void Create_WithoutPermission_ThrowsAndDoesNotRegister()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().Create(new CreateSupplierRequest("900", "Andina", "Calle 1")));

        Assert.Equal(Permissions.PurchasingSuppliersManage, exception.Permission);
        _suppliers.Verify(r => r.Add(It.IsAny<Supplier>()), Times.Never);
        Assert.Equal("AccessDenied", _audit.Entries.Single().Action);
    }

    [Fact]
    public void List_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().List());

        Assert.Equal(Permissions.PurchasingSuppliersView, exception.Permission);
    }

    [Fact]
    public void Evaluate_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().Evaluate(1, 80m));

        Assert.Equal(Permissions.PurchasingEvaluationsManage, exception.Permission);
    }

    [Fact]
    public void SupplierRow_WithoutMeasuredDeliveries_ReportsSinRecepciones()
    {
        var row = Row(1, "Alfa", 90m);

        Assert.Null(row.PunctualityRate);
        Assert.Null(row.FulfillmentRate);
        Assert.Equal("Sin recepciones", row.PunctualityLabel);
        Assert.Equal("Sin recepciones", row.FulfillmentLabel);
    }

    [Fact]
    public void SupplierRow_ComputesPunctualityAndFulfillmentFromRealOrders()
    {
        var row = Row(
            1,
            "Alfa",
            90m,
            measuredOrders: 4,
            onTimeOrders: 3,
            orderedUnits: 200,
            receivedUnits: 150);

        Assert.Equal(75m, row.PunctualityRate);
        Assert.Equal(75m, row.FulfillmentRate);
        Assert.Equal("75 %", row.PunctualityLabel);
        Assert.Equal("75 %", row.FulfillmentLabel);
    }

    [Fact]
    public void RankingCsv_ExportsTheKpisWithHeaders()
    {
        _query.Setup(q => q.List()).Returns(new[]
        {
            Row(1, "Alfa", 95m, measuredOrders: 2, onTimeOrders: 2, orderedUnits: 10, receivedUnits: 10),
            Row(2, "Beta", 80m)
        });

        var lines = CreateService().RankingCsv()
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Posicion,Proveedor,NIT", lines[0]);
        Assert.Contains("Puntualidad", lines[0]);
        Assert.Contains("Cumplimiento", lines[0]);
        Assert.Contains("Alfa,900001,95 %,0,0,100 %,100 %,Activo", lines[1]);
        Assert.Contains("Beta,900002,80 %,0,0,Sin recepciones,Sin recepciones,Activo", lines[2]);
    }

    [Fact]
    public void RankingCsv_NeutralizesFormulaInjection()
    {
        _query.Setup(q => q.List()).Returns(new[] { Row(1, "=HOJA", 90m) });

        var csv = CreateService().RankingCsv();

        Assert.Contains("'=HOJA", csv);
        Assert.DoesNotContain(",=HOJA", csv);
    }

    [Fact]
    public void RankingCsv_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().RankingCsv());

        Assert.Equal(Permissions.PurchasingReportsView, exception.Permission);
    }
}