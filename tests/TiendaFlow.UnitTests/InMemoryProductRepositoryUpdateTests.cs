using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InMemoryProductRepositoryUpdateTests
{
    [Fact]
    public void Update_ReplacesTheStoredProduct()
    {
        var repo = new InMemoryProductRepository();
        repo.Add(new Product("SKU-013", "7701234567813", "Butter", 7000m));
        var updated = new Product("SKU-013", "7701234567813", "Butter", 7500m);

        repo.Update(updated);

        Assert.Same(updated, repo.GetBySku("SKU-013"));
        Assert.Equal(7500m, repo.GetBySku("SKU-013")!.SalePrice);
    }
}
