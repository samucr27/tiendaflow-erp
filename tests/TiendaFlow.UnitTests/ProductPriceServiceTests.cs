using Moq;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductPriceServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IAuditLog> _audit = new();
    private readonly ProductPriceService _service;

    public ProductPriceServiceTests() => _service = new ProductPriceService(_repo.Object, _audit.Object);

    private Product GivenProduct()
    {
        var product = new Product("SKU-001", "7701234567890", "Soda 400 ml", 3500m);
        _repo.Setup(r => r.GetBySku("SKU-001")).Returns(product);
        return product;
    }

    [Fact]
    public void UpdatePrice_ChangesThePriceSavesAndRecordsAnAuditEntry()
    {
        var product = GivenProduct();

        _service.UpdatePrice("SKU-001", 3900m, "user-1", Now);

        Assert.Equal(3900m, product.SalePrice);
        _repo.Verify(r => r.Update(product), Times.Once);
        _audit.Verify(a => a.Record(It.Is<AuditEntry>(e =>
            e.Action == "PriceChanged"
            && e.EntityId == "SKU-001"
            && e.UserId == "user-1"
            && e.Date == Now
            && e.Detail.Contains("3500")
            && e.Detail.Contains("3900"))), Times.Once);
    }

    [Fact]
    public void UpdatePrice_WithUnknownSku_ThrowsProductNotFoundException()
    {
        _repo.Setup(r => r.GetBySku("SKU-404")).Returns((Product?)null);

        Assert.Throws<ProductNotFoundException>(() => _service.UpdatePrice("SKU-404", 1000m, "user-1", Now));
    }

    [Fact]
    public void UpdatePrice_WithInvalidPrice_ThrowsAndDoesNotAudit()
    {
        GivenProduct();

        Assert.Throws<InvalidProductException>(() => _service.UpdatePrice("SKU-001", 0m, "user-1", Now));
        _audit.Verify(a => a.Record(It.IsAny<AuditEntry>()), Times.Never);
    }
}
