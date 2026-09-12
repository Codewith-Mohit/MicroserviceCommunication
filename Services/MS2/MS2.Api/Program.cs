using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

string serviceName = "MS2";
string tableName = $"{serviceName}Messages";
string dbName = $"{serviceName}Db";

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

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = serviceName }));

app.MapPost("/api/messages/send", async (SendMessageRequest request, RabbitMqService rabbitMq, ServiceDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Receiver) || string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Receiver and Message are required.");
    }

    var msg = new MicroserviceMessage
    {
        Sender = serviceName,
        Receiver = request.Receiver.ToUpper(),
        Message = request.Message,
        MessageType = "TestMessage"
    };

    // Save initial outgoing record
    db.Messages.Add(new LocalMessageRecord
    {
        MessageId = msg.MessageId,
        CorrelationId = msg.CorrelationId,
        Sender = msg.Sender,
        Receiver = msg.Receiver,
        Message = msg.Message,
        MessageType = msg.MessageType,
        Status = "Sent",
        CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    // Publish to RabbitMQ
    await rabbitMq.PublishMessageAsync(msg);

    // Poll DB for ACK response up to 5 seconds
    var timeout = DateTime.UtcNow.AddSeconds(5);
    LocalMessageRecord? ackRecord = null;

    while (DateTime.UtcNow < timeout)
    {
        await Task.Delay(300);
        ackRecord = await db.Messages
            .FirstOrDefaultAsync(m => m.CorrelationId == msg.CorrelationId && m.MessageType == "Acknowledgement");
        if (ackRecord != null) break;
    }

    return Results.Ok(new
    {
        Status = ackRecord != null ? "Success" : "Pending/Timeout",
        Sender = msg.Sender,
        Receiver = msg.Receiver,
        OriginalMessage = msg.Message,
        Response = ackRecord?.Message ?? "No ACK received within timeout",
        CorrelationId = msg.CorrelationId
    });
});

app.MapGet("/api/messages/history", async (ServiceDbContext db) =>
{
    var history = await db.Messages.OrderByDescending(m => m.CreatedAt).Take(20).ToListAsync();
    return Results.Ok(history);
});

app.Run();