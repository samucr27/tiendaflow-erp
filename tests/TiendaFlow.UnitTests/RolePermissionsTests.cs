using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class RolePermissionsTests
{
    [Fact]
    public void EverySystemRoleHasAtLeastOnePermission()
    {
        foreach (var role in RoleNames.All)
            Assert.NotEmpty(RolePermissions.ForRole(role));
    }

    [Fact]
    public void UnknownRoleHasNoPermissions()
    {
        Assert.Empty(RolePermissions.ForRole("Unknown role"));
        Assert.False(RolePermissions.IsAllowed("Unknown role", Permissions.InventoryProductsView));
    }

    [Fact]
    public void EveryGrantedPermissionExistsInThePermissionCatalog()
    {
        var matrix = RolePermissions.All;

        Assert.Equal(RoleNames.All.Count, matrix.Count);

        foreach (var (role, permissions) in matrix)
        {
            Assert.Contains(role, RoleNames.All);

            foreach (var permission in permissions)
                Assert.True(Permissions.IsKnown(permission), $"{role} grants unknown {permission}.");
        }
    }

    [Theory]
    [InlineData(RoleNames.Administrator, Permissions.SecurityUsersManage, true)]
    [InlineData(RoleNames.Administrator, Permissions.PurchasingOrdersCreate, false)]
    [InlineData(RoleNames.Cashier, Permissions.InventoryProductsManage, false)]
    [InlineData(RoleNames.Cashier, Permissions.SalesPosUse, true)]
    [InlineData(RoleNames.StockClerk, Permissions.InventoryStockReceive, true)]
    [InlineData(RoleNames.StockClerk, Permissions.InventoryPriceChange, false)]
    [InlineData(RoleNames.LogisticsCoordinator, Permissions.PurchasingOrdersApprove, false)]
    [InlineData(RoleNames.StoreManager, Permissions.PurchasingOrdersApprove, true)]
    [InlineData(RoleNames.HumanResourcesOfficer, Permissions.HrPayrollManage, true)]
    [InlineData(RoleNames.HumanResourcesOfficer, Permissions.SalesPosUse, false)]
    public void Matrix_GrantsOnlyTheFunctionsOfEachRole(
        string role,
        string permission,
        bool expected)
    {
        Assert.Equal(expected, RolePermissions.IsAllowed(role, permission));
    }

    [Fact]
    public void OnlyTheAdministratorCanManageUsersAndRoles()
    {
        foreach (var role in RoleNames.All.Where(role => role != RoleNames.Administrator))
        {
            Assert.False(RolePermissions.IsAllowed(role, Permissions.SecurityUsersManage));
            Assert.False(RolePermissions.IsAllowed(role, Permissions.SecurityRolesManage));
        }

        Assert.True(RolePermissions.IsAllowed(RoleNames.Administrator, Permissions.SecurityUsersManage));
        Assert.True(RolePermissions.IsAllowed(RoleNames.Administrator, Permissions.SecurityRolesManage));
    }

    [Fact]
    public void StoresAreManagedByTheAdministratorAndReadByTheCorporateManager()
    {
        Assert.True(RolePermissions.IsAllowed(RoleNames.Administrator, Permissions.SecurityStoresView));
        Assert.True(RolePermissions.IsAllowed(RoleNames.Administrator, Permissions.SecurityStoresManage));
        Assert.True(RolePermissions.IsAllowed(RoleNames.CorporateManager, Permissions.SecurityStoresView));
        Assert.False(RolePermissions.IsAllowed(RoleNames.CorporateManager, Permissions.SecurityStoresManage));

        foreach (var role in new[]
                 {
                     RoleNames.StoreManager,
                     RoleNames.Cashier,
                     RoleNames.StockClerk,
                     RoleNames.LogisticsCoordinator,
                     RoleNames.HumanResourcesOfficer
                 })
        {
            Assert.False(RolePermissions.IsAllowed(role, Permissions.SecurityStoresView));
            Assert.False(RolePermissions.IsAllowed(role, Permissions.SecurityStoresManage));
        }
    }
}
