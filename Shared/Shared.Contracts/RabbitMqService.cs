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

    public async Task InitializeAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "rabbitmq",
            Port = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
            UserName = _config["RabbitMQ:Username"] ?? "guest",
            Password = _config["RabbitMQ:Password"] ?? "guest",
            VirtualHost = _config["RabbitMQ:VirtualHost"] ?? "/"
        };

        int retries = 10;
        while (retries > 0)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();
                break;
            }
            catch (Exception ex)
            {
                retries--;
                _logger.LogWarning($"RabbitMQ Connection failed. Retrying in 3 seconds... ({retries} left). Error: {ex.Message}");
                await Task.Delay(3000);
            }
        }

        if (_channel == null) throw new Exception("Could not connect to RabbitMQ.");

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        string queueName = $"{CurrentServiceName.ToLower()}.queue";
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: ExchangeName,
            routingKey: CurrentServiceName);

        _logger.LogInformation($"Initialized RabbitMQ binding for queue: {queueName} with routing key: {CurrentServiceName}");
    }

    public async Task PublishMessageAsync(MicroserviceMessage msg)
    {
        if (_channel == null) await InitializeAsync();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel!.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: msg.Receiver.ToUpper(),
            mandatory: true,
            basicProperties: props,
            body: body);

        _logger.LogInformation($"[RabbitMQ Published] {msg.Sender} -> {msg.Receiver} | Type: {msg.MessageType} | CorrelationId: {msg.CorrelationId}");
    }

    public async Task StartConsumingAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_channel == null) await InitializeAsync();

        string queueName = $"{CurrentServiceName.ToLower()}.queue";
        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var msg = JsonSerializer.Deserialize<MicroserviceMessage>(json);

            if (msg != null)
            {
                _logger.LogInformation($"[RabbitMQ Received] {msg.Sender} -> {msg.Receiver} | Type: {msg.MessageType} | CorrelationId: {msg.CorrelationId}");

                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();

                // Save received record
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

                // If receiving a test message intended for us, send an ACK back
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

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
        };

        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }
}

public class RabbitMqConsumerBackgroundService : BackgroundService
{
    private readonly RabbitMqService _rabbitMqService;
    private readonly IServiceProvider _serviceProvider;

    public RabbitMqConsumerBackgroundService(RabbitMqService rabbitMqService, IServiceProvider serviceProvider)
    {
        _rabbitMqService = rabbitMqService;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _rabbitMqService.InitializeAsync();
        await _rabbitMqService.StartConsumingAsync(_serviceProvider, stoppingToken);
    }
}