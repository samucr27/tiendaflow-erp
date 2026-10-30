using Moq;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InventoryServiceTests
{
    [Fact]
    public void CreateProduct_WithDuplicateSku_ThrowsDuplicateSkuException()
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.ExistsBySku("SKU-001")).Returns(true);
        var service = new InventoryService(repo.Object);
        var product = new Product("SKU-001", "7701234567890", "Soda 400 ml", 3500m);

        Assert.Throws<DuplicateSkuException>(() => service.CreateProduct(product));
    }
}
