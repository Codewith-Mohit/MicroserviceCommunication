using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Shared.Contracts.Models;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
  .Enrich.FromLogContext()
      .Enrich.WithProperty("Service", "MS1")
  .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
  .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting MS2 API application...");

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

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
    });

    builder.Services.Configure<HostOptions>(options =>
    {
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Add global exception handler
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var correlationId = context.Items.ContainsKey("CorrelationId") ? context.Items["CorrelationId"]?.ToString() : "Unknown";

            logger.LogError(ex, "Unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var errorResponse = new ErrorResponse
            {
                Message = "An unexpected error occurred. Please try again later.",
                Details = ex.Message,
                StackTrace = app.Environment.IsDevelopment() ? ex.StackTrace : null,
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                StatusCode = StatusCodes.Status500InternalServerError
            };

            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    });

    // Auto Migrate/Ensure DB Created
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();

        int retries = 10;
        while (retries > 0)
        {
            try
            {
                logger.LogInformation("Attempting to connect and initialize database...");

                // Option A: If NOT using EF Migrations (Creates DB & Schema safely if missing)
                await dbContext.Database.EnsureCreatedAsync();

                // Option B: If using EF Migrations (Recommended for microservices)
                // await dbContext.Database.MigrateAsync();

                logger.LogInformation("Database initialization completed successfully.");
                break; // Success! Exit loop.
            }
            catch (Exception ex)
            {
                retries--;
                if (retries == 0)
                {
                    logger.LogError(ex, "Database initialization failed after 10 retries.");
                    throw; // Rethrow to let the container crash gracefully if DB is truly dead
                }

                logger.LogWarning(ex, "SQL Server not ready yet. Retrying in 5 seconds... ({RetriesLeft} retries left).", retries);
                await Task.Delay(5000);
            }
        }
    }

    app.UseCors("AllowAll");

    app.MapGet("/health", (ILogger<Program> logger) =>
    {
        logger.LogInformation("Health check endpoint called");
        return Results.Ok(new { Status = "Healthy", Service = serviceName });
    });

    app.MapPost("/api/messages/send", async (SendMessageRequest request, RabbitMqService rabbitMq, ServiceDbContext db, ILogger<Program> logger) =>
    {
        using (LogContext.PushProperty("MessageType", "Send"))
        {
            logger.LogInformation("Processing message send request to {Receiver}", request.Receiver);

            if (string.IsNullOrWhiteSpace(request.Receiver) || string.IsNullOrWhiteSpace(request.Message))
            {
                logger.LogWarning("Invalid message request: Receiver or Message is null/empty");
                return Results.BadRequest(new ErrorResponse
                {
                    Message = "Receiver and Message are required.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    Path = "/api/messages/send"
                });
            }

            var msg = new MicroserviceMessage
            {
                Sender = serviceName,
                Receiver = request.Receiver.ToUpper(),
                Message = request.Message,
                MessageType = "TestMessage"
            };

            try
            {
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

                logger.LogInformation("Message saved to database. MessageId: {MessageId}, CorrelationId: {CorrelationId}", msg.MessageId, msg.CorrelationId);

                // Publish to RabbitMQ
                await rabbitMq.PublishMessageAsync(msg);

                logger.LogInformation("Message published to RabbitMQ. MessageId: {MessageId}", msg.MessageId);

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

                if (ackRecord != null)
                {
                    logger.LogInformation("Acknowledgement received. MessageId: {MessageId}", msg.MessageId);
                }
                else
                {
                    logger.LogWarning("No acknowledgement received within timeout. MessageId: {MessageId}", msg.MessageId);
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message send request. MessageId: {MessageId}", msg.MessageId);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    });

    app.MapGet("/api/messages/history", async (ServiceDbContext db, ILogger<Program> logger) =>
    {
        logger.LogInformation("Fetching message history");
        try
        {
            var history = await db.Messages.OrderByDescending(m => m.CreatedAt).Take(20).ToListAsync();
            logger.LogInformation("Retrieved {Count} messages from history", history.Count);
            return Results.Ok(history);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching message history");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

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