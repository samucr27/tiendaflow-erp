using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

/// <summary>
/// Spanish labels and badge tones for the purchasing screens. The domain keeps
/// the English status names; only the presentation layer translates them.
/// </summary>
public static class PurchasingText
{
    public static string Status(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "Borrador",
        PurchaseOrderStatus.PendingApproval => "En aprobación",
        PurchaseOrderStatus.Approved => "Aprobada",
        PurchaseOrderStatus.Sent => "Enviada al proveedor",
        PurchaseOrderStatus.Confirmed => "Confirmada",
        PurchaseOrderStatus.PartiallyReceived => "Recibida parcialmente",
        PurchaseOrderStatus.Received => "Recibida",
        PurchaseOrderStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };

    public static string Tone(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "neutral",
        PurchaseOrderStatus.PendingApproval => "warning",
        PurchaseOrderStatus.Approved => "info",
        PurchaseOrderStatus.Sent => "info",
        PurchaseOrderStatus.Confirmed => "info",
        PurchaseOrderStatus.PartiallyReceived => "warning",
        PurchaseOrderStatus.Received => "ok",
        PurchaseOrderStatus.Cancelled => "danger",
        _ => "neutral"
    };

    public static bool IsOpen(PurchaseOrderStatus status) =>
        status is not (PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled);

    public static bool CanReceive(PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Sent
            or PurchaseOrderStatus.Confirmed
            or PurchaseOrderStatus.PartiallyReceived;

    public static string SupplierState(bool active) => active ? "Activo" : "Inactivo";

    public static string SupplierTone(bool active) => active ? "ok" : "neutral";
}
