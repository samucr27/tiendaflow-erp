using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record StockInfo(
    string Sku,
    int StoreId,
    int QuantityAvailable,
    string Location,
    InventoryMovement? LastMovement);
