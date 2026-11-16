using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Cross-module indicators for the start screen. A field is only populated
/// when the user has the permission of that module, so the screen never
/// reveals sales or purchasing figures to a role that cannot open them.
/// </summary>
public record BusinessDashboard(
    bool CanViewSales,
    int SalesTodayCount,
    decimal SalesTodayTotal,
    bool CanViewPurchasing,
    int OpenPurchaseOrders,
    decimal OpenPurchaseOrdersValue,
    int PurchaseOrdersPendingApproval,
    int InvoicesTodayCount = 0,
    int PosSalesTodayCount = 0);

/// <summary>
/// Builds the business summary from the same read models the module screens
/// use. It performs no writes and records no audit entry.
/// </summary>
public sealed class BusinessDashboardService
{
    private readonly IInvoiceQuery _invoices;
    private readonly IPurchaseOrderQuery _orders;
    private readonly Security.IAccessControl _accessControl;
    private readonly IPosSaleQuery? _posSales;

    public BusinessDashboardService(
        IInvoiceQuery invoices,
        IPurchaseOrderQuery orders,
        Security.IAccessControl accessControl,
        IPosSaleQuery? posSales = null)
    {
        _invoices = invoices;
        _orders = orders;
        _accessControl = accessControl;
        _posSales = posSales;
    }

    public BusinessDashboard Get(DateTime nowUtc)
    {
        _accessControl.Demand(Security.Permissions.InventoryStockView);

        var today = LocalTime.ToLocal(nowUtc).Date;
        var canViewSales = _accessControl.Has(Security.Permissions.SalesReportsView);
        var canViewPurchasing = _accessControl.Has(Security.Permissions.PurchasingOrdersView);

        var sales = canViewSales
            ? _invoices.List()
                .Where(invoice =>
                    invoice.Status != InvoiceStatus.Cancelled
                    && LocalTime.ToLocal(invoice.IssueDate).Date == today)
                .ToList()
            : new List<InvoiceRow>();

        // Point-of-sale sales are sales too: "Ventas de hoy" adds them to the
        // issued invoices of the same local day.
        var startUtc = LocalTime.ToUtc(DateOnly.FromDateTime(today));
        var posSales = canViewSales && _posSales is not null
            ? _posSales.List(startUtc, LocalTime.ToUtc(DateOnly.FromDateTime(today).AddDays(1)))
            : Array.Empty<PosSaleRow>();

        var openOrders = canViewPurchasing
            ? _orders.List()
                .Where(order => order.Status is not (PurchaseOrderStatus.Received
                    or PurchaseOrderStatus.Cancelled))
                .ToList()
            : new List<PurchaseOrderRow>();

        return new BusinessDashboard(
            canViewSales,
            sales.Count + posSales.Count,
            sales.Sum(invoice => invoice.Total) + posSales.Sum(sale => sale.Total),
            canViewPurchasing,
            openOrders.Count,
            openOrders.Sum(order => order.Total),
            openOrders.Count(order => order.Status is PurchaseOrderStatus.Draft
                or PurchaseOrderStatus.PendingApproval),
            sales.Count,
            posSales.Count);
    }
}
