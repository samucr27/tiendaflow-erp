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
    public void PurchasingRoutesDeclareThePolicyOfTheMatrix(
        string file,
        string route,
        string permission)
    {
        var source = ReadPage(file);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains($"@attribute [Authorize(Policy = Permissions.{permission})]", source);
    }

    [Fact]
    public void MenuShowsPurchaseOrdersOnlyWithTheViewPermission()
    {
        var menu = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "TiendaFlow.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"compras/ordenes\"", menu);
        Assert.Contains("AccessControl.Has(Permissions.PurchasingOrdersView)", menu);
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
