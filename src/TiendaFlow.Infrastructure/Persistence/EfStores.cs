using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

/// <summary>
/// Answers the three questions that block a store deactivation. Keeping the
/// checks in SQL avoids loading the whole stock or purchasing tables.
/// </summary>
public sealed class EfStoreUsageQuery : IStoreUsageQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfStoreUsageQuery(TiendaFlowDbContext context) => _context = context;

    public bool HasStock(int storeId) =>
        _context.Stocks.Any(item => item.StoreId == storeId && item.QuantityAvailable > 0);

    public bool HasOpenPurchaseOrders(int storeId) =>
        _context.PurchaseOrders.Any(order =>
            order.StoreId == storeId
            && order.Status != PurchaseOrderStatus.Received
            && order.Status != PurchaseOrderStatus.Cancelled);

    public bool HasActiveUsers(int storeId) =>
        (from user in _context.AppUsers
         join employee in _context.Employees on user.EmployeeId equals employee.Id
         where user.Active && employee.StoreId == storeId
         select user.Id).Any();
}

/// <summary>
/// Read model for <c>/seguridad/tiendas</c>. Each store carries the counters
/// the screen shows and the guard uses, and the detail view lists every user
/// and employee so an administrator can reassign them.
/// </summary>
public sealed class EfStoreDirectoryQuery : IStoreDirectoryQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfStoreDirectoryQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<StoreListItem> List()
    {
        var stores = _context.Stores.AsNoTracking().OrderBy(store => store.Name).ToList();
        var employees = _context.Employees.AsNoTracking().ToList();
        var activeUsers = _context.AppUsers
            .AsNoTracking()
            .Where(user => user.Active && user.EmployeeId != null)
            .Select(user => user.EmployeeId!.Value)
            .ToList();
        var stock = _context.Stocks
            .AsNoTracking()
            .Select(item => new { item.StoreId, item.QuantityAvailable })
            .ToList();
        var orders = _context.PurchaseOrders
            .AsNoTracking()
            .Select(order => new { order.StoreId, order.Status })
            .ToList();

        return stores.Select(store => ToRow(
            store,
            employees.Count(employee => employee.StoreId == store.Id),
            activeUsers.Count(employeeId => employees.Any(employee =>
                employee.Id == employeeId && employee.StoreId == store.Id)),
            stock.Where(item => item.StoreId == store.Id).Sum(item => item.QuantityAvailable),
            orders.Count(order => order.StoreId == store.Id
                && order.Status != PurchaseOrderStatus.Received
                && order.Status != PurchaseOrderStatus.Cancelled)))
            .ToList();
    }

    public StoreDetail? Get(int id)
    {
        var store = _context.Stores.AsNoTracking().FirstOrDefault(candidate => candidate.Id == id);
        if (store is null)
            return null;

        var stores = _context.Stores.AsNoTracking().ToDictionary(candidate => candidate.Id);
        var employees = _context.Employees.AsNoTracking().ToList();
        var roles = _context.Roles.AsNoTracking().ToDictionary(role => role.Id);
        var users = _context.AppUsers.AsNoTracking().OrderBy(user => user.Email).ToList();
        var stock = _context.Stocks
            .AsNoTracking()
            .Where(item => item.StoreId == id)
            .Select(item => item.QuantityAvailable)
            .ToList();
        var openOrders = _context.PurchaseOrders
            .AsNoTracking()
            .Count(order => order.StoreId == id
                && order.Status != PurchaseOrderStatus.Received
                && order.Status != PurchaseOrderStatus.Cancelled);

        var employeesOfStore = employees.Where(employee => employee.StoreId == id).ToList();
        var activeUsers = users.Count(user => user.Active
            && user.EmployeeId is int employeeId
            && employees.Any(employee => employee.Id == employeeId && employee.StoreId == id));

        var row = ToRow(
            store,
            employeesOfStore.Count,
            activeUsers,
            stock.Sum(),
            openOrders);

        var userRows = users.Select(user =>
        {
            var employee = user.EmployeeId is int employeeId
                ? employees.FirstOrDefault(candidate => candidate.Id == employeeId)
                : null;
            var role = roles.TryGetValue(user.RoleId, out var foundRole) ? foundRole : null;

            return new StoreUserAssignment(
                user.Id,
                employee?.Name ?? user.Email,
                user.Email,
                role is null ? "Sin rol" : SystemRoles.Label(role.Name),
                employee?.StoreId,
                employee is not null && stores.TryGetValue(employee.StoreId, out var storeRow)
                    ? storeRow.Name
                    : "Sin tienda",
                user.Active);
        }).ToList();

        var employeeRows = employees
            .OrderBy(employee => employee.Name)
            .Select(employee => new StoreEmployeeAssignment(
                employee.Id,
                employee.Name,
                employee.IdNumber,
                employee.Position,
                employee.StoreId,
                stores.TryGetValue(employee.StoreId, out var storeRow) ? storeRow.Name : "Sin tienda",
                employee.Active))
            .ToList();

        return new StoreDetail(row, userRows, employeeRows);
    }

    private static StoreListItem ToRow(
        Store store,
        int employeeCount,
        int activeUsers,
        int unitsInStock,
        int openOrders) =>
        new(
            store.Id,
            store.Name,
            store.Address,
            store.Active,
            activeUsers,
            employeeCount,
            unitsInStock,
            openOrders);
}
