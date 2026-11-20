using MicroserviceCommunication.UI;
using MicroserviceCommunication.UI.Components;
using MicroserviceCommunication.UI.Models;
using MicroserviceCommunication.UI.Services;
using Serilog;

try
{
    Log.Information("Starting Blazor UI application...");

    var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog();

// Configure strongly-typed configuration binding
// The configuration is loaded from appsettings.json hierarchy:
    // 1. appsettings.json (base configuration)
    // 2. appsettings.{ASPNETCORE_ENVIRONMENT}.json (environment-specific overrides)
    // 3. Environment variables (highest priority)
    var serviceConfig = new ServiceConfiguration();
    builder.Configuration.GetSection("Services").Bind(serviceConfig.Services);
    builder.Configuration.GetSection("Database").Bind(serviceConfig.Database);
    builder.Configuration.GetSection("MessageBroker").Bind(serviceConfig.MessageBroker);
    serviceConfig.EnvironmentName = builder.Environment.EnvironmentName;

    // Register configuration in dependency injection container
    builder.Services.AddSingleton(serviceConfig);
    builder.Services.AddSingleton(sp => sp.GetRequiredService<ServiceConfiguration>().Services);
    builder.Services.AddSingleton(sp => sp.GetRequiredService<ServiceConfiguration>().Database);
    builder.Services.AddSingleton(sp => sp.GetRequiredService<ServiceConfiguration>().MessageBroker);

    // Register the configuration service for use in components
    builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddHttpClient();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
    }

    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
