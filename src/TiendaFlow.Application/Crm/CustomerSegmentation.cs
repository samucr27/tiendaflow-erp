using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Crm;

public enum CustomerSegment
{
    A,
    B,
    C
}

public sealed record CustomerSegmentRow(
    int CustomerId,
    string DocumentNumber,
    string FullName,
    int InvoiceCount,
    decimal TotalPurchased,
    DateTime? LastPurchaseDate,
    CustomerSegment Segment)
{
    public decimal AverageTicket =>
        InvoiceCount == 0 ? 0m : TotalPurchased / InvoiceCount;
}

public sealed record CustomerSegmentation(IReadOnlyList<CustomerSegmentRow> Rows)
{
    public int CountA => Rows.Count(row => row.Segment == CustomerSegment.A);

    public int CountB => Rows.Count(row => row.Segment == CustomerSegment.B);

    public int CountC => Rows.Count(row => row.Segment == CustomerSegment.C);

    public decimal TotalPurchased => Rows.Sum(row => row.TotalPurchased);
}

/// <summary>
/// A/B/C segmentation by monetary volume. Customers with purchases are ranked
/// from highest to lowest billed amount: the top 20 % is A, the next 30 % is B
/// and the rest is C. Customers without purchases always fall in C. The rule
/// is deliberately explicit and documented: the business thresholds are a
/// reference and are not legally validated.
/// </summary>
public sealed class CustomerSegmentationService
{
    private const decimal APercent = 0.20m;
    private const decimal BPercent = 0.30m;

    private readonly ICustomerQuery _customers;
    private readonly IInvoiceQuery _invoices;
    private readonly IAccessControl _accessControl;

    public CustomerSegmentationService(
        ICustomerQuery customers,
        IInvoiceQuery invoices,
        IAccessControl accessControl)
    {
        _customers = customers;
        _invoices = invoices;
        _accessControl = accessControl;
    }

    public CustomerSegmentation Get()
    {
        _accessControl.Demand(Permissions.CrmReportsView);

        var purchases = _invoices
            .List()
            .Where(invoice => invoice.Status != InvoiceStatus.Cancelled)
            .GroupBy(invoice => invoice.CustomerDocument, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Count = group.Count(),
                    Total = group.Sum(invoice => invoice.Total),
                    Last = group.Max(invoice => invoice.IssueDate)
                },
                StringComparer.Ordinal);

        var rows = _customers
            .List()
            .Select(customer =>
            {
                purchases.TryGetValue(customer.DocumentNumber, out var purchase);

                return new CustomerSegmentRow(
                    customer.Id,
                    customer.DocumentNumber,
                    customer.FullName,
                    purchase?.Count ?? 0,
                    purchase?.Total ?? 0m,
                    purchase?.Last,
                    CustomerSegment.C);
            })
            .ToList();

        var ranked = rows
            .Where(row => row.TotalPurchased > 0m)
            .OrderByDescending(row => row.TotalPurchased)
            .ThenBy(row => row.FullName)
            .ToList();

        var aLimit = (int)Math.Ceiling(ranked.Count * APercent);
        var bLimit = (int)Math.Ceiling(ranked.Count * BPercent);

        for (var index = 0; index < ranked.Count; index++)
        {
            var segment = index < aLimit
                ? CustomerSegment.A
                : index < aLimit + bLimit
                    ? CustomerSegment.B
                    : CustomerSegment.C;

            var position = rows.IndexOf(ranked[index]);
            rows[position] = ranked[index] with { Segment = segment };
        }

        return new CustomerSegmentation(rows
            .OrderBy(row => row.Segment)
            .ThenByDescending(row => row.TotalPurchased)
            .ThenBy(row => row.FullName)
            .ToList());
    }
}
