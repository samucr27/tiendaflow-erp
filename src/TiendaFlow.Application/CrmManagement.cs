using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public sealed record LoyaltyEarnRequest(
    int CustomerId,
    int StoreId,
    decimal PurchaseAmountCop,
    string Reference);

public sealed record LoyaltyRedeemRequest(
    int CustomerId,
    int StoreId,
    int Points,
    string Reference);

public sealed record PqrsCreateRequest(
    int CustomerId,
    int StoreId,
    PqrsType Type,
    string Subject,
    string Description);

public sealed record CampaignCreateRequest(
    string Name,
    string Description,
    CampaignChannel Channel,
    string Segment,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record LoyaltyRow(
    int Id,
    int CustomerId,
    string CustomerName,
    string CustomerDocument,
    LoyaltyMovementType Type,
    int Points,
    decimal AmountCop,
    string Reference,
    DateTime CreatedAt,
    int StoreId,
    string StoreName);

public sealed record LoyaltyBalanceRow(
    int CustomerId,
    string CustomerName,
    string CustomerDocument,
    int EarnedPoints,
    int RedeemedPoints,
    int Balance,
    decimal RedeemableCop);

public sealed record PqrsRow(
    int Id,
    int CustomerId,
    string CustomerName,
    string CustomerDocument,
    PqrsType Type,
    string Subject,
    string Description,
    PqrsStatus Status,
    DateOnly CreatedDate,
    DateOnly DueDate,
    bool IsOverdue,
    int StoreId,
    string StoreName,
    string? Response,
    DateTime? AnsweredAt);

public sealed record CampaignRow(
    int Id,
    string Name,
    string Description,
    CampaignChannel Channel,
    string Segment,
    DateOnly StartDate,
    DateOnly EndDate,
    CampaignStatus Status);

/// <summary>
/// Spanish labels shared by the CRM screens and their CSV exports.
/// </summary>
public static class CrmText
{
    public static string LoyaltyLabel(LoyaltyMovementType type) => type switch
    {
        LoyaltyMovementType.Earned => "Acumulación",
        LoyaltyMovementType.Redeemed => "Canje",
        _ => "—"
    };

    public static string PqrsLabel(PqrsType type) => type switch
    {
        PqrsType.Petition => "Petición",
        PqrsType.Complaint => "Queja",
        PqrsType.Claim => "Reclamo",
        PqrsType.Suggestion => "Sugerencia",
        PqrsType.Report => "Denuncia",
        _ => "—"
    };

    public static string PqrsStatusLabel(PqrsStatus status) => status switch
    {
        PqrsStatus.Open => "Abierto",
        PqrsStatus.InProgress => "En trámite",
        PqrsStatus.Answered => "Respondido",
        PqrsStatus.Closed => "Cerrado",
        _ => "—"
    };

    public static string CampaignChannelLabel(CampaignChannel channel) => channel switch
    {
        CampaignChannel.Email => "Correo",
        CampaignChannel.Sms => "SMS",
        CampaignChannel.WhatsApp => "WhatsApp",
        CampaignChannel.Social => "Redes sociales",
        _ => "—"
    };

    public static string CampaignStatusLabel(CampaignStatus status) => status switch
    {
        CampaignStatus.Draft => "Borrador",
        CampaignStatus.Active => "Activa",
        CampaignStatus.Paused => "Pausada",
        CampaignStatus.Closed => "Cerrada",
        _ => "—"
    };
}

/// <summary>
/// Customer loyalty ledger. Points are earned from a purchase amount and
/// redeemed only when the customer has enough balance. Both operations carry a
/// reference so the same sale cannot be entered twice, and both the movement and
/// its audit entry commit in one transaction.
/// </summary>
public sealed class LoyaltyService
{
    private readonly ILoyaltyRepository _movements;
    private readonly ICustomerRepository _customers;
    private readonly ICustomerQuery _customerQuery;
    private readonly IStoreRepository _stores;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IParameterValues _parameters;
    private readonly IUnitOfWork _unitOfWork;

    public LoyaltyService(
        ILoyaltyRepository movements,
        ICustomerRepository customers,
        ICustomerQuery customerQuery,
        IStoreRepository stores,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock,
        IParameterValues parameters,
        IUnitOfWork unitOfWork)
    {
        _movements = movements;
        _customers = customers;
        _customerQuery = customerQuery;
        _stores = stores;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
        _parameters = parameters;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<WorkforceStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        var scopedStoreId = CrmStoreScope.Restrict(_currentUser, null);

        return _stores.List()
            .Where(store => scopedStoreId is null || store.Id == scopedStoreId)
            .OrderBy(store => store.Name)
            .Select(store => new WorkforceStoreOption(store.Id, store.Name))
            .ToList();
    }

    public IReadOnlyList<CustomerRow> Customers()
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        // Customers are global; the store scope is enforced when the movement
        // itself is written, not when the picker is filled.
        return _customerQuery.List().Where(customer => customer.Active).ToList();
    }

    public IReadOnlyList<LoyaltyRow> List(
        DateOnly? from,
        DateOnly? to,
        int? storeId,
        int? customerId)
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        var scopedStoreId = CrmStoreScope.Restrict(_currentUser, storeId);
        var customers = _customerQuery.List().ToDictionary(customer => customer.Id);
        var stores = _stores.List().ToDictionary(store => store.Id);

        return _movements.List(from, to, scopedStoreId, customerId)
            .Select(movement =>
            {
                customers.TryGetValue(movement.CustomerId, out var customer);
                stores.TryGetValue(movement.StoreId, out var store);

                return new LoyaltyRow(
                    movement.Id,
                    movement.CustomerId,
                    customer?.FullName ?? "—",
                    customer?.DocumentNumber ?? "—",
                    movement.Type,
                    movement.Points,
                    movement.AmountCop,
                    movement.Reference,
                    movement.CreatedAt,
                    movement.StoreId,
                    store?.Name ?? "Sin tienda");
            })
            .ToList();
    }

    public IReadOnlyList<LoyaltyBalanceRow> Balances()
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        var scopedStoreId = CrmStoreScope.Restrict(_currentUser, null);
        var movements = _movements.List(null, null, scopedStoreId, null);
        var rate = _parameters.GetDecimal(SystemParameterCatalog.CopPerRedeemedPoint);

        return _customerQuery.List()
            .Where(customer => movements.Any(movement => movement.CustomerId == customer.Id))
            .Select(customer =>
            {
                var own = movements.Where(movement => movement.CustomerId == customer.Id).ToList();
                var earned = own
                    .Where(movement => movement.Type == LoyaltyMovementType.Earned)
                    .Sum(movement => movement.Points);
                var redeemed = own
                    .Where(movement => movement.Type == LoyaltyMovementType.Redeemed)
                    .Sum(movement => movement.Points);

                return new LoyaltyBalanceRow(
                    customer.Id,
                    customer.FullName,
                    customer.DocumentNumber,
                    earned,
                    redeemed,
                    earned - redeemed,
                    (earned - redeemed) * rate);
            })
            .OrderByDescending(row => row.Balance)
            .ThenBy(row => row.CustomerName)
            .ToList();
    }

    public int Earn(LoyaltyEarnRequest request)
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        var customer = ActiveCustomer(request.CustomerId);
        var store = ActiveStore(request.StoreId);
        CrmStoreScope.Demand(_currentUser, store.Id);

        if (request.PurchaseAmountCop <= 0)
            throw new InvalidLoyaltyMovementException("El valor de la compra debe ser mayor que cero.");

        var reference = request.Reference?.Trim() ?? string.Empty;

        if (reference.Length == 0)
            throw new InvalidLoyaltyMovementException("La referencia de la compra es obligatoria.");
        if (_movements.ReferenceExists(reference, LoyaltyMovementType.Earned))
            throw new LoyaltyReferenceAlreadyUsedException(reference);

        var copPerPoint = _parameters.GetDecimal(SystemParameterCatalog.CopPerLoyaltyPoint);
        var points = (int)Math.Floor(request.PurchaseAmountCop / copPerPoint);

        if (points < 1)
            throw new InvalidLoyaltyMovementException(
                $"La compra no alcanza para acumular un punto (1 punto = {copPerPoint:0.##} COP).");

        var movement = new LoyaltyMovement(
            0, customer.Id, LoyaltyMovementType.Earned, points,
            request.PurchaseAmountCop, store.Id, reference, _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _movements.Add(movement);

            Record(
                "LoyaltyPointsEarned",
                movement.Id,
                $"Cliente {customer.Id} acumuló {points} puntos por {reference} en la tienda {store.Id}");
        });

        return movement.Id;
    }

    public int Redeem(LoyaltyRedeemRequest request)
    {
        _accessControl.Demand(Permissions.CrmPointsManage);

        var customer = ActiveCustomer(request.CustomerId);
        var store = ActiveStore(request.StoreId);
        CrmStoreScope.Demand(_currentUser, store.Id);

        if (request.Points <= 0)
            throw new InvalidLoyaltyMovementException("Los puntos a canjear deben ser mayores que cero.");

        var reference = request.Reference?.Trim() ?? string.Empty;

        if (reference.Length == 0)
            throw new InvalidLoyaltyMovementException("La referencia del canje es obligatoria.");
        if (_movements.ReferenceExists(reference, LoyaltyMovementType.Redeemed))
            throw new LoyaltyReferenceAlreadyUsedException(reference);

        var balance = _movements.BalanceFor(customer.Id);

        if (balance < request.Points)
            throw new InvalidLoyaltyMovementException(
                $"Saldo insuficiente: el cliente tiene {balance} puntos y se pidieron {request.Points}.");

        var copPerPoint = _parameters.GetDecimal(SystemParameterCatalog.CopPerRedeemedPoint);
        var amountCop = request.Points * copPerPoint;

        var movement = new LoyaltyMovement(
            0, customer.Id, LoyaltyMovementType.Redeemed, request.Points,
            amountCop, store.Id, reference, _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _movements.Add(movement);

            Record(
                "LoyaltyPointsRedeemed",
                movement.Id,
                $"Cliente {customer.Id} canjeó {request.Points} puntos por {reference} en la tienda {store.Id}");
        });

        return movement.Id;
    }

    public string LedgerCsv(DateOnly? from, DateOnly? to, int? storeId, int? customerId)
    {
        var rows = List(from, to, storeId, customerId);

        Record("LoyaltyLedgerExported", 0, "Exportación del libro de puntos de fidelización");

        return CsvReport.Build(
            [
                "Fecha", "Cliente", "Documento", "Tipo", "Puntos",
                "Valor COP", "Referencia", "Tienda"
            ],
            rows.Select(row => new[]
            {
                CsvReport.Date(row.CreatedAt),
                row.CustomerName,
                row.CustomerDocument,
                CrmText.LoyaltyLabel(row.Type),
                row.Points.ToString(),
                CsvReport.Number(row.AmountCop),
                row.Reference,
                row.StoreName
            }));
    }

    private CustomerRow ActiveCustomer(int customerId) =>
        _customerQuery.GetById(customerId) is CustomerRow customer && customer.Active
            ? customer
            : throw new InvalidLoyaltyMovementException("El cliente no existe o está inactivo.");

    private Store ActiveStore(int storeId) =>
        _stores.GetById(storeId) is Store store && store.Active
            ? store
            : throw new InvalidLoyaltyMovementException("La tienda no existe o está inactiva.");

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "crm",
            ip: _currentUser.Ip));
}

/// <summary>
/// Customer requests (PQRS) with a response deadline in business days. A case
/// always belongs to the store that received it and moves forward only.
/// </summary>
public sealed class PqrsService
{
    private readonly IPqrsRepository _cases;
    private readonly ICustomerQuery _customers;
    private readonly IStoreRepository _stores;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IParameterValues _parameters;
    private readonly IUnitOfWork _unitOfWork;

    public PqrsService(
        IPqrsRepository cases,
        ICustomerQuery customers,
        IStoreRepository stores,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock,
        IParameterValues parameters,
        IUnitOfWork unitOfWork)
    {
        _cases = cases;
        _customers = customers;
        _stores = stores;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
        _parameters = parameters;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<WorkforceStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var scopedStoreId = CrmStoreScope.Restrict(_currentUser, null);

        return _stores.List()
            .Where(store => scopedStoreId is null || store.Id == scopedStoreId)
            .OrderBy(store => store.Name)
            .Select(store => new WorkforceStoreOption(store.Id, store.Name))
            .ToList();
    }

    public IReadOnlyList<CustomerRow> Customers()
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        // Customers are global; the store scope is enforced on the case itself.
        return _customers.List().Where(customer => customer.Active).ToList();
    }

    public IReadOnlyList<PqrsRow> List(int? storeId, PqrsStatus? status)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var scopedStoreId = CrmStoreScope.Restrict(_currentUser, storeId);
        var today = Today();

        return Map(_cases.List(scopedStoreId, status), today);
    }

    public PqrsRow Get(int id)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var item = Find(id);
        CrmStoreScope.Demand(_currentUser, item.StoreId);

        var customer = _customers.GetById(item.CustomerId);
        var store = _stores.GetById(item.StoreId);

        return new PqrsRow(
            item.Id,
            item.CustomerId,
            customer?.FullName ?? "—",
            customer?.DocumentNumber ?? "—",
            item.Type,
            item.Subject,
            item.Description,
            item.Status,
            item.CreatedDate,
            item.DueDate,
            item.IsOverdue(Today()),
            item.StoreId,
            store?.Name ?? "Sin tienda",
            item.Response,
            item.AnsweredAt);
    }

    public int Create(PqrsCreateRequest request)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var customer = ActiveCustomer(request.CustomerId);
        var store = ActiveStore(request.StoreId);
        CrmStoreScope.Demand(_currentUser, store.Id);

        var today = Today();
        var responseDays = _parameters.GetInt(SystemParameterCatalog.PqrsResponseDays);
        var dueDate = AddBusinessDays(today, responseDays);

        var item = new PqrsCase(
            0, customer.Id, store.Id, request.Type,
            request.Subject, request.Description,
            today, dueDate, _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _cases.Add(item);

            Record(
                "PqrsCreated",
                item.Id,
                $"Solicitud {item.Id} de tipo {item.Type} del cliente {customer.Id} en la tienda {store.Id}");
        });

        return item.Id;
    }

    public void Start(int id)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var item = Find(id);
        CrmStoreScope.Demand(_currentUser, item.StoreId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            item.Start();
            _cases.Update(item);
            Record("PqrsStarted", item.Id, $"Solicitud {item.Id} pasó a en trámite");
        });
    }

    public void Answer(int id, string response)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var item = Find(id);
        CrmStoreScope.Demand(_currentUser, item.StoreId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            item.Answer(response, _clock.UtcNow);
            _cases.Update(item);
            Record("PqrsAnswered", item.Id, $"Solicitud {item.Id} respondida");
        });
    }

    public void Close(int id)
    {
        _accessControl.Demand(Permissions.CrmPqrsManage);

        var item = Find(id);
        CrmStoreScope.Demand(_currentUser, item.StoreId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            item.Close();
            _cases.Update(item);
            Record("PqrsClosed", item.Id, $"Solicitud {item.Id} cerrada");
        });
    }

    public string CasesCsv(int? storeId, PqrsStatus? status)
    {
        var rows = List(storeId, status);

        Record("PqrsExported", 0, "Exportación de solicitudes PQRS");

        return CsvReport.Build(
            [
                "ID", "Fecha", "Cliente", "Documento", "Tipo", "Asunto",
                "Estado", "Fecha límite", "Vencida", "Tienda"
            ],
            rows.Select(row => new[]
            {
                row.Id.ToString(),
                row.CreatedDate.ToString("yyyy-MM-dd"),
                row.CustomerName,
                row.CustomerDocument,
                CrmText.PqrsLabel(row.Type),
                row.Subject,
                CrmText.PqrsStatusLabel(row.Status),
                row.DueDate.ToString("yyyy-MM-dd"),
                row.IsOverdue ? "Sí" : "No",
                row.StoreName
            }));
    }

    private PqrsCase Find(int id) =>
        _cases.GetById(id) ?? throw new PqrsNotFoundException(id);

    private IReadOnlyList<PqrsRow> Map(IReadOnlyList<PqrsCase> items, DateOnly today)
    {
        var customers = _customers.List().ToDictionary(customer => customer.Id);
        var stores = _stores.List().ToDictionary(store => store.Id);

        return items
            .Select(item =>
            {
                customers.TryGetValue(item.CustomerId, out var customer);
                stores.TryGetValue(item.StoreId, out var store);

                return new PqrsRow(
                    item.Id,
                    item.CustomerId,
                    customer?.FullName ?? "—",
                    customer?.DocumentNumber ?? "—",
                    item.Type,
                    item.Subject,
                    item.Description,
                    item.Status,
                    item.CreatedDate,
                    item.DueDate,
                    item.IsOverdue(today),
                    item.StoreId,
                    store?.Name ?? "Sin tienda",
                    item.Response,
                    item.AnsweredAt);
            })
            .OrderByDescending(row => row.CreatedDate)
            .ThenBy(row => row.Id)
            .ToList();
    }

    private CustomerRow ActiveCustomer(int customerId) =>
        _customers.GetById(customerId) is CustomerRow customer && customer.Active
            ? customer
            : throw new InvalidPqrsException("El cliente no existe o está inactivo.");

    private Store ActiveStore(int storeId) =>
        _stores.GetById(storeId) is Store store && store.Active
            ? store
            : throw new InvalidPqrsException("La tienda no existe o está inactiva.");

    private DateOnly Today() => DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private static DateOnly AddBusinessDays(DateOnly start, int days)
    {
        var result = start;
        var remaining = days;

        while (remaining > 0)
        {
            result = result.AddDays(1);

            if (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            remaining--;
        }

        return result;
    }

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "crm",
            ip: _currentUser.Ip));
}

/// <summary>
/// Marketing campaigns. Only the corporate manager creates and drives them, so
/// there is no store scope here: a campaign belongs to the whole chain.
/// </summary>
public sealed class CampaignService
{
    private readonly ICampaignRepository _campaigns;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CampaignService(
        ICampaignRepository campaigns,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<CampaignRow> List()
    {
        _accessControl.Demand(Permissions.CrmCampaignsManage);

        var today = Today();

        return _campaigns.List()
            .OrderByDescending(campaign => campaign.IsCurrentlyActive(today))
            .ThenBy(campaign => campaign.StartDate)
            .Select(campaign => new CampaignRow(
                campaign.Id,
                campaign.Name,
                campaign.Description,
                campaign.Channel,
                campaign.Segment,
                campaign.StartDate,
                campaign.EndDate,
                campaign.Status))
            .ToList();
    }

    public int Create(CampaignCreateRequest request)
    {
        _accessControl.Demand(Permissions.CrmCampaignsManage);

        var name = request.Name?.Trim() ?? string.Empty;

        if (_campaigns.ExistsName(name))
            throw new DuplicateCampaignNameException(name);

        var today = Today();

        if (request.StartDate < today)
            throw new InvalidCampaignException("La fecha de inicio no puede estar en el pasado.");

        var campaign = new Campaign(
            0,
            name,
            request.Description,
            request.Channel,
            request.Segment,
            request.StartDate,
            request.EndDate,
            _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _campaigns.Add(campaign);

            Record(
                "CampaignCreated",
                campaign.Id,
                $"Campaña {campaign.Name} para el segmento {campaign.Segment} del {campaign.StartDate:yyyy-MM-dd} al {campaign.EndDate:yyyy-MM-dd}");
        });

        return campaign.Id;
    }

    public void Activate(int id) => Change(id, "CampaignActivated", campaign =>
    {
        campaign.Activate(Today());
        return $"Campaña {campaign.Name} activada";
    });

    public void Pause(int id) => Change(id, "CampaignPaused", campaign =>
    {
        campaign.Pause();
        return $"Campaña {campaign.Name} pausada";
    });

    public void Resume(int id) => Change(id, "CampaignResumed", campaign =>
    {
        campaign.Resume();
        return $"Campaña {campaign.Name} reanudada";
    });

    public void Close(int id) => Change(id, "CampaignClosed", campaign =>
    {
        campaign.Close();
        return $"Campaña {campaign.Name} cerrada";
    });

    public string CampaignsCsv()
    {
        var rows = List();

        Record("CampaignsExported", 0, "Exportación de campañas de marketing");

        return CsvReport.Build(
            [
                "Nombre", "Descripción", "Canal", "Segmento",
                "Inicio", "Fin", "Estado"
            ],
            rows.Select(row => new[]
            {
                row.Name,
                row.Description,
                CrmText.CampaignChannelLabel(row.Channel),
                row.Segment,
                row.StartDate.ToString("yyyy-MM-dd"),
                row.EndDate.ToString("yyyy-MM-dd"),
                CrmText.CampaignStatusLabel(row.Status)
            }));
    }

    private void Change(int id, string action, Func<Campaign, string> change)
    {
        _accessControl.Demand(Permissions.CrmCampaignsManage);

        var campaign = _campaigns.GetById(id)
            ?? throw new CampaignNotFoundException(id);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var detail = change(campaign);
            _campaigns.Update(campaign);
            Record(action, campaign.Id, detail);
        });
    }

    private DateOnly Today() => DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "crm",
            ip: _currentUser.Ip));
}
