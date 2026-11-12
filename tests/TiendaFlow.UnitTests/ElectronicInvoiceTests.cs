using System.Xml.Linq;
using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Invoicing;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ElectronicInvoiceTests
{
    private static readonly DateTime IssueDate = new(2026, 11, 10, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_BuildsAnXmlDocumentWithTheInvoiceData()
    {
        var document = new SimulatedElectronicInvoiceGateway().Generate(Detail());

        Assert.Equal("FV-00007", document.InvoiceNumber);
        Assert.True(document.IsSimulated);
        Assert.Contains("NO DIAN", document.Provider);

        var xml = XDocument.Parse(document.Xml);
        var root = xml.Root!;

        Assert.Equal("Invoice", root.Name.LocalName);
        Assert.Equal("FV-00007", root.Attribute("number")!.Value);
        Assert.Equal("true", root.Attribute("simulated")!.Value);
        Assert.Contains("NO DIAN", root.Attribute("provider")!.Value);

        Assert.Contains("NO es una factura electronica valida", root.Element("Disclaimer")!.Value);

        var customer = root.Element("Customer")!;
        Assert.Equal("900123456", customer.Attribute("document")!.Value);
        Assert.Equal("Ana Pérez", customer.Attribute("name")!.Value);

        var lines = root.Element("Lines")!.Elements("Line").ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal("A1", lines[0].Attribute("sku")!.Value);
        Assert.Equal("2", lines[0].Attribute("quantity")!.Value);
        Assert.Equal("1500", lines[0].Attribute("unitPrice")!.Value);
        Assert.Equal("3000", lines[0].Attribute("total")!.Value);

        Assert.Equal(3500m, decimal.Parse(root.Element("Total")!.Value));
        Assert.Equal("SHA-256", root.Element("Signature")!.Attribute("algorithm")!.Value);
        Assert.Equal("example-digest-not-certified", root.Element("Signature")!.Attribute("kind")!.Value);
        Assert.Equal(document.Signature, root.Element("Signature")!.Value);
    }

    [Fact]
    public void Generate_EscapesSpecialCharactersInsteadOfBreakingTheXml()
    {
        var document = new SimulatedElectronicInvoiceGateway().Generate(
            Detail(customerName: "Ana & Luis <\"especial\">"));

        var root = XDocument.Parse(document.Xml).Root!;

        Assert.Equal(
            "Ana & Luis <\"especial\">",
            root.Element("Customer")!.Attribute("name")!.Value);
        Assert.Contains("&amp;", document.Xml);
        Assert.Contains("&lt;", document.Xml);
    }

    [Fact]
    public void Generate_ProducesADeterministicSha256Digest()
    {
        var gateway = new SimulatedElectronicInvoiceGateway();

        var first = gateway.Generate(Detail());
        var second = gateway.Generate(Detail());

        Assert.Equal(first.Signature, second.Signature);
        Assert.Equal(64, first.Signature.Length);
        Assert.Matches("^[0-9A-F]{64}$", first.Signature);

        var other = gateway.Generate(Detail(totalUnitPrice: 1000m));

        Assert.NotEqual(first.Signature, other.Signature);
    }

    [Fact]
    public void Generate_OnACancelledInvoice_IsRejected()
    {
        var gateway = new SimulatedElectronicInvoiceGateway();

        var exception = Assert.Throws<InvalidInvoiceException>(() =>
            gateway.Generate(Detail(status: InvoiceStatus.Cancelled)));

        Assert.Contains("cancelled invoice", exception.Message);
    }

    [Fact]
    public void Service_WithTheSalesPermission_ReturnsTheSimulatedDocument()
    {
        var query = new Mock<IInvoiceQuery>();
        query.Setup(q => q.GetById(7)).Returns(Detail());

        var service = new ElectronicInvoiceService(
            query.Object,
            new SimulatedElectronicInvoiceGateway(),
            Granted(Permissions.SalesInvoicesView));

        var document = service.Generate(7);

        Assert.Equal("FV-00007", document.InvoiceNumber);
        Assert.True(document.IsSimulated);
    }

    [Fact]
    public void Service_WithoutTheSalesPermission_IsRejectedAndDoesNotReadTheInvoice()
    {
        var query = new Mock<IInvoiceQuery>();
        var service = new ElectronicInvoiceService(
            query.Object,
            new SimulatedElectronicInvoiceGateway(),
            Granted(Permissions.CrmCustomersView));

        Assert.Throws<AccessDeniedException>(() => service.Generate(7));

        query.Verify(q => q.GetById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Service_WithAnUnknownInvoice_ThrowsNotFound()
    {
        var query = new Mock<IInvoiceQuery>();
        query.Setup(q => q.GetById(99)).Returns((InvoiceDetail?)null);

        var service = new ElectronicInvoiceService(
            query.Object,
            new SimulatedElectronicInvoiceGateway(),
            Granted(Permissions.SalesInvoicesView));

        Assert.Throws<InvoiceNotFoundException>(() => service.Generate(99));
    }

    private static InvoiceDetail Detail(
        string customerName = "Ana Pérez",
        InvoiceStatus status = InvoiceStatus.Issued,
        decimal totalUnitPrice = 1500m) =>
        new(
            7,
            customerName,
            "900123456",
            IssueDate,
            status,
            new[]
            {
                new InvoiceLineRow("A1", "Arroz", 2, totalUnitPrice),
                new InvoiceLineRow("B2", "Aceite", 1, 500m)
            });

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
