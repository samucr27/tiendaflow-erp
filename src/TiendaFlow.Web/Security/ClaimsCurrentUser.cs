using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TiendaFlow.Application.Security;

namespace TiendaFlow.Web.Security;

public static class TiendaFlowClaims
{
    public const string Permission = "tiendaflow:permission";
    public const string StoreId = "tiendaflow:store_id";
    public const string StoreName = "tiendaflow:store_name";
    public const string RoleName = "tiendaflow:role";
    public const string FullName = "tiendaflow:name";
    public const string Ip = "tiendaflow:ip";
}

/// <summary>
/// Reads the signed-in user from the Blazor authentication state, falling back
/// to the current HTTP request during static rendering. The result is cached
/// because a Blazor circuit does not change user while it is connected.
/// </summary>
public sealed class ClaimsCurrentUser : ICurrentUser
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _cachedPrincipal;

    public ClaimsCurrentUser(
        AuthenticationStateProvider authenticationStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal Principal
    {
        get
        {
            if (_cachedPrincipal is not null)
                return _cachedPrincipal;

            var principal = _authenticationStateProvider
                .GetAuthenticationStateAsync()
                .GetAwaiter()
                .GetResult()
                .User;

            if (principal.Identity?.IsAuthenticated != true
                && _httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } httpUser)
            {
                principal = httpUser;
            }

            return _cachedPrincipal = principal;
        }
    }

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public int? UserId =>
        int.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string Name =>
        Principal.FindFirstValue(TiendaFlowClaims.FullName)
        ?? Principal.Identity?.Name
        ?? "Invitado";

    public string Email => Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public string Role => Principal.FindFirstValue(TiendaFlowClaims.RoleName) ?? string.Empty;

    public string RoleLabel =>
        string.IsNullOrEmpty(Role) ? string.Empty : SystemRoles.Label(Role);

    public int? StoreId =>
        int.TryParse(Principal.FindFirstValue(TiendaFlowClaims.StoreId), out var id) ? id : null;

    public string StoreName =>
        Principal.FindFirstValue(TiendaFlowClaims.StoreName) ?? "Sin tienda";

    public string? Ip => Principal.FindFirstValue(TiendaFlowClaims.Ip);

    public IReadOnlySet<string> Permissions =>
        Principal.FindAll(TiendaFlowClaims.Permission)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
}
