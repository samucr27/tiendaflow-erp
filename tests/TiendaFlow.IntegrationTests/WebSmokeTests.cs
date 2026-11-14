using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// End-to-end smoke test for the real HTTP pipeline: it signs in through the
/// login form as each of the seven roles and opens every route that role is
/// allowed to see. A page that throws while querying SQL Server answers with
/// the generic red message, so the assertion also guards every list and detail
/// screen against the regression that hid the suppliers and customers.
/// </summary>
public class WebSmokeTests : IClassFixture<ErpWebApplicationFactory>
{
    private static readonly string[] ErrorMarkers =
    {
        "class=\"alert alert-error\" role=\"alert\"",
        "An unhandled error has occurred"
    };

    private readonly ErpWebApplicationFactory _factory;

    public WebSmokeTests(ErpWebApplicationFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task EveryRoleRendersEveryAllowedRouteWithoutTheGenericError()
    {
        var routes = await LoadRoutesAsync();
        var failures = new List<string>();

        foreach (var account in Accounts())
        {
            using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = true,
                HandleCookies = true
            });

            await SignInAsync(client, account);

            var permissions = RolePermissions.ForRole(account.RoleName);

            foreach (var route in routes.Where(route => permissions.Contains(route.Permission)))
            {
                using var response = await client.GetAsync(route.Path);
                var html = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    failures.Add(
                        $"{account.RoleName} {route.Path}: HTTP {(int)response.StatusCode}");
                    continue;
                }

                var finalPath = response.RequestMessage?.RequestUri?.AbsolutePath ?? route.Path;

                if (!string.Equals(
                        Uri.UnescapeDataString(finalPath).TrimEnd('/'),
                        route.Path.TrimEnd('/'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        $"{account.RoleName} {route.Path}: redirigido a {Uri.UnescapeDataString(finalPath)}");
                    continue;
                }

                if (ErrorMarkers.Any(marker => html.Contains(marker, StringComparison.Ordinal)))
                {
                    failures.Add($"{account.RoleName} {route.Path}: mensaje de error en la página");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Las siguientes rutas fallaron:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures)
            + Environment.NewLine
            + "Registro del servidor:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, _factory.Log.Entries.TakeLast(20)));
    }

    [SqlServerFact]
    public async Task LoginRejectsWrongCredentialsWithoutRevealingWhichFieldFailed()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = true,
            HandleCookies = true
        });

        var token = await ReadAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync("/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["email"] = "admin@tiendaflow.local",
                ["password"] = "contrasena-incorrecta",
                ["__RequestVerificationToken"] = token
            }));

        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El correo o la contraseña no son correctos", html);
        Assert.DoesNotContain("no existe", html, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SignInAsync(HttpClient client, SeedAccount account)
    {
        var token = await ReadAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync("/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["email"] = account.Email,
                ["password"] = account.Password,
                ["returnUrl"] = "/",
                ["__RequestVerificationToken"] = token
            }));

        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("El correo o la contraseña no son correctos", html);
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/login");
        var html = await response.Content.ReadAsStringAsync();

        var input = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);

        Assert.True(input.Success, "El formulario de ingreso no incluyó el token antiforgery.");

        var value = Regex.Match(input.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

        Assert.True(value.Success, "El token antiforgery no tiene valor.");

        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private async Task<IReadOnlyList<SmokeRoute>> LoadRoutesAsync()
    {
        await _factory.PrepareAsync();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TiendaFlowDbContext>();

        var configuration = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var admin = await context.AppUsers.SingleAsync(user => user.Email == "admin@tiendaflow.local");

        Assert.Equal(_factory.AdminPassword, configuration["Seed:AdminPassword"]);
        Assert.True(
            hasher.Verify(admin.PasswordHash, _factory.AdminPassword),
            "La contraseña sembrada no coincide con la configurada para la prueba.");

        var supplierId = await context.Suppliers.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var orderId = await context.PurchaseOrders.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var invoiceId = await context.SalesInvoices.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var customerId = await context.Customers.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var userId = await context.AppUsers.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var employeeId = await context.Employees.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var storeId = await context.Stores.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var countId = await context.InventoryCounts.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var transferId = await context.InventoryTransfers.OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var parameterKey = SystemParameterCatalog.All.First().Key;

        return new[]
        {
            new SmokeRoute("/", Permissions.InventoryStockView),
            new SmokeRoute("/inventario", Permissions.InventoryStockView),
            new SmokeRoute("/inventario/productos", Permissions.InventoryProductsView),
            new SmokeRoute("/inventario/productos/SKU-1001", Permissions.InventoryProductsView),
            new SmokeRoute("/inventario/productos/nuevo", Permissions.InventoryProductsManage),
            new SmokeRoute("/inventario/stock", Permissions.InventoryStockView),
            new SmokeRoute("/inventario/recibir", Permissions.InventoryStockReceive),
            new SmokeRoute("/inventario/ventas", Permissions.SalesPosUse),
            new SmokeRoute("/inventario/alertas", Permissions.InventoryAlertsView),
            new SmokeRoute("/inventario/conteos", Permissions.InventoryStockCount),
            new SmokeRoute($"/inventario/conteos/{countId}", Permissions.InventoryStockCount),
            new SmokeRoute("/inventario/traslados", Permissions.InventoryStockTransfer),
            new SmokeRoute($"/inventario/traslados/{transferId}", Permissions.InventoryStockTransfer),
            new SmokeRoute("/reportes/inventario", Permissions.InventoryReportsView),
            new SmokeRoute("/reportes/ventas", Permissions.SalesReportsView),
            new SmokeRoute("/reportes/nomina", Permissions.HrPayrollManage),
            new SmokeRoute("/seguridad/usuarios", Permissions.SecurityUsersView),
            new SmokeRoute($"/seguridad/usuarios/{userId}", Permissions.SecurityUsersManage),
            new SmokeRoute("/seguridad/usuarios/nuevo", Permissions.SecurityUsersManage),
            new SmokeRoute("/seguridad/roles", Permissions.SecurityRolesView),
            new SmokeRoute("/seguridad/auditoria", Permissions.SecurityAuditView),
            new SmokeRoute("/seguridad/parametros", Permissions.SecurityParametersManage),
            new SmokeRoute(
                $"/seguridad/parametros/{Uri.EscapeDataString(parameterKey)}",
                Permissions.SecurityParametersManage),
            new SmokeRoute("/seguridad/tiendas", Permissions.SecurityStoresView),
            new SmokeRoute($"/seguridad/tiendas/{storeId}", Permissions.SecurityStoresView),
            new SmokeRoute("/seguridad/tiendas/nueva", Permissions.SecurityStoresManage),
            new SmokeRoute("/rrhh/empleados", Permissions.HrEmployeesView),
            new SmokeRoute($"/rrhh/empleados/{employeeId}", Permissions.HrEmployeesManage),
            new SmokeRoute("/rrhh/empleados/nuevo", Permissions.HrEmployeesManage),
            new SmokeRoute("/rrhh/nomina", Permissions.HrPayrollManage),
            new SmokeRoute("/proveedores", Permissions.PurchasingSuppliersView),
            new SmokeRoute($"/proveedores/{supplierId}", Permissions.PurchasingSuppliersManage),
            new SmokeRoute("/proveedores/nuevo", Permissions.PurchasingSuppliersManage),
            new SmokeRoute("/proveedores/ranking", Permissions.PurchasingReportsView),
            new SmokeRoute("/compras/ordenes", Permissions.PurchasingOrdersView),
            new SmokeRoute($"/compras/ordenes/{orderId}", Permissions.PurchasingOrdersView),
            new SmokeRoute("/compras/ordenes/nueva", Permissions.PurchasingOrdersCreate),
            new SmokeRoute("/compras/reposicion", Permissions.PurchasingOrdersCreate),
            new SmokeRoute("/facturacion", Permissions.SalesInvoicesView),
            new SmokeRoute($"/facturacion/{invoiceId}", Permissions.SalesInvoicesManage),
            new SmokeRoute("/facturacion/nueva", Permissions.SalesInvoicesManage),
            new SmokeRoute("/crm/clientes", Permissions.CrmCustomersView),
            new SmokeRoute($"/crm/clientes/{customerId}", Permissions.CrmCustomersManage),
            new SmokeRoute("/crm/clientes/nuevo", Permissions.CrmCustomersManage),
            new SmokeRoute("/crm/segmentacion", Permissions.CrmReportsView)
        };
    }

    private IEnumerable<SeedAccount> Accounts() => new[]
    {
        new SeedAccount(RoleNames.Administrator, "admin@tiendaflow.local", _factory.AdminPassword),
        new SeedAccount(RoleNames.CorporateManager, "gerente@tiendaflow.local", _factory.DemoPassword),
        new SeedAccount(RoleNames.StoreManager, "jefe.tienda@tiendaflow.local", _factory.DemoPassword),
        new SeedAccount(RoleNames.Cashier, "cajero@tiendaflow.local", _factory.DemoPassword),
        new SeedAccount(RoleNames.StockClerk, "bodega@tiendaflow.local", _factory.DemoPassword),
        new SeedAccount(RoleNames.LogisticsCoordinator, "logistica@tiendaflow.local", _factory.DemoPassword),
        new SeedAccount(RoleNames.HumanResourcesOfficer, "rrhh@tiendaflow.local", _factory.DemoPassword)
    };

    private sealed record SmokeRoute(string Path, string Permission);

    private sealed record SeedAccount(string RoleName, string Email, string Password);
}
