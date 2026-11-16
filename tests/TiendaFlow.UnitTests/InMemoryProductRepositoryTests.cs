using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InMemoryProductRepositoryTests
{
    private readonly InMemoryProductRepository _repo = new();

    [Fact]
    public void Add_ThenGetBySku_ReturnsTheSameProduct()
    {
        var product = new Product("SKU-010", "7701234567810", "Bread", 3000m);

        _repo.Add(product);

        Assert.Same(product, _repo.GetBySku("SKU-010"));
    }

    [Fact]
    public void ExistsBySku_AfterAdd_ReturnsTrue()
    {
        _repo.Add(new Product("SKU-011", "7701234567811", "Milk 1 L", 4200m));

        Assert.True(_repo.ExistsBySku("SKU-011"));
    }

    [Fact]
    public void ExistsBySku_WithUnknownSku_ReturnsFalse()
    {
        Assert.False(_repo.ExistsBySku("SKU-999"));
    }

    [Fact]
    public void ExistsByBarcode_AfterAdd_ReturnsTrue()
    {
        _repo.Add(new Product("SKU-012", "7701234567812", "Eggs x12", 9800m));

        Assert.True(_repo.ExistsByBarcode("7701234567812"));
    }
}
