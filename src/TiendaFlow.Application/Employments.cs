using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public sealed record ContractRow(
    int Id,
    int EmployeeId,
    string EmployeeName,
    string IdNumber,
    string Position,
    ContractType ContractType,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal BaseSalary,
    EmploymentContractStatus Status,
    int? PreviousContractId,
    int AttachmentCount,
    DateTime CreatedAt,
    DateTime? TerminatedAt)
{
    public string ContractTypeLabel => ContractTypeLabels.ToLabel(ContractType);

    public string StatusLabel => Status switch
    {
        EmploymentContractStatus.Active => "Vigente",
        EmploymentContractStatus.Extended => "Prorrogado",
        EmploymentContractStatus.Terminated => "Terminado",
        _ => Status.ToString()
    };
}

public sealed record ContractAttachmentRow(
    int Id,
    string FileName,
    string ContentType,
    int SizeBytes,
    DateTime UploadedAt,
    string UploadedBy);

public sealed record ContractDetail(
    ContractRow Contract,
    IReadOnlyList<ContractAttachmentRow> Attachments,
    IReadOnlyList<ContractRow> History);

public sealed record EmployeeContractOption(
    int Id,
    string Name,
    string IdNumber,
    string Position,
    bool HasActiveContract);

public sealed record CreateContractRequest(
    int EmployeeId,
    ContractType ContractType,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal BaseSalary);

public sealed record RenewContractRequest(
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal BaseSalary);

public sealed record ContractAttachmentUpload(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record ContractAttachmentDownload(
    string FileName,
    string ContentType,
    byte[] Content);

public interface IEmploymentContractRepository
{
    EmploymentContract? GetById(int id);

    IReadOnlyList<EmploymentContract> ListByEmployee(int employeeId);

    EmploymentContract? GetActiveByEmployee(int employeeId);

    void Add(EmploymentContract contract);

    void Update(EmploymentContract contract);

    void AddAttachment(ContractAttachment attachment);

    ContractAttachment? GetAttachment(int id);
}

public interface IEmploymentContractQuery
{
    IReadOnlyList<ContractRow> List();

    ContractDetail? GetById(int id);

    IReadOnlyList<EmployeeContractOption> ListEmployees();
}

/// <summary>
/// Contract registry of the HR module. A renewal closes the current contract as
/// extended and creates the new active one in the same transaction; an
/// attachment download is audited because the file can contain personal data.
/// </summary>
public sealed class EmploymentContractService
{
    private readonly IEmploymentContractRepository _contracts;
    private readonly IEmploymentContractQuery _query;
    private readonly IEmployeeRepository _employees;
    private readonly IParameterValues _parameters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public EmploymentContractService(
        IEmploymentContractRepository contracts,
        IEmploymentContractQuery query,
        IEmployeeRepository employees,
        IParameterValues parameters,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _contracts = contracts;
        _query = query;
        _employees = employees;
        _parameters = parameters;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<ContractRow> List()
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        return _query.List();
    }

    public ContractDetail Get(int id)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        return _query.GetById(id) ?? throw new EmploymentContractNotFoundException(id);
    }

    public IReadOnlyList<EmployeeContractOption> Employees()
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        return _query.ListEmployees();
    }

    /// <summary>
    /// Contracts whose end date falls inside the configurable alert window.
    /// The window comes from hr.contracts.expiry_alert_days.
    /// </summary>
    public IReadOnlyList<ContractRow> ExpiringAlerts()
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        var days = _parameters.GetInt(SystemParameterCatalog.ContractExpiryAlertDays);
        var today = Today();

        return _query.List()
            .Where(contract =>
                contract.Status == EmploymentContractStatus.Active
                && contract.EndDate is DateOnly end
                && end >= today
                && end <= today.AddDays(days))
            .OrderBy(contract => contract.EndDate)
            .ToList();
    }

    public int Create(CreateContractRequest request)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        var employee = _employees.GetById(request.EmployeeId)
            ?? throw new InvalidEmploymentContractException("El empleado seleccionado no existe.");

        if (!employee.Active)
            throw new InvalidEmploymentContractException(
                "No se puede crear un contrato para un empleado inactivo.");

        if (_contracts.GetActiveByEmployee(employee.Id) is not null)
            throw new InvalidEmploymentContractException(
                "El empleado ya tiene un contrato vigente. Usa la prórroga para renovarlo.");

        var contract = new EmploymentContract(
            id: 0,
            employeeId: employee.Id,
            request.ContractType,
            request.StartDate,
            request.EndDate,
            request.BaseSalary,
            _clock.UtcNow);

        _contracts.Add(contract);

        Record(
            "ContractCreated",
            contract.Id,
            $"Contrato {contract.ContractType} del empleado {employee.IdNumber} desde {contract.StartDate:yyyy-MM-dd}");

        return contract.Id;
    }

    public int Renew(int id, RenewContractRequest request)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        var current = _contracts.GetById(id)
            ?? throw new EmploymentContractNotFoundException(id);

        var renewed = current.Renew(
            request.StartDate,
            request.EndDate,
            request.BaseSalary,
            _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            current.MarkExtended();
            _contracts.Update(current);
            _contracts.Add(renewed);
        });

        Record(
            "ContractRenewed",
            renewed.Id,
            $"Contrato {id} prorrogado con el contrato {renewed.Id} desde {renewed.StartDate:yyyy-MM-dd}");

        return renewed.Id;
    }

    public void Terminate(int id)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        var contract = _contracts.GetById(id)
            ?? throw new EmploymentContractNotFoundException(id);

        contract.Terminate(_clock.UtcNow);
        _contracts.Update(contract);

        Record("ContractTerminated", contract.Id, $"Contrato {contract.Id} terminado");
    }

    public int Attach(int contractId, ContractAttachmentUpload upload)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        _ = _contracts.GetById(contractId)
            ?? throw new EmploymentContractNotFoundException(contractId);

        var attachment = new ContractAttachment(
            id: 0,
            contractId,
            upload.FileName,
            upload.ContentType,
            upload.Content,
            _currentUser.UserId,
            _clock.UtcNow);

        _contracts.AddAttachment(attachment);

        Record(
            "ContractAttachmentUploaded",
            attachment.Id,
            $"Adjunto {attachment.FileName} ({attachment.SizeBytes} bytes) del contrato {contractId}");

        return attachment.Id;
    }

    public ContractAttachmentDownload DownloadAttachment(int contractId, int attachmentId)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        var attachment = _contracts.GetAttachment(attachmentId)
            ?? throw new ContractAttachmentNotFoundException(attachmentId);

        if (attachment.ContractId != contractId)
            throw new ContractAttachmentNotFoundException(attachmentId);

        Record(
            "ContractAttachmentDownloaded",
            attachment.Id,
            $"Descarga del adjunto {attachment.FileName} del contrato {attachment.ContractId}");

        return new ContractAttachmentDownload(
            attachment.FileName,
            attachment.ContentType,
            attachment.Content);
    }

    private DateOnly Today() => DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));
}
