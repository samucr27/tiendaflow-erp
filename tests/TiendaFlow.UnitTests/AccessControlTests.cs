using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class AccessControlTests
{
    [Fact]
    public void Has_ReturnsTrueOnlyForThePermissionsOfTheCurrentUser()
    {
        var user = new FakeCurrentUser(permissions: new HashSet<string>
        {
            Permissions.SalesPosUse
        });

        var accessControl = new AccessControl(user, new RecordingAuditLog());

        Assert.True(accessControl.Has(Permissions.SalesPosUse));
        Assert.False(accessControl.Has(Permissions.SecurityUsersManage));
    }

    [Fact]
    public void Demand_WhenAllowed_DoesNotAuditAnything()
    {
        var audit = new RecordingAuditLog();
        var accessControl = new AccessControl(new FakeCurrentUser(), audit);

        accessControl.Demand(Permissions.InventoryStockView);

        Assert.Empty(audit.Entries);
    }

    [Fact]
    public void Demand_WhenDenied_ThrowsAndAuditsTheAuthenticatedUser()
    {
        var audit = new RecordingAuditLog();
        var user = new FakeCurrentUser(
            userId: 7,
            role: RoleNames.Cashier,
            permissions: new HashSet<string> { Permissions.SalesPosUse },
            ip: "10.0.0.9");

        var accessControl = new AccessControl(user, audit);

        var exception = Assert.Throws<AccessDeniedException>(
            () => accessControl.Demand(Permissions.SecurityUsersManage));

        Assert.Equal(Permissions.SecurityUsersManage, exception.Permission);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("AccessDenied", entry.Action);
        Assert.Equal(Permissions.SecurityUsersManage, entry.EntityId);
        Assert.Equal(7, entry.UserId);
        Assert.Equal("security", entry.Module);
        Assert.Equal("10.0.0.9", entry.Ip);
    }

    [Fact]
    public void Demand_WhenNotAuthenticated_ThrowsWithoutAuditingAUser()
    {
        var audit = new RecordingAuditLog();
        var user = new FakeCurrentUser(
            userId: null,
            isAuthenticated: false,
            permissions: new HashSet<string>());

        var accessControl = new AccessControl(user, audit);

        Assert.Throws<AccessDeniedException>(
            () => accessControl.Demand(Permissions.SecurityUsersView));

        Assert.Empty(audit.Entries);
    }
}
