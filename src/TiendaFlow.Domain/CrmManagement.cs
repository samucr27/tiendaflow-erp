namespace TiendaFlow.Domain;

/// <summary>
/// Direction of a loyalty movement. Earned points come from a sale; redeemed
/// points are a discount already granted to the customer.
/// </summary>
public enum LoyaltyMovementType
{
    Earned = 1,
    Redeemed = 2
}

/// <summary>
/// One immutable entry of a customer's loyalty ledger. Points are never edited
/// or deleted: a correction is a new entry with its own reference, so the
/// balance can always be reconstructed from the ledger.
/// </summary>
public class LoyaltyMovement
{
    public const int MaxReferenceLength = 120;

    public LoyaltyMovement(
        int id,
        int customerId,
        LoyaltyMovementType type,
        int points,
        decimal amountCop,
        int storeId,
        string reference,
        DateTime createdAt)
    {
        if (id < 0)
            throw new InvalidLoyaltyMovementException("El identificador del movimiento no puede ser negativo.");
        if (customerId <= 0)
            throw new InvalidLoyaltyMovementException("El cliente es obligatorio.");
        if (type is not LoyaltyMovementType.Earned and not LoyaltyMovementType.Redeemed)
            throw new InvalidLoyaltyMovementException("El tipo de movimiento no es válido.");
        if (points <= 0)
            throw new InvalidLoyaltyMovementException("Los puntos deben ser mayores que cero.");
        if (amountCop <= 0)
            throw new InvalidLoyaltyMovementException("El valor en pesos debe ser mayor que cero.");
        if (storeId <= 0)
            throw new InvalidLoyaltyMovementException("La tienda es obligatoria.");

        var normalizedReference = reference?.Trim() ?? string.Empty;

        if (normalizedReference.Length == 0)
            throw new InvalidLoyaltyMovementException("La referencia del movimiento es obligatoria.");
        if (normalizedReference.Length > MaxReferenceLength)
            throw new InvalidLoyaltyMovementException(
                $"La referencia no puede superar los {MaxReferenceLength} caracteres.");

        Id = id;
        CustomerId = customerId;
        Type = type;
        Points = points;
        AmountCop = amountCop;
        StoreId = storeId;
        Reference = normalizedReference;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public int CustomerId { get; private set; }

    public LoyaltyMovementType Type { get; private set; }

    public int Points { get; private set; }

    public decimal AmountCop { get; private set; }

    public int StoreId { get; private set; }

    public string Reference { get; private set; }

    public DateTime CreatedAt { get; }

    public int SignedPoints => Type == LoyaltyMovementType.Earned ? Points : -Points;
}

/// <summary>
/// The four reasons a customer can contact the company plus a report. The
/// catalogue is fixed so reports do not depend on free text typed by an
/// operator.
/// </summary>
public enum PqrsType
{
    Petition = 1,
    Complaint = 2,
    Claim = 3,
    Suggestion = 4,
    Report = 5
}

public enum PqrsStatus
{
    Open = 1,
    InProgress = 2,
    Answered = 3,
    Closed = 4
}

/// <summary>
/// A customer request with a due date measured in business days. It moves
/// forward only (Open → InProgress → Answered → Closed); an answered case keeps
/// its response and the instant it was given.
/// </summary>
public class PqrsCase
{
    public const int MaxSubjectLength = 150;
    public const int MaxDescriptionLength = 4000;
    public const int MaxResponseLength = 4000;

    public PqrsCase(
        int id,
        int customerId,
        int storeId,
        PqrsType type,
        string subject,
        string description,
        DateOnly createdDate,
        DateOnly dueDate,
        DateTime createdAt)
    {
        if (id < 0)
            throw new InvalidPqrsException("El identificador del caso no puede ser negativo.");
        if (customerId <= 0)
            throw new InvalidPqrsException("El cliente es obligatorio.");
        if (storeId <= 0)
            throw new InvalidPqrsException("La tienda es obligatoria.");
        if (!Enum.IsDefined(type))
            throw new InvalidPqrsException("El tipo de solicitud no es válido.");

        var normalizedSubject = subject?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedSubject.Length == 0)
            throw new InvalidPqrsException("El asunto es obligatorio.");
        if (normalizedSubject.Length > MaxSubjectLength)
            throw new InvalidPqrsException($"El asunto no puede superar los {MaxSubjectLength} caracteres.");
        if (normalizedDescription.Length == 0)
            throw new InvalidPqrsException("La descripción es obligatoria.");
        if (normalizedDescription.Length > MaxDescriptionLength)
            throw new InvalidPqrsException(
                $"La descripción no puede superar los {MaxDescriptionLength} caracteres.");
        if (dueDate < createdDate)
            throw new InvalidPqrsException("La fecha límite no puede ser anterior a la de creación.");

        Id = id;
        CustomerId = customerId;
        StoreId = storeId;
        Type = type;
        Subject = normalizedSubject;
        Description = normalizedDescription;
        CreatedDate = createdDate;
        DueDate = dueDate;
        Status = PqrsStatus.Open;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public int CustomerId { get; private set; }

    public int StoreId { get; private set; }

    public PqrsType Type { get; private set; }

    public string Subject { get; private set; }

    public string Description { get; private set; }

    public PqrsStatus Status { get; private set; }

    public DateOnly CreatedDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    public string? Response { get; private set; }

    public DateTime? AnsweredAt { get; private set; }

    public DateTime CreatedAt { get; }

    public bool IsOverdue(DateOnly today) =>
        Status is not (PqrsStatus.Answered or PqrsStatus.Closed) && today > DueDate;

    public void Start()
    {
        if (Status != PqrsStatus.Open)
            throw new InvalidPqrsException("Solo se puede iniciar un caso que esté abierto.");

        Status = PqrsStatus.InProgress;
    }

    public void Answer(string response, DateTime at)
    {
        if (Status is not (PqrsStatus.Open or PqrsStatus.InProgress))
            throw new InvalidPqrsException("El caso ya tiene una respuesta registrada.");

        var normalized = response?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
            throw new InvalidPqrsException("Escribe la respuesta antes de guardarla.");
        if (normalized.Length > MaxResponseLength)
            throw new InvalidPqrsException(
                $"La respuesta no puede superar los {MaxResponseLength} caracteres.");

        Response = normalized;
        AnsweredAt = UtcTimestamp.Normalize(at);
        Status = PqrsStatus.Answered;
    }

    public void Close()
    {
        if (Status != PqrsStatus.Answered)
            throw new InvalidPqrsException("Solo se puede cerrar un caso que ya fue respondido.");

        Status = PqrsStatus.Closed;
    }
}

public enum CampaignChannel
{
    Email = 1,
    Sms = 2,
    WhatsApp = 3,
    Social = 4
}

public enum CampaignStatus
{
    Draft = 1,
    Active = 2,
    Paused = 3,
    Closed = 4
}

/// <summary>
/// A time-boxed marketing action aimed at one customer segment. A campaign can
/// only be edited while it is a draft; once active, its dates and audience stay
/// frozen so the history of what was sent cannot be rewritten.
/// </summary>
public class Campaign
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxSegmentLength = 20;

    public static readonly string[] Segments = ["A", "B", "C", "Todos"];

    public Campaign(
        int id,
        string name,
        string description,
        CampaignChannel channel,
        string segment,
        DateOnly startDate,
        DateOnly endDate,
        DateTime createdAt)
    {
        if (id < 0)
            throw new InvalidCampaignException("El identificador de la campaña no puede ser negativo.");
        if (!Enum.IsDefined(channel))
            throw new InvalidCampaignException("El canal de la campaña no es válido.");

        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;
        var normalizedSegment = segment?.Trim() ?? string.Empty;

        if (normalizedName.Length == 0)
            throw new InvalidCampaignException("El nombre de la campaña es obligatorio.");
        if (normalizedName.Length > MaxNameLength)
            throw new InvalidCampaignException($"El nombre no puede superar los {MaxNameLength} caracteres.");
        if (normalizedDescription.Length > MaxDescriptionLength)
            throw new InvalidCampaignException(
                $"La descripción no puede superar los {MaxDescriptionLength} caracteres.");
        if (!Segments.Contains(normalizedSegment, StringComparer.Ordinal))
            throw new InvalidCampaignException("El segmento de la campaña no es válido.");
        if (endDate <= startDate)
            throw new InvalidCampaignException("La fecha de fin debe ser posterior a la de inicio.");

        Id = id;
        Name = normalizedName;
        Description = normalizedDescription;
        Channel = channel;
        Segment = normalizedSegment;
        StartDate = startDate;
        EndDate = endDate;
        Status = CampaignStatus.Draft;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public CampaignChannel Channel { get; private set; }

    public string Segment { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public CampaignStatus Status { get; private set; }

    public DateTime CreatedAt { get; }

    public bool IsCurrentlyActive(DateOnly today) =>
        Status == CampaignStatus.Active && today >= StartDate && today <= EndDate;

    public void UpdateDetails(
        string name,
        string description,
        CampaignChannel channel,
        string segment,
        DateOnly startDate,
        DateOnly endDate)
    {
        EnsureDraft();

        var replacement = new Campaign(
            Id, name, description, channel, segment, startDate, endDate, CreatedAt);

        Name = replacement.Name;
        Description = replacement.Description;
        Channel = replacement.Channel;
        Segment = replacement.Segment;
        StartDate = replacement.StartDate;
        EndDate = replacement.EndDate;
    }

    public void Activate(DateOnly today)
    {
        EnsureDraft();

        if (EndDate < today)
            throw new InvalidCampaignException("No se puede activar una campaña que ya venció.");

        Status = CampaignStatus.Active;
    }

    public void Pause()
    {
        if (Status != CampaignStatus.Active)
            throw new InvalidCampaignException("Solo se puede pausar una campaña activa.");

        Status = CampaignStatus.Paused;
    }

    public void Resume()
    {
        if (Status != CampaignStatus.Paused)
            throw new InvalidCampaignException("Solo se puede reanudar una campaña pausada.");

        Status = CampaignStatus.Active;
    }

    public void Close()
    {
        if (Status is CampaignStatus.Closed or CampaignStatus.Draft)
            throw new InvalidCampaignException("Solo se puede cerrar una campaña activa o pausada.");

        Status = CampaignStatus.Closed;
    }

    private void EnsureDraft()
    {
        if (Status != CampaignStatus.Draft)
            throw new InvalidCampaignException("Solo se puede editar o activar una campaña en borrador.");
    }
}
