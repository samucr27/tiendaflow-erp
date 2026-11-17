namespace TiendaFlow.Domain;

public enum EmploymentContractStatus
{
    Active = 1,
    Extended = 2,
    Terminated = 3
}

/// <summary>
/// One employment contract of an employee. A renewal does not overwrite the
/// previous contract: it marks it as extended and creates a new active row with
/// the new dates, so the history of the relationship stays auditable.
/// </summary>
public class EmploymentContract
{
    public EmploymentContract(
        int id,
        int employeeId,
        ContractType contractType,
        DateOnly startDate,
        DateOnly? endDate,
        decimal baseSalary,
        DateTime createdAt)
    {
        Validate(employeeId, contractType, startDate, endDate, baseSalary);

        Id = id;
        EmployeeId = employeeId;
        ContractType = contractType;
        StartDate = startDate;
        EndDate = endDate;
        BaseSalary = baseSalary;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
        Status = EmploymentContractStatus.Active;
    }

    public int Id { get; private set; }

    public int EmployeeId { get; private set; }

    public ContractType ContractType { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public decimal BaseSalary { get; private set; }

    public EmploymentContractStatus Status { get; private set; }

    public int? PreviousContractId { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime? TerminatedAt { get; private set; }

    public void MarkExtended()
    {
        RequireActive("extend");

        Status = EmploymentContractStatus.Extended;
    }

    public void Terminate(DateTime date)
    {
        RequireActive("terminate");

        Status = EmploymentContractStatus.Terminated;
        TerminatedAt = UtcTimestamp.Normalize(date);
    }

    /// <summary>
    /// The renewal is a brand-new contract that points to the one it replaces.
    /// The previous row is extended in the same unit of work.
    /// </summary>
    public EmploymentContract Renew(
        DateOnly startDate,
        DateOnly? endDate,
        decimal baseSalary,
        DateTime createdAt)
    {
        RequireActive("renew");

        if (startDate <= StartDate)
            throw new InvalidEmploymentContractException(
                "La fecha de inicio de la prórroga debe ser posterior a la del contrato actual.");

        if (EndDate is DateOnly currentEnd && startDate < currentEnd)
            throw new InvalidEmploymentContractException(
                "La prórroga no puede empezar antes de que termine el contrato actual.");

        var renewed = new EmploymentContract(
            id: 0,
            employeeId: EmployeeId,
            contractType: ContractType,
            startDate,
            endDate,
            baseSalary,
            createdAt);

        renewed.PreviousContractId = Id;

        return renewed;
    }

    public bool IsExpiring(DateOnly today, int alertDays) =>
        Status == EmploymentContractStatus.Active
        && EndDate is DateOnly end
        && end >= today
        && end <= today.AddDays(alertDays);

    private void RequireActive(string action)
    {
        if (Status != EmploymentContractStatus.Active)
            throw new InvalidEmploymentContractException(
                $"No se puede {action} un contrato en estado {Status}.");
    }

    private static void Validate(
        int employeeId,
        ContractType contractType,
        DateOnly startDate,
        DateOnly? endDate,
        decimal baseSalary)
    {
        if (employeeId <= 0)
            throw new InvalidEmploymentContractException("El contrato requiere un empleado.");
        if (!Enum.IsDefined(contractType))
            throw new InvalidEmploymentContractException("El tipo de contrato no es válido.");
        if (startDate == default)
            throw new InvalidEmploymentContractException("La fecha de inicio es obligatoria.");
        if (endDate is DateOnly end && end < startDate)
            throw new InvalidEmploymentContractException(
                "La fecha de fin no puede ser anterior a la de inicio.");
        if (contractType != ContractType.Indefinite && endDate is null)
            throw new InvalidEmploymentContractException(
                "Los contratos a término fijo, obra o labor y prestación de servicios requieren fecha de fin.");
        if (baseSalary <= 0m)
            throw new InvalidEmploymentContractException("El salario base debe ser mayor que cero.");
    }
}

/// <summary>
/// Metadata and bytes of a contract attachment. The 2 MB limit keeps the file
/// inside SQL Server without a separate storage service; only PDF, PNG and JPG
/// are accepted.
/// </summary>
public class ContractAttachment
{
    public const int MaxSizeBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg" };

    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    public ContractAttachment(
        int id,
        int contractId,
        string fileName,
        string contentType,
        byte[] content,
        int? uploadedByUserId,
        DateTime uploadedAt)
    {
        if (contractId <= 0)
            throw new InvalidContractAttachmentException("El adjunto requiere un contrato.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidContractAttachmentException("El nombre del archivo es obligatorio.");

        var name = Path.GetFileName(fileName.Trim());
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var normalizedContentType = (contentType ?? string.Empty).Trim().ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new InvalidContractAttachmentException(
                "Solo se admiten archivos PDF, PNG o JPG.");
        if (!AllowedContentTypes.Contains(normalizedContentType))
            throw new InvalidContractAttachmentException(
                "El tipo de contenido del archivo no es válido.");
        if (content is null || content.Length == 0)
            throw new InvalidContractAttachmentException("El archivo está vacío.");
        if (content.Length > MaxSizeBytes)
            throw new InvalidContractAttachmentException(
                "El archivo supera el límite de 2 MB.");

        Id = id;
        ContractId = contractId;
        FileName = name;
        ContentType = normalizedContentType;
        Content = content;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = UtcTimestamp.Normalize(uploadedAt);
    }

    public int Id { get; private set; }

    public int ContractId { get; private set; }

    public string FileName { get; }

    public string ContentType { get; }

    public byte[] Content { get; }

    public int? UploadedByUserId { get; }

    public DateTime UploadedAt { get; }

    public int SizeBytes => Content.Length;
}
