using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Crm;

public record PointsBalanceRow(int CustomerId, string DocumentNumber, string FullName, int Earned, int Redeemed)
{
    public int Balance => Earned - Redeemed;
}

public record PointsMovementRow(int Id, DateTime CreatedAt, int Points, string Reason, int? InvoiceId);

public record PqrsRow(
    int Id,
    string FilingNumber,
    PqrsType Type,
    string Subject,
    string Requester,
    DateTime CreatedAt,
    DateOnly DueDate,
    PqrsStatus Status,
    string? Responsible,
    bool Overdue);

public record PqrsDetail(PqrsRow Row, string Description, string? Response, DateTime? ClosedAt);

public record NewPqrsRequest(PqrsType Type, string Subject, string Description, int? CustomerId, string? Contact);

public record CampaignRow(
    int Id,
    string Name,
    string Segment,
    DateOnly StartDate,
    DateOnly EndDate,
    string Message,
    bool Cancelled,
    int TargetCustomers,
    int InvoicesInPeriod,
    decimal SalesInPeriod);

public record NewCampaignRequest(string Name, string Segment, DateOnly StartDate, DateOnly EndDate, string Message);

/// <summary>
/// Loyalty program hook used by invoicing: issuing an invoice to a registered
/// customer earns points and cancelling it reverses them.
/// </summary>
public interface ILoyaltyProgram
{
    void EarnForInvoice(SalesInvoice invoice);

    void ReverseForInvoice(SalesInvoice invoice);
}

/// <summary>
/// Loyalty points ledger: one point for every $1.000 of an issued invoice to
/// a registered, active customer; the cashier redeems points (crm.points.manage).
/// The balance is always the sum of the ledger, never a stored number.
/// </summary>
public sealed class LoyaltyService : ILoyaltyProgram
{
    public const decimal PesosPerPoint = 1000m;

    private readonly IEntityStore<LoyaltyPointsEntry> _ledger;
    private readonly ICustomerRepository _customers;
    private readonly ICustomerQuery _customerQuery;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public LoyaltyService(
        IEntityStore<LoyaltyPointsEntry> ledger,
        ICustomerRepository customers,
        ICustomerQuery customerQuery,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _ledger = ledger;
        _customers = customers;
        _customerQuery = customerQuery;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public static int PointsFor(decimal total) => (int)Math.Floor(total / PesosPerPoint);

    public void EarnForInvoice(SalesInvoice invoice)
    {
        var customer = _customers.GetByDocument(invoice.CustomerDocument);
        var points = PointsFor(invoice.Total);

        if (customer is null || !customer.Active || points <= 0)
            return;

        _ledger.Add(new LoyaltyPointsEntry(customer.Id, points, $"Factura #{invoice.Id}", _clock.UtcNow, invoice.Id));
    }

    public void ReverseForInvoice(SalesInvoice invoice)
    {
        var earned = _ledger.All().Where(entry => entry.InvoiceId == invoice.Id).ToList();
        var net = earned.Sum(entry => entry.Points);

        if (net <= 0)
            return;

        _ledger.Add(new LoyaltyPointsEntry(
            earned[0].CustomerId, -net, $"Anulación de la factura #{invoice.Id}", _clock.UtcNow, invoice.Id));
    }

    public IReadOnlyList<PointsBalanceRow> Balances()
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        var byCustomer = _ledger.All().GroupBy(entry => entry.CustomerId).ToDictionary(group => group.Key);

        return _customerQuery.List()
            .Select(customer =>
            {
                byCustomer.TryGetValue(customer.Id, out var entries);
                var earned = entries?.Where(entry => entry.Points > 0).Sum(entry => entry.Points) ?? 0;
                var redeemed = -(entries?.Where(entry => entry.Points < 0).Sum(entry => entry.Points) ?? 0);
                return new PointsBalanceRow(customer.Id, customer.DocumentNumber, customer.FullName, earned, redeemed);
            })
            .OrderByDescending(row => row.Balance)
            .ThenBy(row => row.FullName)
            .ToList();
    }

    public IReadOnlyList<PointsMovementRow> Movements(int customerId)
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        return _ledger.All()
            .Where(entry => entry.CustomerId == customerId)
            .OrderByDescending(entry => entry.CreatedAt)
            .Select(entry => new PointsMovementRow(entry.Id, entry.CreatedAt, entry.Points, entry.Reason, entry.InvoiceId))
            .ToList();
    }

    public void Redeem(int customerId, int points, string reason)
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        if (points <= 0)
            throw new InvalidCrmRecordException("The points to redeem must be greater than zero.");

        _ = _customers.GetById(customerId) ?? throw new CustomerNotFoundException(customerId);
        var balance = _ledger.All().Where(entry => entry.CustomerId == customerId).Sum(entry => entry.Points);

        if (points > balance)
            throw new InvalidCrmRecordException($"The customer only has {balance} points.");

        var entry = new LoyaltyPointsEntry(customerId, -points, reason, _clock.UtcNow);
        _ledger.Add(entry);
        _auditLog.Record(new AuditEntry(
            "LoyaltyPointsRedeemed", entry.Id.ToString(),
            $"Canje de {points} puntos del cliente {customerId}",
            _currentUser.UserId, _clock.UtcNow, module: "crm", ip: _currentUser.Ip));
    }
}

/// <summary>
/// Petitions, complaints, claims and suggestions (crm.pqrs.manage). Each case
/// has a filing number and is due 15 days after it is filed.
/// </summary>
public sealed class PqrsService
{
    private readonly IEntityStore<PqrsCase> _cases;
    private readonly ICustomerQuery _customers;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public PqrsService(
        IEntityStore<PqrsCase> cases,
        ICustomerQuery customers,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _cases = cases;
        _customers = customers;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<PqrsRow> List()
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);
        var names = _customers.List().ToDictionary(customer => customer.Id, customer => customer.FullName);
        var today = DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

        return _cases.All()
            .OrderBy(item => item.Status == PqrsStatus.Closed)
            .ThenBy(item => item.DueDate)
            .Select(item => ToRow(item, names, today))
            .ToList();
    }

    public PqrsDetail Get(int id)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);
        var item = _cases.Get(id) ?? throw new CrmRecordNotFoundException("PQRS", id);
        var names = _customers.List().ToDictionary(customer => customer.Id, customer => customer.FullName);
        var today = DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));
        return new PqrsDetail(ToRow(item, names, today), item.Description, item.Response, item.ClosedAt);
    }

    public int File(NewPqrsRequest request)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var item = new PqrsCase(request.Type, request.Subject, request.Description,
            request.CustomerId, request.Contact, _clock.UtcNow);
        _cases.Add(item);
        Audit("PqrsFiled", item.Id, $"{item.FilingNumber} radicada ({item.Type})");
        return item.Id;
    }

    public void Assign(int id, string responsible)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);
        var item = _cases.Get(id) ?? throw new CrmRecordNotFoundException("PQRS", id);
        item.Assign(responsible);
        _cases.Update(item);
        Audit("PqrsAssigned", item.Id, $"{item.FilingNumber} asignada");
    }

    public void Close(int id, string response)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);
        var item = _cases.Get(id) ?? throw new CrmRecordNotFoundException("PQRS", id);
        item.Close(response, _clock.UtcNow);
        _cases.Update(item);
        Audit("PqrsClosed", item.Id, $"{item.FilingNumber} cerrada");
    }

    private static PqrsRow ToRow(PqrsCase item, IReadOnlyDictionary<int, string> names, DateOnly today) =>
        new(item.Id, item.FilingNumber, item.Type, item.Subject,
            item.CustomerId is int id && names.TryGetValue(id, out var name) ? name : item.Contact ?? "—",
            item.CreatedAt, item.DueDate, item.Status, item.Responsible, item.IsOverdue(today));

    private void Audit(string action, int id, string detail) =>
        _auditLog.Record(new AuditEntry(action, id.ToString(), detail, _currentUser.UserId,
            _clock.UtcNow, module: "crm", ip: _currentUser.Ip));
}

/// <summary>
/// Campaigns on a customer segment (crm.campaigns.manage). The result counts
/// the invoices that the segment's customers received during the campaign.
/// </summary>
public sealed class CampaignService
{
    private readonly IEntityStore<MarketingCampaign> _campaigns;
    private readonly CustomerSegmentationService _segmentation;
    private readonly IInvoiceQuery _invoices;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public CampaignService(
        IEntityStore<MarketingCampaign> campaigns,
        CustomerSegmentationService segmentation,
        IInvoiceQuery invoices,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _campaigns = campaigns;
        _segmentation = segmentation;
        _invoices = invoices;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public IReadOnlyList<CampaignRow> List()
    {
        _accessControl.Demand(Permissions.CrmCampaignsManage);

        var segments = _segmentation.Get().Rows;
        var invoices = _invoices.List().Where(invoice => invoice.Status != InvoiceStatus.Cancelled).ToList();

        return _campaigns.All()
            .OrderByDescending(campaign => campaign.StartDate)
            .Select(campaign =>
            {
                var documents = segments
                    .Where(row => row.Segment.ToString() == campaign.Segment)
                    .Select(row => row.DocumentNumber)
                    .ToHashSet(StringComparer.Ordinal);
                var inPeriod = invoices
                    .Where(invoice => documents.Contains(invoice.CustomerDocument))
                    .Where(invoice =>
                    {
                        var date = DateOnly.FromDateTime(LocalTime.ToLocal(invoice.IssueDate));
                        return date >= campaign.StartDate && date <= campaign.EndDate;
                    })
                    .ToList();

                return new CampaignRow(campaign.Id, campaign.Name, campaign.Segment, campaign.StartDate,
                    campaign.EndDate, campaign.Message, campaign.Cancelled, documents.Count,
                    inPeriod.Count, inPeriod.Sum(invoice => invoice.Total));
            })
            .ToList();
    }

    public int Create(NewCampaignRequest request)
    {
        _accessControl.Demand(Permissions.CrmCampaignsManage);
        var campaign = new MarketingCampaign(request.Name, request.Segment, request.StartDate, request.EndDate, request.Message);
        _campaigns.Add(campaign);
        Audit("CampaignCreated", campaign.Id, $"Campaña {campaign.Id} para el segmento {campaign.Segment}");
        return campaign.Id;
    }

    public void Cancel(int id)
    {
        _accessControl.Demand(Permissions.CrmCampaignsManage);
        var campaign = _campaigns.Get(id) ?? throw new CrmRecordNotFoundException("Campaign", id);
        campaign.Cancel();
        _campaigns.Update(campaign);
        Audit("CampaignCancelled", campaign.Id, $"Campaña {campaign.Id} cancelada");
    }

    private void Audit(string action, int id, string detail) =>
        _auditLog.Record(new AuditEntry(action, id.ToString(), detail, _currentUser.UserId,
            DateTime.UtcNow, module: "crm", ip: _currentUser.Ip));
}
