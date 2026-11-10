namespace TiendaFlow.Application.Security;

/// <summary>
/// Every function of the ERP is protected by one permission code. Pages,
/// navigation items and application services all use these constants so a
/// function can never be exposed through one layer while missing in another.
/// </summary>
public static class Permissions
{
    // Security and reporting
    public const string SecurityUsersView = "security.users.view";
    public const string SecurityUsersManage = "security.users.manage";
    public const string SecurityRolesView = "security.roles.view";
    public const string SecurityRolesManage = "security.roles.manage";
    public const string SecurityAuditView = "security.audit.view";
    public const string SecurityParametersManage = "security.parameters.manage";
    public const string SecurityReportsView = "security.reports.view";

    // Inventory
    public const string InventoryProductsView = "inventory.products.view";
    public const string InventoryProductsManage = "inventory.products.manage";
    public const string InventoryPriceChange = "inventory.price.change";
    public const string InventoryStockView = "inventory.stock.view";
    public const string InventoryStockReceive = "inventory.stock.receive";
    public const string InventoryStockTransfer = "inventory.stock.transfer";
    public const string InventoryStockCount = "inventory.stock.count";
    public const string InventoryAlertsView = "inventory.alerts.view";
    public const string InventoryReportsView = "inventory.reports.view";

    // Sales and billing
    public const string SalesPosUse = "sales.pos.use";
    public const string SalesReturnsAuthorize = "sales.returns.authorize";
    public const string SalesReportsView = "sales.reports.view";

    // Supplier and purchasing
    public const string PurchasingSuppliersView = "purchasing.suppliers.view";
    public const string PurchasingSuppliersManage = "purchasing.suppliers.manage";
    public const string PurchasingOrdersView = "purchasing.orders.view";
    public const string PurchasingOrdersCreate = "purchasing.orders.create";
    public const string PurchasingOrdersApprove = "purchasing.orders.approve";
    public const string PurchasingOrdersSend = "purchasing.orders.send";
    public const string PurchasingOrdersReceive = "purchasing.orders.receive";
    public const string PurchasingOrdersCancel = "purchasing.orders.cancel";
    public const string PurchasingEvaluationsManage = "purchasing.evaluations.manage";
    public const string PurchasingReportsView = "purchasing.reports.view";

    // Human resources
    public const string HrEmployeesView = "hr.employees.view";
    public const string HrEmployeesManage = "hr.employees.manage";
    public const string HrPayrollManage = "hr.payroll.manage";
    public const string HrContractsManage = "hr.contracts.manage";
    public const string HrAttendanceManage = "hr.attendance.manage";
    public const string HrShiftsManage = "hr.shifts.manage";

    // Customer management
    public const string CrmCustomersView = "crm.customers.view";
    public const string CrmCustomersManage = "crm.customers.manage";
    public const string CrmPointsManage = "crm.points.manage";
    public const string CrmPqrsManage = "crm.pqrs.manage";
    public const string CrmCampaignsManage = "crm.campaigns.manage";
    public const string CrmReportsView = "crm.reports.view";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SecurityUsersView,
        SecurityUsersManage,
        SecurityRolesView,
        SecurityRolesManage,
        SecurityAuditView,
        SecurityParametersManage,
        SecurityReportsView,
        InventoryProductsView,
        InventoryProductsManage,
        InventoryPriceChange,
        InventoryStockView,
        InventoryStockReceive,
        InventoryStockTransfer,
        InventoryStockCount,
        InventoryAlertsView,
        InventoryReportsView,
        SalesPosUse,
        SalesReturnsAuthorize,
        SalesReportsView,
        PurchasingSuppliersView,
        PurchasingSuppliersManage,
        PurchasingOrdersView,
        PurchasingOrdersCreate,
        PurchasingOrdersApprove,
        PurchasingOrdersSend,
        PurchasingOrdersReceive,
        PurchasingOrdersCancel,
        PurchasingEvaluationsManage,
        PurchasingReportsView,
        HrEmployeesView,
        HrEmployeesManage,
        HrPayrollManage,
        HrContractsManage,
        HrAttendanceManage,
        HrShiftsManage,
        CrmCustomersView,
        CrmCustomersManage,
        CrmPointsManage,
        CrmPqrsManage,
        CrmCampaignsManage,
        CrmReportsView
    };

    public static bool IsKnown(string permission) => All.Contains(permission);
}
