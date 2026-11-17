namespace TiendaFlow.Domain;

/// <summary>
/// One movement of the loyalty points ledger: points earned with an invoice
/// (positive) or redeemed (negative). The balance is the sum of the ledger.
/// </summary>
public class LoyaltyPointsEntry
{
    public LoyaltyPointsEntry(int customerId, int points, string reason, DateTime createdAt, int? invoiceId = null)
    {
        if (customerId <= 0)
            throw new InvalidCrmRecordException("The customer is required.");
        if (points == 0)
            throw new InvalidCrmRecordException("The points cannot be zero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidCrmRecordException("The reason is required.");

        CustomerId = customerId;
        Points = points;
        Reason = reason.Trim();
        CreatedAt = UtcTimestamp.Normalize(createdAt);
        InvoiceId = invoiceId;
    }

    public int Id { get; private set; }

    public int CustomerId { get; private set; }

    public int Points { get; private set; }

    public string Reason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public int? InvoiceId { get; private set; }
}

public enum PqrsType
{
    Petition = 1,
    Complaint = 2,
    Claim = 3,
    Suggestion = 4
}

public enum PqrsStatus
{
    Open = 1,
    InProgress = 2,
    Closed = 3
}

/// <summary>
/// Petition, complaint, claim or suggestion (PQRS). It gets a filing number,
/// a responsible person and a due date; it is closed with a response.
/// </summary>
public class PqrsCase
{
    public const int ResponseDays = 15;

    public PqrsCase(
        PqrsType type,
        string subject,
        string description,
        int? customerId,
        string? contact,
        DateTime createdAt)
    {
        if (!Enum.IsDefined(type))
            throw new InvalidCrmRecordException("The PQRS type is not valid.");
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidCrmRecordException("The subject is required.");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidCrmRecordException("The description is required.");
        if (customerId is null && string.IsNullOrWhiteSpace(contact))
            throw new InvalidCrmRecordException("A customer or a contact is required.");

        Type = type;
        Subject = subject.Trim();
        Description = description.Trim();
        CustomerId = customerId;
        Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        CreatedAt = UtcTimestamp.Normalize(createdAt);
        DueDate = DateOnly.FromDateTime(CreatedAt).AddDays(ResponseDays);
        Status = PqrsStatus.Open;
    }

    public int Id { get; private set; }

    public PqrsType Type { get; private set; }

    public string Subject { get; private set; }

    public string Description { get; private set; }

    public int? CustomerId { get; private set; }

    public string? Contact { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateOnly DueDate { get; private set; }

    public PqrsStatus Status { get; private set; }

    public string? Responsible { get; private set; }

    public string? Response { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public string FilingNumber => $"PQRS-{CreatedAt:yyyy}-{Id:D5}";

    public bool IsOverdue(DateOnly today) => Status != PqrsStatus.Closed && today > DueDate;

    public void Assign(string responsible)
    {
        if (Status == PqrsStatus.Closed)
            throw new InvalidCrmRecordException("A closed PQRS cannot be reassigned.");
        if (string.IsNullOrWhiteSpace(responsible))
            throw new InvalidCrmRecordException("The responsible person is required.");

        Responsible = responsible.Trim();
        Status = PqrsStatus.InProgress;
    }

    public void Close(string response, DateTime closedAt)
    {
        if (Status == PqrsStatus.Closed)
            throw new InvalidCrmRecordException("The PQRS is already closed.");
        if (string.IsNullOrWhiteSpace(response))
            throw new InvalidCrmRecordException("The response is required to close the PQRS.");

        Response = response.Trim();
        ClosedAt = UtcTimestamp.Normalize(closedAt);
        Status = PqrsStatus.Closed;
    }
}

/// <summary>
/// Marketing campaign aimed at one customer segment (A, B or C) during a
/// date range. Its result is measured with the invoices of those customers.
/// </summary>
public class MarketingCampaign
{
    public MarketingCampaign(string name, string segment, DateOnly startDate, DateOnly endDate, string message)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidCrmRecordException("The campaign name is required.");
        if (segment is not ("A" or "B" or "C"))
            throw new InvalidCrmRecordException("The segment must be A, B or C.");
        if (startDate == default || endDate == default)
            throw new InvalidCrmRecordException("The campaign dates are required.");
        if (endDate < startDate)
            throw new InvalidCrmRecordException("The campaign cannot end before it starts.");
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidCrmRecordException("The campaign message is required.");

        Name = name.Trim();
        Segment = segment;
        StartDate = startDate;
        EndDate = endDate;
        Message = message.Trim();
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public string Segment { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string Message { get; private set; }

    public bool Cancelled { get; private set; }

    public void Cancel() => Cancelled = true;
}

public class InvalidCrmRecordException : Exception
{
    public InvalidCrmRecordException(string message) : base(message) { }
}

public class CrmRecordNotFoundException : Exception
{
    public CrmRecordNotFoundException(string what, int id) : base($"{what} {id} was not found.") { }
}

/// <summary>
/// Permission granted to a role, stored when an administrator edits the
/// matrix. A role without stored rows keeps the default matrix of the code.
/// </summary>
public class RolePermissionGrant
{
    public RolePermissionGrant(int roleId, string permission)
    {
        if (roleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(roleId));
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("The permission is required.", nameof(permission));

        RoleId = roleId;
        Permission = permission.Trim();
    }

    public int RoleId { get; private set; }

    public string Permission { get; private set; }
}
