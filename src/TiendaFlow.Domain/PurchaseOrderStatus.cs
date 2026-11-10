namespace TiendaFlow.Domain;

public enum PurchaseOrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    Sent,
    Confirmed,
    PartiallyReceived,
    Received,
    Cancelled
}
