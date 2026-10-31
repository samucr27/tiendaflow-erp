using Reqnroll;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.AcceptanceTests.StepDefinitions;

[Binding]
public class ConsultStockSteps
{
    private static readonly DateTime SaleDate = new(2026, 11, 1, 10, 0, 0);

    private readonly InMemoryStockRepository _repository = new();
    private readonly StockService _service;
    private StockInfo? _info;
    private Exception? _error;

    public ConsultStockSteps() => _service = new StockService(_repository);

    [Given("store {int} has {int} units of {string} in location {string}")]
    public void GivenStoreHasUnitsInLocation(int storeId, int quantity, string sku, string location) =>
        _repository.Add(new StockItem(sku, storeId, quantity, location));

    [When("I consult the stock of {string} in store {int}")]
    public void WhenIConsultTheStock(string sku, int storeId) =>
        _info = _service.GetStock(storeId, sku);

    [When("{int} units of {string} are sold in store {int}")]
    public void WhenUnitsAreSold(int quantity, string sku, int storeId) =>
        _service.RegisterSale(storeId, sku, quantity, SaleDate);

    [When("I try to sell {int} units of {string} in store {int}")]
    public void WhenITryToSell(int quantity, string sku, int storeId) =>
        _error = Record.Exception(() => _service.RegisterSale(storeId, sku, quantity, SaleDate));

    [When("I try to consult the stock of {string} in store {int}")]
    public void WhenITryToConsultTheStock(string sku, int storeId) =>
        _error = Record.Exception(() => _service.GetStock(storeId, sku));

    [Then("I see {int} units available in location {string}")]
    public void ThenISeeUnitsAvailableInLocation(int quantity, string location)
    {
        Assert.NotNull(_info);
        Assert.Equal(quantity, _info.QuantityAvailable);
        Assert.Equal(location, _info.Location);
    }

    [Then("the last movement is a sale of {int} units")]
    public void ThenTheLastMovementIsASale(int quantity) =>
        Assert.Equal(new InventoryMovement(MovementType.Sale, quantity, SaleDate), _info?.LastMovement);

    [Then("the sale is rejected because there is not enough stock")]
    public void ThenTheSaleIsRejected() =>
        Assert.IsType<InsufficientStockException>(_error);

    [Then("store {int} still has {int} units of {string}")]
    public void ThenStoreStillHasUnits(int storeId, int quantity, string sku) =>
        Assert.Equal(quantity, _service.GetStock(storeId, sku).QuantityAvailable);

    [Then("the consultation is rejected because there is no stock for that product")]
    public void ThenTheConsultationIsRejected() =>
        Assert.IsType<StockNotFoundException>(_error);

    private sealed class InMemoryStockRepository : IStockRepository
    {
        private readonly Dictionary<(int StoreId, string Sku), StockItem> _items = new();

        public StockItem? Get(int storeId, string sku) =>
            _items.GetValueOrDefault((storeId, sku));

        public void Add(StockItem item) => _items[(item.StoreId, item.Sku)] = item;

        public void Update(StockItem item) => _items[(item.StoreId, item.Sku)] = item;
    }
}
