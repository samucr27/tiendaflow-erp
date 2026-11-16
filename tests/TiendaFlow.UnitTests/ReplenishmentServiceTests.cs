using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ReplenishmentServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IStockQuery> _stock = new();
    private readonly Mock<IConsumptionQuery> _consumption = new();
    private readonly Mock<IProductSupplierRepository> _relations = new();
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IPurchaseOrderRepository> _orders = new();
    private readonly StubParameterValues _parameters = new();
    private readonly RecordingTransaction _unitOfWork = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5);
    private readonly FixedClock _clock = new(Now);
    private readonly List<PurchaseOrder> _added = new();

    public ReplenishmentServiceTests()
    {
        _parameters.Set(SystemParameterCatalog.ReplenishmentConsumptionWindowDays, 30);
        _parameters.Set(SystemParameterCatalog.ReplenishmentCoverageDays, 15);
        _orders.Setup(r => r.Add(It.IsAny<PurchaseOrder>()))
            .Callback<PurchaseOrder>(order =>
            {
                TestIds.Assign(order, _added.Count + 1);
                _added.Add(order);
            });
    }

    private ReplenishmentService CreateService(IAccessControl? access = null) =>
        new(
            _stock.Object,
            _consumption.Object,
            _relations.Object,
            _suppliers.Object,
            _orders.Object,
            _parameters,
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            _user,
            _clock);

    private ReplenishmentService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 5, permissions: new HashSet<string>()),
            _audit));

    private void GivenStock(params StockRow[] rows) =>
        _stock.Setup(q => q.ListByStore(1)).Returns(rows);

    private void GivenConsumption(params ProductConsumption[] rows) =>
        _consumption.Setup(q => q.Get(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(rows);

    private static StockRow Row(string sku, string name, int available, int minStock) =>
        new(sku, name, 1, available, minStock, "A1", 1000m, null);

    private void GivenSupplier(int id, string name, bool active = true) =>
        _suppliers.Setup(r => r.GetById(id))
            .Returns(new Supplier(id, $"90000{id}", name, "Calle 1", active: active));

    [Fact]
    public void Preview_UsesTheConfiguredWindowAndCoverage()
    {
        GivenStock(
            Row("SKU-1", "Arroz", available: 2, minStock: 5),
            Row("SKU-2", "Aceite", available: 10, minStock: 10));
        GivenConsumption(new ProductConsumption("SKU-1", 60));
        _relations.Setup(r => r.List()).Returns(Array.Empty<ProductSupplier>());
        _suppliers.Setup(r => r.List()).Returns(Array.Empty<Supplier>());

        var preview = CreateService().Preview(1);

        Assert.Equal(30, preview.ConsumptionWindowDays);
        Assert.Equal(15, preview.CoverageDays);
        _consumption.Verify(q => q.Get(new DateOnly(2026, 10, 14), new DateOnly(2026, 11, 12)), Times.Once);

        var row = Assert.Single(preview.Rows);
        Assert.Equal("SKU-1", row.Sku);
        Assert.Equal(2m, row.AverageDailyConsumption);
        Assert.Equal(30, row.TargetStock);
        Assert.Equal(28, row.SuggestedQuantity);
        Assert.False(row.HasSupplier);
        Assert.Null(row.SupplierId);
        Assert.Equal(0m, row.EstimatedCost);
        Assert.Equal(0, preview.ReadyCount);
        Assert.Equal(1, preview.MissingSupplierCount);
    }

    [Fact]
    public void Preview_UsesTheMinimumWhenItIsLargerThanTheExpectedDemand()
    {
        GivenStock(Row("SKU-1", "Arroz", available: 2, minStock: 25));
        GivenConsumption(new ProductConsumption("SKU-1", 30));
        _relations.Setup(r => r.List()).Returns(Array.Empty<ProductSupplier>());
        _suppliers.Setup(r => r.List()).Returns(Array.Empty<Supplier>());

        var row = Assert.Single(CreateService().Preview(1).Rows);

        Assert.Equal(15, row.TargetStock);
        Assert.Equal(23, row.SuggestedQuantity);
    }

    [Fact]
    public void Preview_AddsTheDefaultSupplierAndLastCost()
    {
        GivenStock(Row("SKU-1", "Arroz", available: 0, minStock: 10));
        GivenConsumption(new ProductConsumption("SKU-1", 30));
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 1200m, isDefault: true)
        });
        _suppliers.Setup(r => r.List()).Returns(new[]
        {
            new Supplier(5, "900123", "Andina", "Calle 1")
        });

        var preview = CreateService().Preview(1);

        var row = Assert.Single(preview.Rows);
        Assert.True(row.HasSupplier);
        Assert.Equal(5, row.SupplierId);
        Assert.Equal("Andina", row.SupplierName);
        Assert.Equal(1200m, row.LastCost);
        Assert.Equal(18000m, row.EstimatedCost);
        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(18000m, preview.EstimatedTotal);
    }

    [Fact]
    public void Preview_IgnoresNonDefaultRelations()
    {
        GivenStock(Row("SKU-1", "Arroz", available: 0, minStock: 10));
        GivenConsumption(new ProductConsumption("SKU-1", 0));
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 1200m)
        });
        _suppliers.Setup(r => r.List()).Returns(new[]
        {
            new Supplier(5, "900123", "Andina", "Calle 1")
        });

        var row = Assert.Single(CreateService().Preview(1).Rows);

        Assert.False(row.HasSupplier);
    }

    [Fact]
    public void Preview_WithoutPermission_ThrowsBeforeTouchingStock()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().Preview(1));

        Assert.Equal(Permissions.PurchasingOrdersCreate, exception.Permission);
        _stock.Verify(q => q.ListByStore(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Preview_WithoutAStore_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService().Preview(0));
    }

    [Fact]
    public void GenerateDrafts_GroupsBySupplierAndKeepsEveryOrderInDraft()
    {
        GivenStock(
            Row("SKU-1", "Arroz", available: 0, minStock: 10),
            Row("SKU-2", "Aceite", available: 0, minStock: 5),
            Row("SKU-3", "Sal", available: 0, minStock: 4));
        GivenConsumption(new ProductConsumption("SKU-1", 0));
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 1000m, isDefault: true),
            new ProductSupplier("SKU-2", 5, 2000m, isDefault: true),
            new ProductSupplier("SKU-3", 6, 500m, isDefault: true)
        });
        _suppliers.Setup(r => r.List()).Returns(new[]
        {
            new Supplier(5, "900123", "Andina", "Calle 1"),
            new Supplier(6, "900456", "Caribe", "Calle 2")
        });
        GivenSupplier(5, "Andina");
        GivenSupplier(6, "Caribe");

        var result = CreateService().GenerateDrafts(1);

        Assert.Equal(2, result.Orders.Count);
        Assert.Equal(2, _added.Count);
        Assert.All(_added, order => Assert.Equal(PurchaseOrderStatus.Draft, order.Status));
        Assert.Equal(2, _added[0].Lines.Count);
        Assert.Single(_added[1].Lines);
        Assert.Equal(1, _unitOfWork.Executions);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(3, result.TotalLines);

        var grouped = result.Orders.Single(order => order.SupplierId == 5);
        Assert.Equal(2, grouped.LineCount);
        Assert.Equal("Andina", grouped.SupplierName);

        Assert.Contains(_audit.Entries, entry => entry.Action == "ReplenishmentDraftCreated");
        Assert.Contains(_audit.Entries, entry => entry.Action == "ReplenishmentDraftsGenerated");
    }

    [Fact]
    public void GenerateDrafts_SkipsRowsWithoutASupplier()
    {
        GivenStock(
            Row("SKU-1", "Arroz", available: 0, minStock: 10),
            Row("SKU-2", "Aceite", available: 0, minStock: 5));
        GivenConsumption(new ProductConsumption("SKU-1", 0));
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 1000m, isDefault: true)
        });
        _suppliers.Setup(r => r.List()).Returns(new[]
        {
            new Supplier(5, "900123", "Andina", "Calle 1")
        });
        GivenSupplier(5, "Andina");

        var result = CreateService().GenerateDrafts(1);

        Assert.Single(result.Orders);
        Assert.Equal(1, result.SkippedRows);
        Assert.Single(_added);
    }

    [Fact]
    public void GenerateDrafts_SkipsInactiveSuppliers()
    {
        GivenStock(Row("SKU-1", "Arroz", available: 0, minStock: 10));
        GivenConsumption(new ProductConsumption("SKU-1", 0));
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 1000m, isDefault: true)
        });
        _suppliers.Setup(r => r.List()).Returns(new[]
        {
            new Supplier(5, "900123", "Andina", "Calle 1")
        });
        GivenSupplier(5, "Andina", active: false);

        var result = CreateService().GenerateDrafts(1);

        Assert.Empty(result.Orders);
        Assert.Equal(1, result.SkippedRows);
        _orders.Verify(r => r.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public void GenerateDrafts_WithoutReadyRows_DoesNothing()
    {
        GivenStock(Row("SKU-1", "Arroz", available: 50, minStock: 10));
        GivenConsumption(new ProductConsumption("SKU-1", 0));
        _relations.Setup(r => r.List()).Returns(Array.Empty<ProductSupplier>());
        _suppliers.Setup(r => r.List()).Returns(Array.Empty<Supplier>());

        var result = CreateService().GenerateDrafts(1);

        Assert.Empty(result.Orders);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(0, _unitOfWork.Executions);
        _orders.Verify(r => r.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public void GenerateDrafts_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().GenerateDrafts(1));

        Assert.Equal(Permissions.PurchasingOrdersCreate, exception.Permission);
        _orders.Verify(r => r.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public void GenerateDrafts_WithoutAStore_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService().GenerateDrafts(0));
    }

    private sealed class StubParameterValues : IParameterValues
    {
        private readonly Dictionary<string, int> _integers = new(StringComparer.Ordinal);

        public void Set(string key, int value) => _integers[key] = value;

        public int GetInt(string key, int? storeId = null) => _integers[key];

        public decimal GetDecimal(string key, int? storeId = null) => GetInt(key, storeId);

        public string GetText(string key, int? storeId = null) => GetInt(key, storeId).ToString();

        public bool GetBoolean(string key, int? storeId = null) => GetInt(key, storeId) != 0;
    }

    private sealed class RecordingTransaction : IUnitOfWork
    {
        public int Executions { get; private set; }

        public void ExecuteInTransaction(Action action)
        {
            Executions++;
            action();
        }
    }
}
