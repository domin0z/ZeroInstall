using Microsoft.EntityFrameworkCore;
using ZeroInstall.Dashboard.Api;
using ZeroInstall.Dashboard.Data;
using ZeroInstall.Dashboard.Hubs;
using ZeroInstall.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
var dashboardConfig = new DashboardConfiguration();
builder.Configuration.GetSection("Dashboard").Bind(dashboardConfig);
builder.Services.AddSingleton(dashboardConfig);

// Database
builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={dashboardConfig.DatabasePath}"));

// Services
builder.Services.AddScoped<IDashboardDataService, DashboardDataService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<INasScannerService, NasScannerService>();
builder.Services.AddHostedService<NasScannerBackgroundService>();

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthHandler>(
        "ApiKey", null);
builder.Services.AddAuthorization();

// Blazor
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// Auto-create/migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
