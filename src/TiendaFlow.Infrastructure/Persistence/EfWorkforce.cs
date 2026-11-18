using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfAttendanceRepository : IAttendanceRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfAttendanceRepository(TiendaFlowDbContext context) => _context = context;

    public AttendanceRecord? GetById(int id) =>
        _context.LoadFresh<AttendanceRecord>(
            record => record.Id == id,
            () => _context.AttendanceRecords.FirstOrDefault(record => record.Id == id));

    public AttendanceRecord? GetByEmployeeAndDate(int employeeId, DateOnly workDate) =>
        _context.LoadFresh<AttendanceRecord>(
            record => record.EmployeeId == employeeId && record.WorkDate == workDate,
            () => _context.AttendanceRecords.FirstOrDefault(record =>
                record.EmployeeId == employeeId && record.WorkDate == workDate));

    public IReadOnlyList<AttendanceRecord> List(DateOnly from, DateOnly to, int? storeId) =>
        _context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.WorkDate >= from && record.WorkDate <= to)
            .Where(record => storeId == null || record.StoreId == storeId)
            .OrderByDescending(record => record.WorkDate)
            .ThenBy(record => record.EmployeeId)
            .ToList();

    public void Add(AttendanceRecord record)
    {
        _context.AttendanceRecords.Add(record);
        _context.SaveChanges();
    }

    public void Update(AttendanceRecord record)
    {
        if (_context.Entry(record).State == EntityState.Detached)
            _context.AttendanceRecords.Update(record);

        _context.SaveChanges();
    }
}

public sealed class EfShiftRepository : IShiftRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfShiftRepository(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<ShiftTemplate> ListTemplates() =>
        _context.ShiftTemplates
            .AsNoTracking()
            .OrderBy(template => template.StartTime)
            .ThenBy(template => template.Name)
            .ToList();

    public ShiftTemplate? GetTemplate(int id) =>
        _context.LoadFresh<ShiftTemplate>(
            template => template.Id == id,
            () => _context.ShiftTemplates.FirstOrDefault(template => template.Id == id));

    public bool ExistsTemplateName(string name, int? exceptTemplateId = null)
    {
        var normalized = name.Trim();

        return _context.ShiftTemplates
            .AsNoTracking()
            .Any(template => template.Name == normalized
                && (exceptTemplateId == null || template.Id != exceptTemplateId));
    }

    public void AddTemplate(ShiftTemplate template)
    {
        _context.ShiftTemplates.Add(template);
        _context.SaveChanges();
    }

    public ShiftAssignment? GetAssignment(int id) =>
        _context.LoadFresh<ShiftAssignment>(
            assignment => assignment.Id == id,
            () => _context.ShiftAssignments.FirstOrDefault(assignment => assignment.Id == id));

    public IReadOnlyList<ShiftAssignment> ListAssignments(
        DateOnly from,
        DateOnly to,
        int? storeId) =>
        _context.ShiftAssignments
            .AsNoTracking()
            .Where(assignment => assignment.WorkDate >= from && assignment.WorkDate <= to)
            .Where(assignment => storeId == null || assignment.StoreId == storeId)
            .OrderBy(assignment => assignment.WorkDate)
            .ThenBy(assignment => assignment.StartTime)
            .ToList();

    public IReadOnlyList<ShiftAssignment> ListAssignmentsByEmployeeAndDate(
        int employeeId,
        DateOnly workDate) =>
        _context.ShiftAssignments
            .AsNoTracking()
            .Where(assignment => assignment.EmployeeId == employeeId
                && assignment.WorkDate == workDate)
            .OrderBy(assignment => assignment.StartTime)
            .ToList();

    public void AddAssignment(ShiftAssignment assignment)
    {
        _context.ShiftAssignments.Add(assignment);
        _context.SaveChanges();
    }

    public void RemoveAssignment(ShiftAssignment assignment)
    {
        _context.ShiftAssignments.Remove(assignment);
        _context.SaveChanges();
    }
}
