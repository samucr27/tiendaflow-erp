using TiendaFlow.Application.Security;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Structural guards for the screens added after Sprint 2. Each route must
/// declare the same permission the service enforces, and the menu must hide
/// modules the user cannot open.
/// </summary>
public class FinalScreenAuthorizationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [InlineData("Compras/Ordenes.razor", "/compras/ordenes", "PurchasingOrdersView")]
    [InlineData("Compras/OrdenForm.razor", "/compras/ordenes/nueva", "PurchasingOrdersCreate")]
    [InlineData("Compras/OrdenDetalle.razor", "/compras/ordenes/{Id:int}", "PurchasingOrdersView")]
    [InlineData("Compras/Reposicion.razor", "/compras/reposicion", "PurchasingOrdersCreate")]
    [InlineData("Proveedores/Ranking.razor", "/proveedores/ranking", "PurchasingReportsView")]
    [InlineData("Crm/Segmentacion.razor", "/crm/segmentacion", "CrmReportsView")]
    public void PurchasingRoutesDeclareThePolicyOfTheMatrix(
        string file,
        string route,
        string permission)
    {
        var source = ReadPage(file);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains($"@attribute [Authorize(Policy = Permissions.{permission})]", source);
    }

    [Theory]
    [InlineData("Reportes/Inventario.razor", "/reportes/inventario", "InventoryReportsView")]
    [InlineData("Reportes/Ventas.razor", "/reportes/ventas", "SalesReportsView")]
    [InlineData("Reportes/Nomina.razor", "/reportes/nomina", "HrPayrollManage")]
    public void ReportRoutesDeclareThePolicyOfTheMatrix(
        string file,
        string route,
        string permission)
    {
        var source = ReadPage(file);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains($"@attribute [Authorize(Policy = Permissions.{permission})]", source);
    }

    [Theory]
    [InlineData("Seguridad/Tiendas.razor", "/seguridad/tiendas", "SecurityStoresView")]
    [InlineData("Seguridad/TiendaNueva.razor", "/seguridad/tiendas/nueva", "SecurityStoresManage")]
    [InlineData("Seguridad/TiendaDetalle.razor", "/seguridad/tiendas/{Id:int}", "SecurityStoresView")]
    public void StoreRoutesDeclareThePolicyOfTheMatrix(
        string file,
        string route,
        string permission)
    {
        var source = ReadPage(file);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains($"@attribute [Authorize(Policy = Permissions.{permission})]", source);
    }

    [Theory]
    [InlineData("Seguridad/Parametros.razor", "/seguridad/parametros")]
    [InlineData("Seguridad/ParametroDetalle.razor", "/seguridad/parametros/{Key}")]
    public void ParameterRoutesDeclareTheManagePolicy(string file, string route)
    {
        var source = ReadPage(file);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains(
            "@attribute [Authorize(Policy = Permissions.SecurityParametersManage)]",
            source);
    }

    [Fact]
    public void MenuShowsParametersOnlyWithTheManagePermission()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"seguridad/parametros\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.SecurityParametersManage)", menu);
    }

    [Fact]
    public void MenuShowsStoresOnlyWithTheViewPermission()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"seguridad/tiendas\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.SecurityStoresView)", menu);
    }

    [Fact]
    public void MenuShowsReportsOnlyWithTheReportPermissions()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"reportes/inventario\"", menu);
        Assert.Contains("href=\"reportes/ventas\"", menu);
        Assert.Contains("href=\"reportes/nomina\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.InventoryReportsView)", menu);
        Assert.Contains("AccessControl.Has(Permissions.SalesReportsView)", menu);
        Assert.Contains("AccessControl.Has(Permissions.HrPayrollManage)", menu);
    }

    [Fact]
    public void MenuShowsPurchaseOrdersOnlyWithTheViewPermission()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"compras/ordenes\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.PurchasingOrdersView)", menu);
        Assert.Contains("href=\"compras/reposicion\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.PurchasingOrdersCreate)", menu);
        Assert.Contains("href=\"proveedores/ranking\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.PurchasingReportsView)", menu);
        Assert.Contains("href=\"crm/segmentacion\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.CrmReportsView)", menu);
    }

    [Fact]
    public void InvoicesPageOffersTheSimulatedXmlAndWarnsItIsNotDian()
    {
        var source = ReadPage(Path.Combine("Facturacion", "Facturas.razor"));

        Assert.Contains("XML simulado", source);
        Assert.Contains("facturacion/{invoice.Id}/xml", source);
        Assert.Contains("no es válido ante la DIAN", source);
    }

    [Theory]
    [InlineData("Inventario/Conteos.razor", "/inventario/conteos", "InventoryStockCount")]
    [InlineData("Inventario/ConteoDetalle.razor", "/inventario/conteos/{Id:int}", "InventoryStockCount")]
    [InlineData("Inventario/Traslados.razor", "/inventario/traslados", "InventoryStockTransfer")]
    [InlineData("Inventario/TrasladoDetalle.razor", "/inventario/traslados/{Id:int}", "InventoryStockTransfer")]
    public void InventoryDocumentRoutesDeclareThePolicyOfTheMatrix(
        string file,
        string route,
        string permission)
    {
        var source = ReadPage(file);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains($"@attribute [Authorize(Policy = Permissions.{permission})]", source);
    }

    [Fact]
    public void MenuShowsInventoryDocumentsOnlyWithTheirPermissions()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"inventario/conteos\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.InventoryStockCount)", menu);
        Assert.Contains("href=\"inventario/traslados\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.InventoryStockTransfer)", menu);
    }

    private static string ReadPage(string file) =>
        File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Pages", file));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TiendaFlow.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find TiendaFlow.slnx above {AppContext.BaseDirectory}.");
    }
}
