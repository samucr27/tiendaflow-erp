using Reqnroll;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.AcceptanceTests.StepDefinitions;

[Binding]
public class RegisterProductSteps
{
    private readonly InMemoryProductRepository _repository = new();
    private readonly InventoryService _service;
    private Exception? _error;

    public RegisterProductSteps() => _service = new InventoryService(_repository);

    [Given("no product is registered with SKU {string}")]
    public void GivenNoProductIsRegisteredWithSku(string sku) =>
        Assert.False(_repository.ExistsBySku(sku));

    [Given("a product is registered with SKU {string} and barcode {string}")]
    public void GivenAProductIsRegisteredWithSkuAndBarcode(string sku, string barcode) =>
        _service.CreateProduct(new Product(sku, barcode, "Existing product", 1000m));

    [When("I register the product {string} with barcode {string}, name {string} and price {int}")]
    public void WhenIRegisterTheProduct(string sku, string barcode, string name, int price) =>
        _service.CreateProduct(new Product(sku, barcode, name, price));

    [When("I try to register another product with SKU {string} and barcode {string}")]
    public void WhenITryToRegisterAnotherProduct(string sku, string barcode) =>
        _error = Record.Exception(() =>
            _service.CreateProduct(new Product(sku, barcode, "Another product", 2000m)));

    [Then("the product {string} is available in the catalog")]
    public void ThenTheProductIsAvailableInTheCatalog(string sku) =>
        Assert.NotNull(_repository.GetBySku(sku));

    [Then("the registration is rejected because the SKU already exists")]
    public void ThenTheRegistrationIsRejectedBecauseTheSkuAlreadyExists() =>
        Assert.IsType<DuplicateSkuException>(_error);
}
