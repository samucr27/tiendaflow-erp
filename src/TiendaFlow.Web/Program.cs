using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Security;
using TiendaFlow.Infrastructure;
using TiendaFlow.Infrastructure.Persistence;
using TiendaFlow.Web.Components;
using TiendaFlow.Web.Persistence;
using TiendaFlow.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// App Service captures stdout/stderr, so console logging is the production
// transport. No file sink is needed and no log file is written to wwwroot.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// App Service terminates TLS and forwards the original protocol and client IP.
// The platform front end is not a fixed proxy address, so the known networks
// list is cleared; this is the configuration the ASP.NET Core App Service
// guidance documents.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:DefaultConnection' is not configured. " +
        "Set it with dotnet user-secrets on the Web project, with " +
        "ConnectionStrings__DefaultConnection in App Service, or with " +
        "`terraform` app settings.");

// Azure SQL requires encryption. The Terraform connection string already sets
// Encrypt=True; forcing it for every non-development environment keeps a
// manually configured deployment from silently connecting without TLS.
if (!builder.Environment.IsDevelopment())
{
    var sqlBuilder = new SqlConnectionStringBuilder(connectionString) { Encrypt = true };
    connectionString = sqlBuilder.ConnectionString;
}

builder.Services.AddTiendaFlowPersistence(connectionString);

// Data Protection keys must survive a restart and a slot swap. /home is the
// only durable, writable path on a Linux App Service plan.
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionPath) && builder.Environment.IsProduction())
    dataProtectionPath = "/home/.tiendaflow/keys";

if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    Directory.CreateDirectory(dataProtectionPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
        .SetApplicationName("TiendaFlow");
}

// Payroll module (HU-04): the engine depends only on abstractions (DIP) and
// each strategy owns the rules of one family of contract types (SRP).
builder.Services.AddSingleton<IPayrollParametersProvider, DefaultPayrollParametersProvider>();
builder.Services.AddSingleton<IWithholdingCalculator, WithholdingCalculator>();
builder.Services.AddSingleton<IPayrollStrategy, EmploymentPayrollStrategy>();
builder.Services.AddSingleton<IPayrollStrategy, ServiceProvisionPayrollStrategy>();
builder.Services.AddScoped<IPayrollEngine, PayrollEngine>();
builder.Services.AddScoped<PayrollRunService>();

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
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
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

// The demo seeder is opt-in. It never runs unless Seed:Enabled is explicitly
// true, and it reads the user passwords from configuration (Seed:AdminPassword
// and Seed:DemoPassword), never from code.
if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    builder.Services.AddHostedService<DevDataSeeder>();

var app = builder.Build();

// Migrations run only when Database:MigrateOnStartup is true. The default is
// false: production schema changes go through the EF migration bundle that the
// deployment guide runs explicitly.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<TiendaFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

// The store employees work in Colombian Spanish and pesos.
var supportedCultures = new[] { new CultureInfo("es-CO") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-CO"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

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

    var target = IsLocalUrl(returnUrl) ? returnUrl! : LandingPage.For(user.Permissions);
    return Results.LocalRedirect(target);
}).AllowAnonymous();

app.MapPost("/logout", async (HttpContext context, [FromForm] string? returnUrl) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

// HU-04: monthly payroll calculation for one employee. Protected by the HR
// payroll permission; the request and the result carry no personal data
// beyond the employee id, and nothing is logged here.
app.MapPost("/api/nomina/calculate", (PayrollInput input, IPayrollEngine engine) =>
{
    try
    {
        return Results.Ok(engine.Calculate(input));
    }
    catch (Exception ex) when (ex is InvalidPayrollInputException
                                   or UnsupportedContractTypeException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization(Permissions.HrPayrollManage);

// Liveness/readiness probe for App Service. It returns 503 when the database
// is unreachable so a bad deployment is visible in the platform health check.
app.MapGet("/healthz", HealthCheckAsync).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task<IResult> HealthCheckAsync(
    TiendaFlowDbContext dbContext,
    CancellationToken cancellationToken)
{
    try
    {
        if (await dbContext.Database.CanConnectAsync(cancellationToken))
            return Results.Ok(new { status = "healthy" });
    }
    catch
    {
        // The database is down or the connection string is wrong; answer 503
        // instead of leaking the exception through the health endpoint.
    }

    return Results.Json(
        new { status = "unhealthy" },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

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