using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class BusinessDashboardServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 11, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IInvoiceQuery> _invoices = new();
    private readonly Mock<IPurchaseOrderQuery> _orders = new();

    [Fact]
    public void Get_WithSalesAndPurchasingPermissions_SummarisesTodayAndOpenOrders()
    {
        _invoices
            .Setup(query => query.List())
            .Returns(new[]
            {
                Invoice(1, NowUtc.AddMinutes(-30), InvoiceStatus.Issued, 100m),
                Invoice(2, NowUtc.AddMinutes(-10), InvoiceStatus.Issued, 50m),
                Invoice(3, NowUtc.AddMinutes(-15), InvoiceStatus.Cancelled, 900m),
                Invoice(4, NowUtc.AddDays(-2), InvoiceStatus.Issued, 700m)
            });

        _orders
            .Setup(query => query.List())
            .Returns(new[]
            {
                Order(1, PurchaseOrderStatus.Draft, 100m),
                Order(2, PurchaseOrderStatus.Sent, 50m),
                Order(3, PurchaseOrderStatus.Received, 999m),
                Order(4, PurchaseOrderStatus.Cancelled, 777m)
            });

        var dashboard = CreateService(Granted(
            Permissions.InventoryStockView,
            Permissions.SalesReportsView,
            Permissions.PurchasingOrdersView)).Get(NowUtc);

        Assert.True(dashboard.CanViewSales);
        Assert.Equal(2, dashboard.SalesTodayCount);
        Assert.Equal(150m, dashboard.SalesTodayTotal);

        Assert.True(dashboard.CanViewPurchasing);
        Assert.Equal(2, dashboard.OpenPurchaseOrders);
        Assert.Equal(150m, dashboard.OpenPurchaseOrdersValue);
        Assert.Equal(1, dashboard.PurchaseOrdersPendingApproval);
    }

    [Fact]
    public void Get_WithoutModulePermissions_ReturnsNoFiguresAndSkipsTheQueries()
    {
        var dashboard = CreateService(Granted(Permissions.InventoryStockView)).Get(NowUtc);

        Assert.False(dashboard.CanViewSales);
        Assert.Equal(0, dashboard.SalesTodayCount);
        Assert.Equal(0m, dashboard.SalesTodayTotal);

        Assert.False(dashboard.CanViewPurchasing);
        Assert.Equal(0, dashboard.OpenPurchaseOrders);
        Assert.Equal(0m, dashboard.OpenPurchaseOrdersValue);
        Assert.Equal(0, dashboard.PurchaseOrdersPendingApproval);

        _invoices.Verify(query => query.List(), Times.Never);
        _orders.Verify(query => query.List(), Times.Never);
    }

    [Fact]
    public void Get_WithoutInventoryPermission_IsRejected()
    {
        var service = CreateService(Granted(Permissions.SalesReportsView));

        Assert.Throws<AccessDeniedException>(() => service.Get(NowUtc));

        _invoices.Verify(query => query.List(), Times.Never);
        _orders.Verify(query => query.List(), Times.Never);
    }

    [Fact]
    public void Get_WithoutPurchasingPermission_StillReturnsSales()
    {
        _invoices
            .Setup(query => query.List())
            .Returns(new[] { Invoice(1, NowUtc.AddMinutes(-5), InvoiceStatus.Issued, 80m) });

        var dashboard = CreateService(Granted(
            Permissions.InventoryStockView,
            Permissions.SalesReportsView)).Get(NowUtc);

        Assert.True(dashboard.CanViewSales);
        Assert.Equal(80m, dashboard.SalesTodayTotal);
        Assert.False(dashboard.CanViewPurchasing);

        _orders.Verify(query => query.List(), Times.Never);
    }

    private BusinessDashboardService CreateService(IAccessControl accessControl) =>
        new(_invoices.Object, _orders.Object, accessControl);

    private static IAccessControl Granted(params string[] permissions)
    {
        var access = new Mock<IAccessControl>();
        var granted = new HashSet<string>(permissions, StringComparer.Ordinal);

        access
            .Setup(control => control.Has(It.IsAny<string>()))
            .Returns((string permission) => granted.Contains(permission));

        access
            .Setup(control => control.Demand(It.IsAny<string>()))
            .Callback((string permission) =>
            {
                if (!granted.Contains(permission))
                    throw new AccessDeniedException("Permission denied.");
            });

        return access.Object;
    }

    private static InvoiceRow Invoice(
        int id,
        DateTime issueDate,
        InvoiceStatus status,
        decimal total) =>
        new(id, "Cliente", "900123456", issueDate, status, 1, total);

    private static PurchaseOrderRow Order(
        int id,
        PurchaseOrderStatus status,
        decimal total) =>
        new(id, 1, "Proveedor", 1, "Tienda", NowUtc, status, total, 1);
}
