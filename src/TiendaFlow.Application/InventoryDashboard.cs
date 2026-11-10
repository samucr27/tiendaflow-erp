namespace TiendaFlow.Application;

public record InventoryDashboard(
    int ProductCount,
    int UnitsInStock,
    decimal StockValue,
    IReadOnlyList<StockRow> Rows,
    IReadOnlyList<StockRow> LowStock);
