using TiendaFlow.Application.Security;

namespace TiendaFlow.Application.Reporting;

public sealed record InventoryReportRow(
    string Sku,
    string ProductName,
    string Location,
    int QuantityAvailable,
    int MinStock,
    decimal SalePrice)
{
    public decimal StockValue => QuantityAvailable * SalePrice;

    public bool IsBelowMinimum => QuantityAvailable <= MinStock;

    public string Status =>
        QuantityAvailable == 0 ? "Sin stock"
        : IsBelowMinimum ? "Bajo mínimo"
        : "Disponible";
}

public sealed record InventoryReport(int StoreId, IReadOnlyList<InventoryReportRow> Rows)
{
    public int UnitsInStock => Rows.Sum(row => row.QuantityAvailable);

    public decimal StockValue => Rows.Sum(row => row.StockValue);

    public int LowStockCount => Rows.Count(row => row.IsBelowMinimum);

    public string ToCsv() =>
        CsvReport.Build(
            ["SKU", "Producto", "Ubicacion", "Stock", "Minimo", "Precio", "Valor", "Estado"],
            Rows.Select(row => new[]
            {
                row.Sku,
                row.ProductName,
                row.Location,
                row.QuantityAvailable.ToString(),
                row.MinStock.ToString(),
                CsvReport.Number(row.SalePrice),
                CsvReport.Number(row.StockValue),
                row.Status
            }));
}

/// <summary>
/// Inventory report for one store. It reads the same stock query as the panel
/// but demands the reports permission, so the export has its own guard.
/// </summary>
public sealed class InventoryReportService
{
    private readonly IStockQuery _stock;
    private readonly IAccessControl _accessControl;

    public InventoryReportService(IStockQuery stock, IAccessControl accessControl)
    {
        _stock = stock;
        _accessControl = accessControl;
    }

    public InventoryReport Get(int storeId)
    {
        _accessControl.Demand(Permissions.InventoryReportsView);

        if (storeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(storeId));

        var rows = _stock
            .ListByStore(storeId)
            .OrderBy(row => row.ProductName)
            .ThenBy(row => row.Sku)
            .Select(row => new InventoryReportRow(
                row.Sku,
                row.ProductName,
                row.Location,
                row.QuantityAvailable,
                row.MinStock,
                row.SalePrice))
            .ToList();

        return new InventoryReport(storeId, rows);
    }
}
