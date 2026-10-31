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

    [Fact]
    public void CreateProduct_WithDuplicateBarcode_ThrowsDuplicateBarcodeException()
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.ExistsByBarcode("7701234567890")).Returns(true);
        var service = new InventoryService(repo.Object);
        var product = new Product("SKU-003", "7701234567890", "Potato chips", 4000m);

        Assert.Throws<DuplicateBarcodeException>(() => service.CreateProduct(product));
    }

    [Fact]
    public void CreateProduct_WithNewSkuAndBarcode_SavesProduct()
    {
        var repo = new Mock<IProductRepository>();
        var service = new InventoryService(repo.Object);
        var product = new Product("SKU-002", "7701234567891", "Water 600 ml", 2500m);

        service.CreateProduct(product);

        repo.Verify(r => r.Add(product), Times.Once);
    }
}
