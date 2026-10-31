using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InventoryServiceWithRepositoryTests
{
    private readonly InMemoryProductRepository _repo = new();
    private readonly InventoryService _service;

    public InventoryServiceWithRepositoryTests() => _service = new InventoryService(_repo);

    [Fact]
    public void CreateProduct_WithUniqueData_ProductIsAvailable()
    {
        var product = new Product("SKU-020", "7701234567820", "Rice 500 g", 3300m);

        _service.CreateProduct(product);

        Assert.Same(product, _repo.GetBySku("SKU-020"));
    }

    [Fact]
    public void CreateProduct_WithSameSkuTwice_ThrowsDuplicateSkuException()
    {
        _service.CreateProduct(new Product("SKU-021", "7701234567821", "Lentils 500 g", 4100m));

        Assert.Throws<DuplicateSkuException>(() =>
            _service.CreateProduct(new Product("SKU-021", "7701234567822", "Beans 500 g", 4500m)));
    }

    [Fact]
    public void CreateProduct_WithSameBarcodeTwice_ThrowsDuplicateBarcodeException()
    {
        _service.CreateProduct(new Product("SKU-022", "7701234567823", "Oil 1 L", 12000m));

        Assert.Throws<DuplicateBarcodeException>(() =>
            _service.CreateProduct(new Product("SKU-023", "7701234567823", "Sugar 1 kg", 4800m)));
    }
}
