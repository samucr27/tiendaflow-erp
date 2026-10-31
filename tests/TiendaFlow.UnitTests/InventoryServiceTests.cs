using Moq;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InventoryServiceTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly InventoryService _service;

    public InventoryServiceTests() => _service = new InventoryService(_repo.Object);

    [Fact]
    public void CreateProduct_WithDuplicateSku_ThrowsDuplicateSkuException()
    {
        _repo.Setup(r => r.ExistsBySku("SKU-001")).Returns(true);
        var product = new Product("SKU-001", "7701234567890", "Soda 400 ml", 3500m);

        Assert.Throws<DuplicateSkuException>(() => _service.CreateProduct(product));
    }

    [Fact]
    public void CreateProduct_WithDuplicateBarcode_ThrowsDuplicateBarcodeException()
    {
        _repo.Setup(r => r.ExistsByBarcode("7701234567890")).Returns(true);
        var product = new Product("SKU-003", "7701234567890", "Potato chips", 4000m);

        Assert.Throws<DuplicateBarcodeException>(() => _service.CreateProduct(product));
    }

    [Fact]
    public void CreateProduct_WithNewSkuAndBarcode_SavesProduct()
    {
        var product = new Product("SKU-002", "7701234567891", "Water 600 ml", 2500m);

        _service.CreateProduct(product);

        _repo.Verify(r => r.Add(product), Times.Once);
    }
}
