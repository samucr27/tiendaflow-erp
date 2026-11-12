using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Crm;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class CustomerSegmentationServiceTests
{
    private static readonly DateTime Date = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICustomerQuery> _customers = new();
    private readonly Mock<IInvoiceQuery> _invoices = new();

    [Fact]
    public void Get_ClassifiesByVolumeAndIgnoresCancelledAndUnknownDocuments()
    {
        _customers
            .Setup(query => query.List())
            .Returns(new[]
            {
                Customer(1, "100", "Ana"),
                Customer(2, "200", "Bruno"),
                Customer(3, "300", "Carla"),
                Customer(4, "400", "Diego")
            });

        _invoices
            .Setup(query => query.List())
            .Returns(new[]
            {
                Invoice(1, "100", 100m, InvoiceStatus.Issued),
                Invoice(2, "100", 200m, InvoiceStatus.Issued),
                Invoice(3, "200", 50m, InvoiceStatus.Issued),
                Invoice(4, "300", 999m, InvoiceStatus.Cancelled),
                Invoice(5, "999", 5000m, InvoiceStatus.Issued)
            });

        var segmentation = CreateService(Granted(Permissions.CrmReportsView)).Get();

        var ana = Row(segmentation, 1);
        var bruno = Row(segmentation, 2);
        var carla = Row(segmentation, 3);
        var diego = Row(segmentation, 4);

        Assert.Equal(CustomerSegment.A, ana.Segment);
        Assert.Equal(2, ana.InvoiceCount);
        Assert.Equal(300m, ana.TotalPurchased);
        Assert.Equal(150m, ana.AverageTicket);

        Assert.Equal(CustomerSegment.B, bruno.Segment);
        Assert.Equal(CustomerSegment.C, carla.Segment);
        Assert.Equal(CustomerSegment.C, diego.Segment);
        Assert.Equal(0, diego.TotalPurchased);
        Assert.Null(diego.LastPurchaseDate);

        Assert.Equal(1, segmentation.CountA);
        Assert.Equal(1, segmentation.CountB);
        Assert.Equal(2, segmentation.CountC);
        Assert.Equal(350m, segmentation.TotalPurchased);
    }

    [Fact]
    public void Get_OrdersTheResultBySegmentThenVolume()
    {
        _customers
            .Setup(query => query.List())
            .Returns(new[]
            {
                Customer(1, "100", "Ana"),
                Customer(2, "200", "Bruno"),
                Customer(3, "300", "Carla")
            });

        _invoices
            .Setup(query => query.List())
            .Returns(new[]
            {
                Invoice(1, "100", 10m, InvoiceStatus.Issued),
                Invoice(2, "200", 300m, InvoiceStatus.Issued),
                Invoice(3, "300", 200m, InvoiceStatus.Issued)
            });

        var segmentation = CreateService(Granted(Permissions.CrmReportsView)).Get();

        Assert.Equal(new[] { 2, 3, 1 }, segmentation.Rows.Select(row => row.CustomerId));
    }

    [Fact]
    public void Get_WithoutTheReportsPermission_IsRejected()
    {
        var service = CreateService(Granted(Permissions.CrmCustomersView));

        Assert.Throws<AccessDeniedException>(() => service.Get());

        _customers.Verify(query => query.List(), Times.Never);
        _invoices.Verify(query => query.List(), Times.Never);
    }

    private CustomerSegmentationService CreateService(IAccessControl accessControl) =>
        new(_customers.Object, _invoices.Object, accessControl);

    private static CustomerSegmentRow Row(CustomerSegmentation segmentation, int id) =>
        segmentation.Rows.Single(row => row.CustomerId == id);

    private static CustomerRow Customer(int id, string document, string name) =>
        new(id, document, name, null, null, null, true);

    private static InvoiceRow Invoice(
        int id,
        string document,
        decimal total,
        InvoiceStatus status) =>
        new(id, "Cliente", document, Date, status, 1, total);

    private static IAccessControl Granted(params string[] permissions)
    {
        var granted = new HashSet<string>(permissions, StringComparer.Ordinal);
        var access = new Mock<IAccessControl>();

        access
            .Setup(control => control.Has(It.IsAny<string>()))
            .Returns((string permission) => granted.Contains(permission));

        access
            .Setup(control => control.Demand(It.IsAny<string>()))
            .Callback((string permission) =>
            {
                if (!granted.Contains(permission))
                    throw new AccessDeniedException(permission);
            });

        return access.Object;
    }
}
