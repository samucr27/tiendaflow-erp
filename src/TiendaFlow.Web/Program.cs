using System.Globalization;
using Microsoft.AspNetCore.Localization;
using TiendaFlow.Infrastructure;
using TiendaFlow.Web.Components;
using TiendaFlow.Web.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:DefaultConnection' is not configured. " +
        "Set it with dotnet user-secrets on the Web project.");

builder.Services.AddTiendaFlowPersistence(connectionString);

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
