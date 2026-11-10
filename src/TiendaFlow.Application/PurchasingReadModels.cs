using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Supplier data plus the purchase history the ranking screen shows.
/// </summary>
public record SupplierRow(
    int Id,
    string Nit,
    string BusinessName,
    string Address,
    decimal Rating,
    bool Active,
    int OrderCount,
    decimal PurchasedTotal)
{
    public string RatingLabel => $"{Rating:0} %";
}

public record PurchaseOrderRow(
    int Id,
    int SupplierId,
    string SupplierName,
    int StoreId,
    string StoreName,
    DateTime OrderDate,
    PurchaseOrderStatus Status,
    decimal Total,
    int LineCount);

public record PurchaseOrderLineRow(
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitCost,
    int ReceivedQuantity)
{
    public int PendingQuantity => Quantity - ReceivedQuantity;

    public decimal LineTotal => Quantity * UnitCost;
}

public record PurchaseOrderDetail(
    PurchaseOrderRow Header,
    IReadOnlyList<PurchaseOrderLineRow> Lines);

/// <summary>
/// A low-stock row turned into a buying proposal. The quantity doubles the
/// minimum level and covers the missing units, never less than one.
/// </summary>
public record ReorderSuggestion(
    string Sku,
    string ProductName,
    int Available,
    int MinStock,
    int SuggestedQuantity,
    decimal ReferenceUnitCost);
