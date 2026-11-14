using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Store option used by the inventory screens. Only active stores can receive
/// a new count or transfer, so the menu offers exactly those.
/// </summary>
public sealed record InventoryStoreOption(int Id, string Name);

public sealed record InventoryCountRow(
    int Id,
    int StoreId,
    string StoreName,
    string? Location,
    InventoryCountStatus Status,
    int LineCount,
    int CountedLineCount,
    int DifferenceUnits,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    DateTime? AdjustedAt)
{
    public string StatusLabel => Status switch
    {
        InventoryCountStatus.Open => "Abierto",
        InventoryCountStatus.Closed => "Cerrado",
        InventoryCountStatus.Adjusted => "Ajustado",
        _ => Status.ToString()
    };
}

public sealed record InventoryCountLineRow(
    string ProductSku,
    string ProductName,
    int SystemQuantity,
    int? CountedQuantity)
{
    public int? Difference => CountedQuantity is int counted
        ? counted - SystemQuantity
        : null;
}

public sealed record InventoryCountDetail(
    InventoryCountRow Header,
    IReadOnlyList<InventoryCountLineRow> Lines);

public sealed record InventoryTransferRow(
    int Id,
    int OriginStoreId,
    string OriginStoreName,
    int DestinationStoreId,
    string DestinationStoreName,
    InventoryTransferStatus Status,
    int LineCount,
    int TotalUnits,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    DateTime? CancelledAt)
{
    public string StatusLabel => Status switch
    {
        InventoryTransferStatus.Draft => "Borrador",
        InventoryTransferStatus.Sent => "Enviado",
        InventoryTransferStatus.Received => "Recibido",
        InventoryTransferStatus.Cancelled => "Anulado",
        _ => Status.ToString()
    };
}

public sealed record InventoryTransferLineRow(
    string ProductSku,
    string ProductName,
    int Quantity);

public sealed record InventoryTransferDetail(
    InventoryTransferRow Header,
    IReadOnlyList<InventoryTransferLineRow> Lines);

public sealed record CreateInventoryTransferLine(string ProductSku, int Quantity);

public sealed record CreateInventoryTransferRequest(
    int OriginStoreId,
    int DestinationStoreId,
    IReadOnlyList<CreateInventoryTransferLine> Lines);

public interface IInventoryCountRepository
{
    InventoryCount? GetById(int id);

    IReadOnlyList<InventoryCount> List(int? storeId = null);

    void Add(InventoryCount count);

    void Update(InventoryCount count);
}

public interface IInventoryTransferRepository
{
    InventoryTransfer? GetById(int id);

    IReadOnlyList<InventoryTransfer> List(int? storeId = null);

    void Add(InventoryTransfer transfer);

    void Update(InventoryTransfer transfer);
}

/// <summary>
/// Read side of the two inventory documents. It joins the store and product
/// catalogs so the screens never show numeric ids or raw SKUs.
/// </summary>
public interface IInventoryDocumentQuery
{
    IReadOnlyList<InventoryCountRow> ListCounts(int? storeId = null);

    InventoryCountDetail? GetCount(int id);

    IReadOnlyList<InventoryTransferRow> ListTransfers(int? storeId = null);

    InventoryTransferDetail? GetTransfer(int id);
}
