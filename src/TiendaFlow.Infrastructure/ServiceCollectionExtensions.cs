using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TiendaFlow.Application;
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
        services.AddScoped<IStoreRepository, EfStoreRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IUserDirectoryQuery, EfUserDirectoryQuery>();
        services.AddScoped<IAuditTrailQuery, EfAuditTrailQuery>();

        services.AddScoped<ISupplierRepository, EfSupplierRepository>();
        services.AddScoped<IPurchaseOrderRepository, EfPurchaseOrderRepository>();
        services.AddScoped<ISupplierQuery, EfSupplierQuery>();
        services.AddScoped<IPurchaseOrderQuery, EfPurchaseOrderQuery>();

        services.AddScoped<IInvoiceRepository, EfInvoiceRepository>();
        services.AddScoped<IInvoiceQuery, EfInvoiceQuery>();

        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<ICustomerQuery, EfCustomerQuery>();

        services.AddScoped<IAccessControl, AccessControl>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<InventoryDashboardService>();
        services.AddScoped<BusinessDashboardService>();
        services.AddScoped<InventoryReportService>();
        services.AddScoped<SalesReportService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<ProductPriceService>();
        services.AddScoped<UserService>();
        services.AddScoped<UserDirectoryService>();
        services.AddScoped<AuditTrailService>();
        services.AddScoped<RoleCatalogService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<SalesInvoiceService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<EmployeeService>();

        return services;
    }
}
