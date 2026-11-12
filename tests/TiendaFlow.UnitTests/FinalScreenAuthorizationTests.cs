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
