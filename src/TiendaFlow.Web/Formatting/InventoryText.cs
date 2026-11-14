using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

/// <summary>
/// Spanish labels and badge tones for the inventory screens. The domain keeps
/// the English enum names; only the presentation layer translates them.
/// </summary>
public static class InventoryText
{
    public static string Movement(MovementType type) => type switch
    {
        MovementType.Receipt => "Recepción",
        MovementType.Sale => "Venta",
        MovementType.Adjustment => "Ajuste",
        MovementType.TransferIn => "Entrada por traslado",
        MovementType.TransferOut => "Salida por traslado",
        _ => type.ToString()
    };

    public static string Movement(InventoryMovement? movement) =>
        movement is null
            ? "Sin movimientos"
            : $"{Movement(movement.Type)} de {Formatters.Quantity(movement.Quantity)} · " +
              Formatters.ShortDateTime(movement.Date);

    public static string CountStatus(InventoryCountStatus status) => status switch
    {
        InventoryCountStatus.Open => "Abierto",
        InventoryCountStatus.Closed => "Cerrado",
        InventoryCountStatus.Adjusted => "Ajustado",
        _ => status.ToString()
    };

    public static string CountTone(InventoryCountStatus status) => status switch
    {
        InventoryCountStatus.Open => "info",
        InventoryCountStatus.Closed => "warning",
        InventoryCountStatus.Adjusted => "ok",
        _ => "neutral"
    };

    public static string TransferStatus(InventoryTransferStatus status) => status switch
    {
        InventoryTransferStatus.Draft => "Borrador",
        InventoryTransferStatus.Sent => "Enviado",
        InventoryTransferStatus.Received => "Recibido",
        InventoryTransferStatus.Cancelled => "Anulado",
        _ => status.ToString()
    };

    public static string TransferTone(InventoryTransferStatus status) => status switch
    {
        InventoryTransferStatus.Draft => "neutral",
        InventoryTransferStatus.Sent => "warning",
        InventoryTransferStatus.Received => "ok",
        InventoryTransferStatus.Cancelled => "danger",
        _ => "neutral"
    };
}
