using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ReportingTests
{
    [Fact]
    public void CsvReport_EscapesCommasQuotesAndLineBreaks()
    {
        var csv = CsvReport.Build(
            ["SKU", "Producto"],
            new[] { new[] { "A1", "Café, \"especial\"\n100 g" } });

        Assert.Equal("SKU,Producto\r\nA1,\"Café, \"\"especial\"\"\n100 g\"\r\n", csv);
    }

    [Fact]
    public void InventoryReport_ComputesTotalsAndExportsTheRows()
    {
        var stock = new Mock<IStockQuery>();
        stock
            .Setup(query => query.ListByStore(3))
            .Returns(new[]
            {
                Row("B2", "Arroz", 10, 4, 2500m),
                Row("A1", "Aceite", 0, 5, 9000m),
                Row("C3", "Café", 20, 5, 1000m)
            });

        var service = new InventoryReportService(stock.Object, AllowAll);
        var report = service.Get(3);

        Assert.Equal(3, report.Rows.Count);
        Assert.Equal("A1", report.Rows[0].Sku);
        Assert.Equal(30, report.UnitsInStock);
        Assert.Equal(45_000m, report.StockValue);
        Assert.Equal(1, report.LowStockCount);

        var csv = report.ToCsv();
        Assert.StartsWith("SKU,Producto,Ubicacion,Stock,Minimo,Precio,Valor,Estado\r\n", csv);
        Assert.Contains("A1,Aceite,Estante A,0,5,9000,0,Sin stock", csv);
        Assert.Contains("C3,Café,Estante C,20,5,1000,20000,Disponible", csv);
    }

    [Fact]
    public void InventoryReport_WithoutTheReportsPermission_IsRejected()
    {
        var stock = new Mock<IStockQuery>();
        var service = new InventoryReportService(
            stock.Object,
            RestrictedTo(Permissions.InventoryStockView));

        Assert.Throws<AccessDeniedException>(() => service.Get(1));

        stock.Verify(query => query.ListByStore(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void InventoryReport_WithAnInvalidStore_IsRejected()
    {
        var service = new InventoryReportService(new Mock<IStockQuery>().Object, AllowAll);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Get(0));
    }

    [Fact]
    public void SalesReport_FiltersByLocalDateAndLeavesCancelledInvoicesOutOfTheTotal()
    {
        var today = new DateTime(2026, 11, 11, 15, 0, 0, DateTimeKind.Utc);
        var invoices = new Mock<IInvoiceQuery>();
        invoices
            .Setup(query => query.List())
            .Returns(new[]
            {
                Invoice(1, today, InvoiceStatus.Issued, 100m),
                Invoice(2, today.AddHours(-1), InvoiceStatus.Cancelled, 30m),
                Invoice(3, today.AddDays(-1), InvoiceStatus.Issued, 700m)
            });

        var service = new SalesReportService(invoices.Object, AllowAll);
        var report = service.Get(new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 11));

        Assert.Equal(2, report.Invoices.Count);
        Assert.Equal(1, report.IssuedCount);
        Assert.Equal(1, report.CancelledCount);
        Assert.Equal(100m, report.IssuedTotal);
        Assert.Equal(100m, report.AverageTicket);

        var csv = report.ToCsv();
        Assert.Equal(3, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("Anulada", csv);
    }

    [Fact]
    public void SalesReport_WithAnInvertedRange_IsRejected()
    {
        var service = new SalesReportService(new Mock<IInvoiceQuery>().Object, AllowAll);

        Assert.Throws<ArgumentException>(() =>
            service.Get(new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 11)));
    }

    [Fact]
    public void SalesReport_WithoutTheReportsPermission_IsRejected()
    {
        var invoices = new Mock<IInvoiceQuery>();
        var service = new SalesReportService(
            invoices.Object,
            RestrictedTo(Permissions.SalesInvoicesView));

        Assert.Throws<AccessDeniedException>(() =>
            service.Get(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));

        invoices.Verify(query => query.List(), Times.Never);
    }

    [Fact]
    public void PayrollReport_KeepsEmployeesWithProblemsVisible()
    {
        var run = new PayrollRun(30, 1, new[]
        {
            new PayrollRunLine(
                1,
                "Ana Pérez",
                "Cajera",
                "Término indefinido",
                new PayrollResult
                {
                    EmployeeId = 1,
                    GrossEarnings = 1000m,
                    EmployeeHealth = 40m,
                    EmployeePension = 40m
                },
                null),
            new PayrollRunLine(
                2,
                "Luis Gómez",
                "Auxiliar",
                "Término fijo",
                null,
                "El empleado no tiene salario asignado.")
        });

        var csv = PayrollReportCsv.Build(run);

        Assert.Contains("Ana Pérez", csv);
        Assert.Contains("920", csv);
        Assert.Contains("Luis Gómez", csv);
        Assert.Contains("El empleado no tiene salario asignado.", csv);
    }

    private static IAccessControl AllowAll => RestrictedTo(Permissions.All.ToArray());

    private static IAccessControl RestrictedTo(params string[] permissions)
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

    private static StockRow Row(
        string sku,
        string name,
        int quantity,
        int min,
        decimal price) =>
        new(sku, name, 3, quantity, min, $"Estante {sku[0]}", price, null);

    private static InvoiceRow Invoice(
        int id,
        DateTime issueDate,
        InvoiceStatus status,
        decimal total) =>
        new(id, "Cliente", "900123456", issueDate, status, 1, total);
}

public class CsvReportSecurityTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@SUM(A1:A2)")]
    public void CsvReport_NeutralizesFormulaInjection(string value)
    {
        var csv = CsvReport.Build(["Valor"], new[] { new[] { value } });

        Assert.Equal($"Valor\r\n'{value}\r\n", csv);
    }

    [Fact]
    public void CsvReport_LeavesRegularTextUntouched()
    {
        var csv = CsvReport.Build(["Valor"], new[] { new[] { "Arroz 500 g" } });

        Assert.Equal("Valor\r\nArroz 500 g\r\n", csv);
    }
}
