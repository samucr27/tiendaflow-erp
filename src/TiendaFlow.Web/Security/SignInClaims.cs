using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TiendaFlow.Application.Security;

namespace TiendaFlow.Web.Security;

/// <summary>
/// Turns an authenticated application user into the claims the authorization
/// policies and ICurrentUser read. One permission claim is emitted per
/// permission granted by the role matrix.
/// </summary>
public static class SignInClaims
{
    public static async Task SignInAsync(
        HttpContext context,
        AuthenticatedUser user,
        string? ip)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleName),
            new(TiendaFlowClaims.FullName, user.FullName),
            new(TiendaFlowClaims.RoleName, user.RoleName),
            new(TiendaFlowClaims.StoreId, user.StoreId?.ToString() ?? string.Empty),
            new(TiendaFlowClaims.StoreName, user.StoreName),
            new(TiendaFlowClaims.Ip, ip ?? string.Empty)
        };

        claims.AddRange(user.Permissions.Select(permission =>
            new Claim(TiendaFlowClaims.Permission, permission)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }
}
