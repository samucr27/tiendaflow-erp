using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductSupplierServiceTests
{
    private readonly Mock<IProductSupplierRepository> _relations = new();
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IProductCatalogQuery> _products = new();
    private readonly RecordingTransaction _unitOfWork = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5);

    private ProductSupplierService CreateService(IAccessControl? access = null) =>
        new(
            _relations.Object,
            _suppliers.Object,
            _products.Object,
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            _user);

    private ProductSupplierService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 5, permissions: new HashSet<string>()),
            _audit));

    private void GivenProductAndSupplier(string sku = "SKU-1", int supplierId = 5, bool active = true)
    {
        _products.Setup(q => q.GetBySku(sku))
            .Returns(new ProductListItem(sku, "7700001", "Arroz", 2000m));
        _suppliers.Setup(r => r.GetById(supplierId))
            .Returns(new Supplier(supplierId, "900123", "Andina", "Calle 1", active: active));
    }

    [Fact]
    public void List_JoinsProductAndSupplierNamesAndShowsDefaultsFirst()
    {
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 1000m),
            new ProductSupplier("SKU-2", 6, 500m, isDefault: true)
        });
        _products.Setup(q => q.Search(null)).Returns(new[]
        {
            new ProductListItem("SKU-1", "7701", "Arroz", 2000m),
            new ProductListItem("SKU-2", "7702", "Aceite", 9000m)
        });
        _suppliers.Setup(r => r.List()).Returns(new[]
        {
            new Supplier(5, "900123", "Andina", "Calle 1"),
            new Supplier(6, "900456", "Caribe", "Calle 2")
        });

        var rows = CreateService().List();

        Assert.Equal(new[] { "SKU-2", "SKU-1" }, rows.Select(row => row.ProductSku));
        Assert.Equal("Aceite", rows[0].ProductName);
        Assert.Equal("Caribe", rows[0].SupplierName);
        Assert.True(rows[0].IsDefault);
    }

    [Fact]
    public void List_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().List());

        Assert.Equal(Permissions.PurchasingSuppliersView, exception.Permission);
        _relations.Verify(r => r.List(), Times.Never);
    }

    [Fact]
    public void Set_TheFirstRelationOfAProduct_BecomesDefaultAutomatically()
    {
        GivenProductAndSupplier();
        _relations.Setup(r => r.List()).Returns(Array.Empty<ProductSupplier>());
        ProductSupplier? added = null;
        _relations.Setup(r => r.Add(It.IsAny<ProductSupplier>()))
            .Callback<ProductSupplier>(relation => added = relation);

        CreateService().Set(new SetProductSupplierRequest(" SKU-1 ", 5, 1500m, IsDefault: false));

        Assert.NotNull(added);
        Assert.Equal("SKU-1", added!.ProductSku);
        Assert.True(added.IsDefault);
        Assert.Equal(1500m, added.LastCost);
        Assert.Equal(1, _unitOfWork.Executions);

        var entry = _audit.Entries.Single();
        Assert.Equal("ProductSupplierSet", entry.Action);
        Assert.Equal("purchasing", entry.Module);
        Assert.Contains("predeterminado sí", entry.Detail);
    }

    [Fact]
    public void Set_ANewDefault_ClearsThePreviousDefaultOfTheSameProduct()
    {
        GivenProductAndSupplier();
        var previous = new ProductSupplier("SKU-1", 6, 900m, isDefault: true);
        _relations.Setup(r => r.List()).Returns(new[] { previous });

        CreateService().Set(new SetProductSupplierRequest("SKU-1", 5, 1500m, IsDefault: true));

        Assert.False(previous.IsDefault);
        _relations.Verify(r => r.Update(previous), Times.Once);
        _relations.Verify(
            r => r.Add(It.Is<ProductSupplier>(relation =>
                relation.SupplierId == 5 && relation.IsDefault)),
            Times.Once);
    }

    [Fact]
    public void Set_AnExistingRelation_UpdatesTheCostAndDefaultFlag()
    {
        GivenProductAndSupplier();
        var relation = new ProductSupplier("SKU-1", 5, 900m, isDefault: true);
        _relations.Setup(r => r.List()).Returns(new[] { relation });

        CreateService().Set(new SetProductSupplierRequest("SKU-1", 5, 1100m, IsDefault: false));

        Assert.Equal(1100m, relation.LastCost);
        Assert.False(relation.IsDefault);
        _relations.Verify(r => r.Update(relation), Times.Once);
        _relations.Verify(r => r.Add(It.IsAny<ProductSupplier>()), Times.Never);
    }

    [Fact]
    public void Set_WithAnUnknownProduct_ThrowsAndDoesNotSave()
    {
        Assert.Throws<ProductNotFoundException>(
            () => CreateService().Set(new SetProductSupplierRequest("SKU-404", 5, 100m, true)));

        _relations.Verify(r => r.Add(It.IsAny<ProductSupplier>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void Set_WithAnUnknownSupplier_ThrowsAndDoesNotSave()
    {
        _products.Setup(q => q.GetBySku("SKU-1"))
            .Returns(new ProductListItem("SKU-1", "7701", "Arroz", 2000m));

        Assert.Throws<SupplierNotFoundException>(
            () => CreateService().Set(new SetProductSupplierRequest("SKU-1", 99, 100m, true)));

        _relations.Verify(r => r.Add(It.IsAny<ProductSupplier>()), Times.Never);
    }

    [Fact]
    public void Set_WithAnInactiveSupplier_ThrowsAndDoesNotSave()
    {
        GivenProductAndSupplier(active: false);

        Assert.Throws<InvalidProductSupplierException>(
            () => CreateService().Set(new SetProductSupplierRequest("SKU-1", 5, 100m, true)));

        _relations.Verify(r => r.Add(It.IsAny<ProductSupplier>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Set_WithoutPermission_ThrowsBeforeTouchingRepositories()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().Set(new SetProductSupplierRequest("SKU-1", 5, 100m, true)));

        Assert.Equal(Permissions.PurchasingSuppliersManage, exception.Permission);
        _products.Verify(q => q.GetBySku(It.IsAny<string>()), Times.Never);
        _relations.Verify(r => r.Add(It.IsAny<ProductSupplier>()), Times.Never);
    }

    [Fact]
    public void DefaultCost_ReturnsTheCostOfTheDefaultRelation()
    {
        _relations.Setup(r => r.List()).Returns(new[]
        {
            new ProductSupplier("SKU-1", 5, 900m),
            new ProductSupplier("SKU-1", 6, 1100m, isDefault: true)
        });

        Assert.Equal(1100m, CreateService().DefaultCost(" SKU-1 "));
    }

    [Fact]
    public void DefaultCost_WithoutARelation_ReturnsNull()
    {
        _relations.Setup(r => r.List()).Returns(Array.Empty<ProductSupplier>());

        Assert.Null(CreateService().DefaultCost("SKU-1"));
    }

    [Fact]
    public void DefaultCost_WithoutTheCreatePermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().DefaultCost("SKU-1"));

        Assert.Equal(Permissions.PurchasingOrdersCreate, exception.Permission);
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
