using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Invoicing;

/// <summary>
/// Result of the simulated electronic invoice generation. The signature is an
/// example SHA-256 digest, not a certified digital signature.
/// </summary>
public sealed record ElectronicInvoiceDocument(
    string InvoiceNumber,
    string Xml,
    string Signature,
    string Provider,
    bool IsSimulated);

/// <summary>
/// Abstraction for an electronic invoice provider. The only implementation in
/// this project is the local simulator; a real DIAN integration would replace
/// it with a provider that signs, transmits and stores the CUFE.
/// </summary>
public interface IElectronicInvoiceGateway
{
    ElectronicInvoiceDocument Generate(InvoiceDetail invoice);
}

/// <summary>
/// Academic simulator. It builds a readable XML document and a deterministic
/// SHA-256 digest so the flow can be demonstrated without credentials.
/// THIS IS NOT A DIAN INTEGRATION: the XML has no CUFE, no valid digital
/// signature and no legal validity. The disclaimer is inside the document.
/// </summary>
public sealed class SimulatedElectronicInvoiceGateway : IElectronicInvoiceGateway
{
    public const string ProviderName = "Simulador academico TiendaFlow (NO DIAN)";

    private const string Disclaimer =
        "Documento simulado con fines academicos. NO es una factura electronica " +
        "valida ante la DIAN: no tiene CUFE ni firma digital certificada.";

    public ElectronicInvoiceDocument Generate(InvoiceDetail invoice)
    {
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidInvoiceException("A cancelled invoice cannot be sent as an electronic invoice.");

        var number = $"FV-{invoice.Id:D5}";
        var signature = Sign(invoice);

        var document = new XElement("Invoice",
            new XAttribute("number", number),
            new XAttribute("simulated", "true"),
            new XAttribute("provider", ProviderName),
            new XElement("Disclaimer", Disclaimer),
            new XElement("Customer",
                new XAttribute("document", invoice.CustomerDocument),
                new XAttribute("name", invoice.CustomerName)),
            new XElement("IssueDate", invoice.IssueDate.ToString("O")),
            new XElement("Lines",
                invoice.Lines.Select(line =>
                    new XElement("Line",
                        new XAttribute("sku", line.ProductSku),
                        new XAttribute("description", line.Description),
                        new XAttribute("quantity", line.Quantity),
                        new XAttribute("unitPrice", line.UnitPrice),
                        new XAttribute("total", line.LineTotal)))),
            new XElement("Total", invoice.Total),
            new XElement("Signature",
                new XAttribute("algorithm", "SHA-256"),
                new XAttribute("kind", "example-digest-not-certified"),
                signature));

        return new ElectronicInvoiceDocument(
            number,
            new XDocument(new XDeclaration("1.0", "utf-8", null), document).ToString(),
            signature,
            ProviderName,
            IsSimulated: true);
    }

    private static string Sign(InvoiceDetail invoice)
    {
        var canonical = string.Join(
            '|',
            invoice.Id,
            invoice.CustomerDocument,
            invoice.IssueDate.ToString("O"),
            invoice.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>
/// Permission guard for the simulated invoice. It is a read-only operation:
/// nothing is stored and no audit entry is written.
/// </summary>
public sealed class ElectronicInvoiceService
{
    private readonly IInvoiceQuery _invoices;
    private readonly IElectronicInvoiceGateway _gateway;
    private readonly IAccessControl _accessControl;

    public ElectronicInvoiceService(
        IInvoiceQuery invoices,
        IElectronicInvoiceGateway gateway,
        IAccessControl accessControl)
    {
        _invoices = invoices;
        _gateway = gateway;
        _accessControl = accessControl;
    }

    public ElectronicInvoiceDocument Generate(int invoiceId)
    {
        _accessControl.Demand(Permissions.SalesInvoicesView);

        var invoice = _invoices.GetById(invoiceId)
            ?? throw new InvoiceNotFoundException(invoiceId);

        return _gateway.Generate(invoice);
    }
}
