using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Infrastructure;
using TiendaFlow.Web.Components;
using TiendaFlow.Web.Persistence;
using TiendaFlow.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:DefaultConnection' is not configured. " +
        "Set it with dotnet user-secrets on the Web project.");

builder.Services.AddTiendaFlowPersistence(connectionString);

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/acceso-denegado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "TiendaFlow.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToAccessDenied = context =>
            {
                RecordDeniedPageAccess(context.HttpContext);
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(TiendaFlowClaims.Permission, permission));
    }
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<DevDataSeeder>();

var app = builder.Build();

// The store employees work in Colombian Spanish and pesos.
var supportedCultures = new[] { new CultureInfo("es-CO") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-CO"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapPost("/login", async (
    HttpContext context,
    TiendaFlow.Application.Security.AuthenticationService authentication,
    [FromForm] string email,
    [FromForm] string password,
    [FromForm] string? returnUrl) =>
{
    var ip = context.Connection.RemoteIpAddress?.ToString();
    var user = authentication.SignIn(email, password, ip);

    if (user is null)
    {
        var errorTarget = string.IsNullOrWhiteSpace(returnUrl)
            ? "/login?error=1"
            : $"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}";

        return Results.Redirect(errorTarget);
    }

    await SignInClaims.SignInAsync(context, user, ip);

    var target = IsLocalUrl(returnUrl) ? returnUrl! : "/inventario";
    return Results.LocalRedirect(target);
}).AllowAnonymous();

app.MapPost("/logout", async (HttpContext context, [FromForm] string? returnUrl) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsLocalUrl(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && value.StartsWith('/')
    && !value.StartsWith("//")
    && !value.StartsWith("/\\");

// A page protected by [Authorize] is rejected by the authorization middleware
// before any application service runs, so record the denied attempt here too.
// Only the authenticated user id, the permission and the path are stored.
static void RecordDeniedPageAccess(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated != true)
        return;

    if (!int.TryParse(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var userId))
        return;

    var policy = context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>()?.Policy;
    var function = string.IsNullOrWhiteSpace(policy)
        ? context.Request.Path.Value ?? "unknown"
        : policy;

    if (function.Length > 50)
        function = function[..50];

    var path = context.Request.Path.Value ?? "/";
    if (path.Length > 200)
        path = path[..200];

    context.RequestServices.GetService<IAuditLog>()?.Record(new AuditEntry(
        "AccessDenied",
        function,
        $"Intento de acceso denegado a {path}",
        userId,
        DateTime.UtcNow,
        module: "security",
        ip: context.Connection.RemoteIpAddress?.ToString()));
}
