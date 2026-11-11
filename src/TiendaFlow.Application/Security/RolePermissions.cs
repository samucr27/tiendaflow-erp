using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// The single source of truth for what each role may do. The matrix is code,
/// not database data: the database stores roles and users, while this matrix
/// keeps authorization deterministic between deployments.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Matrix =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [RoleNames.Administrator] = Set(
                Permissions.SecurityUsersView,
                Permissions.SecurityUsersManage,
                Permissions.SecurityRolesView,
                Permissions.SecurityRolesManage,
                Permissions.SecurityAuditView,
                Permissions.SecurityParametersManage,
                Permissions.SecurityReportsView,
                Permissions.InventoryProductsView,
                Permissions.InventoryStockView,
                Permissions.InventoryAlertsView,
                Permissions.InventoryReportsView,
                Permissions.SalesReportsView,
                Permissions.SalesInvoicesView,
                Permissions.SalesInvoicesManage,
                Permissions.PurchasingSuppliersView,
                Permissions.PurchasingOrdersView,
                Permissions.PurchasingReportsView,
                Permissions.HrEmployeesView,
                Permissions.CrmCustomersView,
                Permissions.CrmReportsView),

            [RoleNames.CorporateManager] = Set(
                Permissions.SecurityReportsView,
                Permissions.InventoryProductsView,
                Permissions.InventoryStockView,
                Permissions.InventoryAlertsView,
                Permissions.InventoryReportsView,
                Permissions.SalesReportsView,
                Permissions.SalesInvoicesView,
                Permissions.SalesInvoicesManage,
                Permissions.PurchasingSuppliersView,
                Permissions.PurchasingOrdersView,
                Permissions.PurchasingReportsView,
                Permissions.HrEmployeesView,
                Permissions.CrmCustomersView,
                Permissions.CrmCampaignsManage,
                Permissions.CrmReportsView),

            [RoleNames.StoreManager] = Set(
                Permissions.InventoryProductsView,
                Permissions.InventoryProductsManage,
                Permissions.InventoryPriceChange,
                Permissions.InventoryStockView,
                Permissions.InventoryStockReceive,
                Permissions.InventoryStockTransfer,
                Permissions.InventoryStockCount,
                Permissions.InventoryAlertsView,
                Permissions.InventoryReportsView,
                Permissions.SalesPosUse,
                Permissions.SalesReturnsAuthorize,
                Permissions.SalesReportsView,
                Permissions.SalesInvoicesView,
                Permissions.SalesInvoicesManage,
                Permissions.PurchasingSuppliersView,
                Permissions.PurchasingOrdersView,
                Permissions.PurchasingOrdersApprove,
                Permissions.PurchasingOrdersReceive,
                Permissions.PurchasingReportsView,
                Permissions.HrEmployeesView,
                Permissions.HrAttendanceManage,
                Permissions.HrShiftsManage,
                Permissions.CrmCustomersView,
                Permissions.CrmPqrsManage),

            [RoleNames.Cashier] = Set(
                Permissions.InventoryStockView,
                Permissions.SalesPosUse,
                Permissions.SalesInvoicesView,
                Permissions.SalesInvoicesManage,
                Permissions.CrmCustomersView,
                Permissions.CrmCustomersManage,
                Permissions.CrmPointsManage),

            [RoleNames.StockClerk] = Set(
                Permissions.InventoryProductsView,
                Permissions.InventoryStockView,
                Permissions.InventoryStockReceive,
                Permissions.InventoryStockTransfer,
                Permissions.InventoryStockCount,
                Permissions.InventoryAlertsView),

            [RoleNames.LogisticsCoordinator] = Set(
                Permissions.InventoryProductsView,
                Permissions.InventoryStockView,
                Permissions.InventoryStockReceive,
                Permissions.InventoryAlertsView,
                Permissions.PurchasingSuppliersView,
                Permissions.PurchasingSuppliersManage,
                Permissions.PurchasingOrdersView,
                Permissions.PurchasingOrdersCreate,
                Permissions.PurchasingOrdersSend,
                Permissions.PurchasingOrdersReceive,
                Permissions.PurchasingOrdersCancel,
                Permissions.PurchasingEvaluationsManage,
                Permissions.PurchasingReportsView),

            [RoleNames.HumanResourcesOfficer] = Set(
                Permissions.HrEmployeesView,
                Permissions.HrEmployeesManage,
                Permissions.HrPayrollManage,
                Permissions.HrContractsManage,
                Permissions.HrAttendanceManage,
                Permissions.HrShiftsManage)
        };

    public static IReadOnlySet<string> ForRole(string roleName) =>
        Matrix.TryGetValue(roleName, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> All => Matrix;

    public static bool IsAllowed(string roleName, string permission) =>
        ForRole(roleName).Contains(permission);

    private static IReadOnlySet<string> Set(params string[] permissions) =>
        new HashSet<string>(permissions, StringComparer.Ordinal);
}
