using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Read model for the inventory screens. It joins catalog and stock data so
/// the UI does not need to know about persistence details.
/// </summary>
public record StockRow(
    string Sku,
    string ProductName,
    int StoreId,
    int QuantityAvailable,
    int MinStock,
    string Location,
    decimal SalePrice,
    InventoryMovement? LastMovement)
{
    public bool IsBelowMinimum => QuantityAvailable <= MinStock;

    public decimal StockValue => QuantityAvailable * SalePrice;
}
