using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfEmploymentContractRepository : IEmploymentContractRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfEmploymentContractRepository(TiendaFlowDbContext context) => _context = context;

    public EmploymentContract? GetById(int id) =>
        _context.LoadFresh<EmploymentContract>(
            contract => contract.Id == id,
            () => _context.EmployeeContracts.FirstOrDefault(contract => contract.Id == id));

    public IReadOnlyList<EmploymentContract> ListByEmployee(int employeeId) =>
        _context.EmployeeContracts
            .AsNoTracking()
            .Where(contract => contract.EmployeeId == employeeId)
            .OrderByDescending(contract => contract.StartDate)
            .ToList();

    public EmploymentContract? GetActiveByEmployee(int employeeId) =>
        _context.EmployeeContracts
            .AsNoTracking()
            .Where(contract => contract.EmployeeId == employeeId
                && contract.Status == EmploymentContractStatus.Active)
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefault();

    public void Add(EmploymentContract contract)
    {
        _context.EmployeeContracts.Add(contract);
        _context.SaveChanges();
    }

    public void Update(EmploymentContract contract)
    {
        if (_context.Entry(contract).State == EntityState.Detached)
            _context.EmployeeContracts.Update(contract);

        _context.SaveChanges();
    }

    public void AddAttachment(ContractAttachment attachment)
    {
        _context.ContractAttachments.Add(attachment);
        _context.SaveChanges();
    }

    public ContractAttachment? GetAttachment(int id) =>
        _context.ContractAttachments
            .AsNoTracking()
            .FirstOrDefault(attachment => attachment.Id == id);
}

public sealed class EfEmploymentContractQuery : IEmploymentContractQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfEmploymentContractQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<ContractRow> List()
    {
        var contracts = _context.EmployeeContracts
            .AsNoTracking()
            .OrderByDescending(contract => contract.StartDate)
            .ThenByDescending(contract => contract.Id)
            .ToList();

        return BuildRows(contracts);
    }

    public ContractDetail? GetById(int id)
    {
        var contract = _context.EmployeeContracts
            .AsNoTracking()
            .FirstOrDefault(item => item.Id == id);

        if (contract is null)
            return null;

        var row = BuildRows(new[] { contract })[0];

        var attachments = _context.ContractAttachments
            .AsNoTracking()
            .Where(attachment => attachment.ContractId == id)
            .OrderByDescending(attachment => attachment.UploadedAt)
            .ToList();

        var authorNames = BuildAuthorNames(attachments);

        var attachmentRows = attachments
            .Select(attachment => new ContractAttachmentRow(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.Content.Length,
                attachment.UploadedAt,
                attachment.UploadedByUserId is int userId
                    && authorNames.TryGetValue(userId, out var author)
                        ? author
                        : "—"))
            .ToList();

        var history = BuildRows(_context.EmployeeContracts
            .AsNoTracking()
            .Where(item => item.EmployeeId == contract.EmployeeId)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.Id)
            .ToList());

        return new ContractDetail(row, attachmentRows, history);
    }

    public IReadOnlyList<EmployeeContractOption> ListEmployees()
    {
        var activeEmployeeIds = _context.EmployeeContracts
            .AsNoTracking()
            .Where(contract => contract.Status == EmploymentContractStatus.Active)
            .Select(contract => contract.EmployeeId)
            .ToHashSet();

        return _context.Employees
            .AsNoTracking()
            .OrderBy(employee => employee.Name)
            .ToList()
            .Select(employee => new EmployeeContractOption(
                employee.Id,
                employee.Name,
                employee.IdNumber,
                employee.Position,
                activeEmployeeIds.Contains(employee.Id)))
            .ToList();
    }

    private IReadOnlyList<ContractRow> BuildRows(IReadOnlyList<EmploymentContract> contracts)
    {
        if (contracts.Count == 0)
            return Array.Empty<ContractRow>();

        var employeeIds = contracts
            .Select(contract => contract.EmployeeId)
            .Distinct()
            .ToList();

        var employees = _context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .ToDictionary(employee => employee.Id);

        var contractIds = contracts.Select(contract => contract.Id).ToList();

        var attachmentCounts = _context.ContractAttachments
            .AsNoTracking()
            .Where(attachment => contractIds.Contains(attachment.ContractId))
            .GroupBy(attachment => attachment.ContractId)
            .Select(group => new { ContractId = group.Key, Count = group.Count() })
            .ToDictionary(item => item.ContractId, item => item.Count);

        return contracts.Select(contract =>
        {
            employees.TryGetValue(contract.EmployeeId, out var employee);

            return new ContractRow(
                contract.Id,
                contract.EmployeeId,
                employee?.Name ?? "—",
                employee?.IdNumber ?? "—",
                employee?.Position ?? "—",
                contract.ContractType,
                contract.StartDate,
                contract.EndDate,
                contract.BaseSalary,
                contract.Status,
                contract.PreviousContractId,
                attachmentCounts.TryGetValue(contract.Id, out var count) ? count : 0,
                contract.CreatedAt,
                contract.TerminatedAt);
        }).ToList();
    }

    private IReadOnlyDictionary<int, string> BuildAuthorNames(
        IReadOnlyList<ContractAttachment> attachments)
    {
        var userIds = attachments
            .Where(attachment => attachment.UploadedByUserId is not null)
            .Select(attachment => attachment.UploadedByUserId!.Value)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new Dictionary<int, string>();

        var users = _context.AppUsers
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToList();

        var employeeIds = users
            .Where(user => user.EmployeeId is not null)
            .Select(user => user.EmployeeId!.Value)
            .Distinct()
            .ToList();

        var names = _context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .ToDictionary(employee => employee.Id, employee => employee.Name);

        return users.ToDictionary(
            user => user.Id,
            user => user.EmployeeId is int employeeId && names.TryGetValue(employeeId, out var name)
                ? name
                : user.Email);
    }
}
