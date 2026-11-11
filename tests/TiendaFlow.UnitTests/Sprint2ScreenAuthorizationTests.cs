using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Guards the wiring between the permission matrix and the Sprint 2 screens.
/// These are structural tests: they read the Razor sources so a page or the
/// menu cannot drift to a permission different from the one the matrix grants.
/// </summary>
public class Sprint2ScreenAuthorizationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [InlineData("Proveedores/Proveedores.razor", "/proveedores", "PurchasingSuppliersView")]
    [InlineData("Proveedores/ProveedorForm.razor", "/proveedores/nuevo", "PurchasingSuppliersManage")]
    [InlineData("Proveedores/ProveedorForm.razor", "/proveedores/{Id:int}", "PurchasingSuppliersManage")]
    [InlineData("Facturacion/Facturas.razor", "/facturacion", "SalesInvoicesView")]
    [InlineData("Facturacion/FacturaForm.razor", "/facturacion/nueva", "SalesInvoicesManage")]
    [InlineData("Facturacion/FacturaForm.razor", "/facturacion/{Id:int}", "SalesInvoicesManage")]
    [InlineData("Crm/Clientes.razor", "/crm/clientes", "CrmCustomersView")]
    [InlineData("Crm/ClienteForm.razor", "/crm/clientes/nuevo", "CrmCustomersManage")]
    [InlineData("Crm/ClienteForm.razor", "/crm/clientes/{Id:int}", "CrmCustomersManage")]
    public void EverySprint2RouteDeclaresThePolicyOfTheMatrix(
        string file,
        string route,
        string permission)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Pages", file));

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains($"@attribute [Authorize(Policy = Permissions.{permission})]", source);
    }

    [Fact]
    public void SuppliersAreVisibleToFourRolesAndManagedOnlyByLogistics()
    {
        Assert.Equal(
            new[]
            {
                RoleNames.Administrator,
                RoleNames.CorporateManager,
                RoleNames.StoreManager,
                RoleNames.LogisticsCoordinator
            },
            RolesWith(Permissions.PurchasingSuppliersView));

        Assert.Equal(
            new[] { RoleNames.LogisticsCoordinator },
            RolesWith(Permissions.PurchasingSuppliersManage));
    }

    [Fact]
    public void InvoicingIsVisibleAndManagedByTheStoreRoles()
    {
        var expected = new[]
        {
            RoleNames.Administrator,
            RoleNames.CorporateManager,
            RoleNames.StoreManager,
            RoleNames.Cashier
        };

        Assert.Equal(expected, RolesWith(Permissions.SalesInvoicesView));
        Assert.Equal(expected, RolesWith(Permissions.SalesInvoicesManage));
    }

    [Fact]
    public void CrmCustomersAreVisibleAndManagedByTheStoreRoles()
    {
        var expected = new[]
        {
            RoleNames.Administrator,
            RoleNames.CorporateManager,
            RoleNames.StoreManager,
            RoleNames.Cashier
        };

        Assert.Equal(expected, RolesWith(Permissions.CrmCustomersView));
        Assert.Equal(expected, RolesWith(Permissions.CrmCustomersManage));
    }

    [Fact]
    public void MenuShowsEachSprint2ModuleOnlyWithItsViewPermission()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"proveedores\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.PurchasingSuppliersView)", menu);

        Assert.Contains("href=\"facturacion\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.SalesInvoicesView)", menu);

        Assert.Contains("href=\"crm/clientes\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.CrmCustomersView)", menu);
    }

    [Fact]
    public void MenuHasNoPendingPlaceholdersForTheSprint2Modules()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.DoesNotContain("Fase 3", menu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Próximamente", menu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Coming soon", menu, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] RolesWith(string permission) =>
        RoleNames.All.Where(role => RolePermissions.IsAllowed(role, permission)).ToArray();

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
