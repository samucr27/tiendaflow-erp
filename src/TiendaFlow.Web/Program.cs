using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Infrastructure.Persistence;
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

builder.Services.AddDbContext<TiendaFlowDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddScoped<IProductRepository, EfProductRepository>();
builder.Services.AddScoped<IStockRepository, EfStockRepository>();
builder.Services.AddScoped<IAuditLog, EfAuditLog>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<ProductPriceService>();

if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<DevDataSeeder>();

var app = builder.Build();

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
