using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class LandingPageTests
{
    [Theory]
    [InlineData(RoleNames.Administrator, "/inventario")]
    [InlineData(RoleNames.CorporateManager, "/inventario")]
    [InlineData(RoleNames.StoreManager, "/inventario")]
    [InlineData(RoleNames.Cashier, "/inventario")]
    [InlineData(RoleNames.StockClerk, "/inventario")]
    [InlineData(RoleNames.LogisticsCoordinator, "/inventario")]
    [InlineData(RoleNames.HumanResourcesOfficer, "/rrhh/empleados")]
    public void EveryRoleLandsOnItsFirstAllowedPage(string role, string expected)
    {
        Assert.Equal(expected, LandingPage.For(RolePermissions.ForRole(role)));
    }

    [Fact]
    public void NoPermissions_FallsBackToInventory()
    {
        Assert.Equal(LandingPage.Fallback, LandingPage.For(new HashSet<string>()));
    }

    [Fact]
    public void InventoryWinsOverHumanResourcesAndSecurity()
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.SecurityUsersView,
            Permissions.HrEmployeesView,
            Permissions.InventoryStockView
        };

        Assert.Equal("/inventario", LandingPage.For(permissions));
    }

    [Fact]
    public void HumanResourcesWinsOverSecurityWhenInventoryIsMissing()
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.SecurityUsersView,
            Permissions.HrEmployeesView
        };

        Assert.Equal("/rrhh/empleados", LandingPage.For(permissions));
    }

    [Fact]
    public void SecurityIsTheLandingPageWhenOnlyItIsAllowed()
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.SecurityUsersView
        };

        Assert.Equal("/seguridad/usuarios", LandingPage.For(permissions));
    }
}
