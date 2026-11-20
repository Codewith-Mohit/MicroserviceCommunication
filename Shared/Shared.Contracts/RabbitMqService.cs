using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Shared.Contracts;

public class RabbitMqService
{
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMqService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public string CurrentServiceName { get; }
    public const string ExchangeName = "microservice.exchange";

    public RabbitMqService(IConfiguration config, ILogger<RabbitMqService> logger, string currentServiceName)
    {
        _config = config;
        _logger = logger;
        CurrentServiceName = currentServiceName.ToUpper();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Explicitly check Environment Variable double-underscore notation as well as standard colon notation
        string host = _config["RabbitMQ:Host"]
                   ?? _config["RabbitMQ:HostName"]
                   ?? _config["RabbitMQ__Host"]
                   ?? _config["RabbitMQ__HostName"]
                   ?? "rabbitmq-service";

        int port = 5672;
        var rawPort = _config["RabbitMQ:Port"] ?? _config["RabbitMQ__Port"];
        if (!string.IsNullOrEmpty(rawPort) && int.TryParse(rawPort, out var parsedPort))
        {
            port = parsedPort;
        }

        string user = _config["RabbitMQ:Username"] ?? _config["RabbitMQ__Username"] ?? "guest";
        string pass = _config["RabbitMQ:Password"] ?? _config["RabbitMQ__Password"] ?? "guest";

        _logger.LogInformation("[RabbitMQ] Attempting connection to {Host}:{Port}", host, port);

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = pass,
            VirtualHost = _config["RabbitMQ:VirtualHost"] ?? "/"
        };

        int retries = 30; // Extended retries for K8s pod startup timing
        while (retries > 0 && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
                _logger.LogInformation("[RabbitMQ] Successfully connected to {Host}:{Port}", host, port);
                break;
            }
            catch (Exception ex)
            {
                retries--;
                _logger.LogWarning(ex, "[RabbitMQ] Connection to {Host}:{Port} failed. Retrying in 4s... ({Retries} retries left)", host, port, retries);

                if (retries == 0)
                {
                    _logger.LogError("[RabbitMQ] Critical: Failed to connect to {Host}:{Port} after 30 attempts.", host, port);
                    return; // Return safely without throwing an exception to keep ASP.NET Core web host running
                }

                await Task.Delay(4000, cancellationToken);
            }
        }

        if (_channel == null) return;

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        string queueName = $"{CurrentServiceName.ToLower()}.queue";
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: ExchangeName,
            routingKey: CurrentServiceName,
            cancellationToken: cancellationToken);

        _logger.LogInformation("[RabbitMQ] Initialized binding for queue: {QueueName} with key: {RoutingKey}", queueName, CurrentServiceName);
    }

    public async Task PublishMessageAsync(MicroserviceMessage msg)
    {
        if (_channel == null) await InitializeAsync();
        if (_channel == null)
        {
            _logger.LogWarning("[RabbitMQ] Cannot publish message {CorrelationId}: Channel unavailable.", msg.CorrelationId);
            return;
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: msg.Receiver.ToUpper(),
            mandatory: true,
            basicProperties: props,
            body: body);

        _logger.LogInformation("[RabbitMQ Published] {Sender} -> {Receiver} | CorrelationId: {CorrelationId}", msg.Sender, msg.Receiver, msg.CorrelationId);
    }

    public async Task StartConsumingAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_channel == null) await InitializeAsync(cancellationToken);
        if (_channel == null)
        {
            _logger.LogWarning("[RabbitMQ] Consumer omitted: Channel unavailable.");
            return;
        }

        string queueName = $"{CurrentServiceName.ToLower()}.queue";
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var msg = JsonSerializer.Deserialize<MicroserviceMessage>(json);

            if (msg != null)
            {
                _logger.LogInformation("[RabbitMQ Received] {Sender} -> {Receiver} | Type: {MessageType} | CorrelationId: {CorrelationId}", msg.Sender, msg.Receiver, msg.MessageType, msg.CorrelationId);

                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();

                db.Messages.Add(new LocalMessageRecord
                {
                    MessageId = msg.MessageId,
                    CorrelationId = msg.CorrelationId,
                    Sender = msg.Sender,
                    Receiver = msg.Receiver,
                    Message = msg.Message,
                    MessageType = msg.MessageType,
                    Status = msg.MessageType == "Acknowledgement" ? "Acknowledged" : "Received",
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(cancellationToken);

                if (msg.MessageType == "TestMessage" && msg.Receiver.Equals(CurrentServiceName, StringComparison.OrdinalIgnoreCase))
                {
                    var ackMsg = new MicroserviceMessage
                    {
                        MessageId = Guid.NewGuid(),
                        CorrelationId = msg.CorrelationId,
                        Sender = CurrentServiceName,
                        Receiver = msg.Sender,
                        Message = $"ACK: Message received by {CurrentServiceName}",
                        MessageType = "Acknowledgement",
                        Timestamp = DateTime.UtcNow
                    };

                    await PublishMessageAsync(ackMsg);
                }
            }

            await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        };

        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }
}

public class RabbitMqConsumerBackgroundService : BackgroundService
{
    private readonly RabbitMqService _rabbitMqService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumerBackgroundService> _logger;

    public RabbitMqConsumerBackgroundService(RabbitMqService rabbitMqService, IServiceProvider serviceProvider, ILogger<RabbitMqConsumerBackgroundService> logger)
    {
        _rabbitMqService = rabbitMqService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _rabbitMqService.InitializeAsync(stoppingToken);
            await _rabbitMqService.StartConsumingAsync(_serviceProvider, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ BackgroundService] Non-fatal exception encountered during execution.");
        }
    }
}