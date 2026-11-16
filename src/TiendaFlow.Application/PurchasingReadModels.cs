using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Supplier data plus the purchase history and the two objective indicators
/// the ranking shows. The manual rating is a percentage kept by the buyer;
/// punctuality and fulfillment come from the purchase orders, never from a
/// typed value.
/// </summary>
public record SupplierRow(
    int Id,
    string Nit,
    string BusinessName,
    string Address,
    decimal Rating,
    bool Active,
    int OrderCount,
    decimal PurchasedTotal,
    int ReceivedOrderCount,
    int MeasuredOrderCount,
    int OnTimeOrderCount,
    int OrderedUnits,
    int ReceivedUnits)
{
    public string RatingLabel => $"{Rating:0} %";

    /// <summary>
    /// Share of fully received orders that arrived on or before the promised
    /// date. Orders without a promised date are not part of the denominator,
    /// and a supplier with no measured delivery reports "Sin recepciones".
    /// </summary>
    public decimal? PunctualityRate => MeasuredOrderCount == 0
        ? null
        : Math.Round(OnTimeOrderCount * 100m / MeasuredOrderCount, 1);

    /// <summary>
    /// Received units over ordered units on orders that already have at least
    /// one receipt. It exposes short deliveries that the manual rating misses.
    /// </summary>
    public decimal? FulfillmentRate => OrderedUnits == 0
        ? null
        : Math.Round(ReceivedUnits * 100m / OrderedUnits, 1);

    public string PunctualityLabel =>
        PunctualityRate is null ? "Sin recepciones" : $"{PunctualityRate:0.#} %";

    public string FulfillmentLabel =>
        FulfillmentRate is null ? "Sin recepciones" : $"{FulfillmentRate:0.#} %";
}

public record PurchaseOrderRow(
    int Id,
    int SupplierId,
    string SupplierName,
    int StoreId,
    string StoreName,
    DateTime OrderDate,
    DateOnly? PromisedDate,
    DateOnly? ReceivedDate,
    PurchaseOrderStatus Status,
    decimal Total,
    int LineCount)
{
    public bool IsReceived => Status == PurchaseOrderStatus.Received;

    public string PunctualityLabel => !IsReceived || PromisedDate is null || ReceivedDate is null
        ? "Sin recepciones"
        : ReceivedDate <= PromisedDate ? "A tiempo" : "Con retraso";
}

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
/// minimum level and covers the missing units, never less than one. It is the
/// quick suggestion of the manual order form; the replenishment screen uses
/// the consumption-based formula instead.
/// </summary>
public record ReorderSuggestion(
    string Sku,
    string ProductName,
    int Available,
    int MinStock,
    int SuggestedQuantity,
    decimal ReferenceUnitCost);

/// <summary>
/// One row of the replenishment preview. The quantity is
/// max(minimum - stock, average daily consumption * coverage days - stock),
/// rounded up, so both the minimum level and the expected demand are covered.
/// When the product has no default supplier it stays visible but cannot be
/// turned into a draft.
/// </summary>
public record ReplenishmentSuggestionRow(
    string Sku,
    string ProductName,
    int Available,
    int MinStock,
    decimal AverageDailyConsumption,
    int TargetStock,
    int SuggestedQuantity,
    int? SupplierId,
    string? SupplierName,
    decimal? LastCost)
{
    public bool HasSupplier => SupplierId is not null && LastCost is > 0m;

    public decimal EstimatedCost => SuggestedQuantity * (LastCost ?? 0m);
}

public record ReplenishmentPreview(
    int ConsumptionWindowDays,
    int CoverageDays,
    IReadOnlyList<ReplenishmentSuggestionRow> Rows)
{
    public int ReadyCount => Rows.Count(row => row.HasSupplier);

    public int MissingSupplierCount => Rows.Count(row => !row.HasSupplier);

    public decimal EstimatedTotal => Rows.Sum(row => row.EstimatedCost);
}

public record ReplenishmentDraft(
    int OrderId,
    int SupplierId,
    string SupplierName,
    int LineCount,
    decimal Total);

public record ReplenishmentGenerationResult(
    IReadOnlyList<ReplenishmentDraft> Orders,
    int SkippedRows)
{
    public int TotalLines => Orders.Sum(order => order.LineCount);

    public decimal TotalValue => Orders.Sum(order => order.Total);
}

/// <summary>
/// The relation between a product and a supplier as the purchasing screens
/// show it.
/// </summary>
public record ProductSupplierRow(
    string ProductSku,
    string ProductName,
    int SupplierId,
    string SupplierName,
    decimal LastCost,
    bool IsDefault,
    bool SupplierActive);
