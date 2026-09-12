using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "MS1";
var tableName = $"{serviceName}Messages";
var dbName = $"{serviceName}Db";

var baseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? "Server=sqlserver;Database=Master;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";
var connectionString = baseConnectionString.Contains("Database=")
    ? baseConnectionString
    : $"{baseConnectionString.TrimEnd(';')};Database={dbName};";

builder.Services.AddDbContext<ServiceDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(sp =>
    new ServiceDbContext(
        sp.GetRequiredService<DbContextOptions<ServiceDbContext>>(),
        tableName));

builder.Services.AddSingleton(sp => new RabbitMqService(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILogger<RabbitMqService>>(),
    serviceName));

builder.Services.AddHostedService<RabbitMqConsumerBackgroundService>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Auto Migrate/Ensure DB Created
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    int retries = 10;
    while (retries > 0)
    {
        try
        {
            var db = new ServiceDbContext(
                new DbContextOptionsBuilder<ServiceDbContext>().UseSqlServer(connectionString).Options,
                tableName);

            await db.Database.EnsureCreatedAsync();
            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogWarning($"Database initialization failed. Retrying in 5s... Error: {ex.Message}");
            await Task.Delay(5000);
        }
    }
}

app.Run();