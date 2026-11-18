using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TiendaFlow.Application;
using TiendaFlow.Application.Crm;
using TiendaFlow.Application.Invoicing;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.Infrastructure;

/// <summary>
/// Registers the EF Core implementations and the Application services.
/// Program.cs and the integration tests share this method so the container
/// wiring is verified instead of duplicated.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTiendaFlowPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TiendaFlowDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IStockRepository, EfStockRepository>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IProductCatalogQuery, EfProductCatalogQuery>();
        services.AddScoped<IStockQuery, EfStockQuery>();

        services.AddScoped<IAppUserRepository, EfAppUserRepository>();
        services.AddScoped<IEmployeeRepository, EfEmployeeRepository>();
        services.AddScoped<IEmploymentContractRepository, EfEmploymentContractRepository>();
        services.AddScoped<IEmploymentContractQuery, EfEmploymentContractQuery>();
        services.AddScoped<IAttendanceRepository, EfAttendanceRepository>();
        services.AddScoped<IShiftRepository, EfShiftRepository>();
        services.AddScoped<IStoreRepository, EfStoreRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IUserDirectoryQuery, EfUserDirectoryQuery>();
        services.AddScoped<IAuditTrailQuery, EfAuditTrailQuery>();
        services.AddScoped<IStoreDirectoryQuery, EfStoreDirectoryQuery>();
        services.AddScoped<IStoreUsageQuery, EfStoreUsageQuery>();
        services.AddScoped<ISystemParameterRepository, EfSystemParameterRepository>();
        services.AddScoped<IParameterAuthorQuery, EfParameterAuthorQuery>();

        services.AddScoped<ISupplierRepository, EfSupplierRepository>();
        services.AddScoped<IPurchaseOrderRepository, EfPurchaseOrderRepository>();
        services.AddScoped<IProductSupplierRepository, EfProductSupplierRepository>();
        services.AddScoped<ISupplierQuery, EfSupplierQuery>();
        services.AddScoped<IPurchaseOrderQuery, EfPurchaseOrderQuery>();
        services.AddScoped<IConsumptionQuery, EfConsumptionQuery>();

        services.AddScoped<IInvoiceRepository, EfInvoiceRepository>();
        services.AddScoped<IInvoiceQuery, EfInvoiceQuery>();
        services.AddScoped<ISalesReturnRepository, EfSalesReturnRepository>();
        services.AddScoped<ISalesReturnQuery, EfSalesReturnQuery>();
        services.AddScoped<IPosSaleRepository, EfPosSaleRepository>();
        services.AddScoped<IPosSaleQuery, EfPosSaleQuery>();

        services.AddScoped<IInventoryCountRepository, EfInventoryCountRepository>();
        services.AddScoped<IInventoryTransferRepository, EfInventoryTransferRepository>();
        services.AddScoped<IInventoryDocumentQuery, EfInventoryDocumentQuery>();

        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<ICustomerQuery, EfCustomerQuery>();

        services.AddScoped<ILoyaltyRepository, EfLoyaltyRepository>();
        services.AddScoped<IPqrsRepository, EfPqrsRepository>();
        services.AddScoped<ICampaignRepository, EfCampaignRepository>();

        services.AddScoped<IAccessControl, AccessControl>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<InventoryDashboardService>();
        services.AddScoped<BusinessDashboardService>();
        services.AddScoped<InventoryReportService>();
        services.AddScoped<SalesReportService>();
        services.AddScoped<CustomerSegmentationService>();
        services.AddSingleton<IElectronicInvoiceGateway, SimulatedElectronicInvoiceGateway>();
        services.AddScoped<ElectronicInvoiceService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<PosSaleService>();
        services.AddScoped<InventoryCountService>();
        services.AddScoped<InventoryTransferService>();
        services.AddScoped<ProductPriceService>();
        services.AddScoped<UserService>();
        services.AddScoped<UserDirectoryService>();
        services.AddScoped<StoreService>();
        services.AddScoped<StoreDirectoryService>();
        services.AddScoped<ParameterService>();
        services.AddScoped<IParameterValues, ParameterValues>();
        services.AddScoped<AuditTrailService>();
        services.AddScoped<RoleCatalogService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ProductSupplierService>();
        services.AddScoped<ReplenishmentService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<SalesInvoiceService>();
        services.AddScoped<SalesReturnService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<EmploymentContractService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<PqrsService>();
        services.AddScoped<CampaignService>();

        return services;
    }
}
