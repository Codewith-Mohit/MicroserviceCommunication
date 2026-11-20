namespace MicroserviceCommunication.UI.Models;

public class ServiceConfiguration
{
    public ServicesConfig Services { get; set; } = new();

    public DatabaseConfig Database { get; set; } = new();

    public MessageBrokerConfig MessageBroker { get; set; } = new();

    public string EnvironmentName { get; set; } = "Unknown";
}

public class ServicesConfig
{

    public string MS1 { get; set; } = "http://localhost:5001";

    public string MS2 { get; set; } = "http://localhost:5002";

    public string MS3 { get; set; } = "http://localhost:5003";
}

public class DatabaseConfig
{

    public string Server { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the database port.
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string Database { get; set; } = "microservices_db";

    /// <summary>
    /// Gets or sets the database user name.
    /// Should be provided via environment variables or Key Vault in production.
    /// </summary>
    public string UserId { get; set; } = "postgres";

    /// <summary>
    /// Gets or sets the database password.
    /// Should NEVER be stored in appsettings files in production environments.
    /// Use Azure Key Vault, Kubernetes Secrets, or environment variables instead.
    /// </summary>
    public string Password { get; set; } = "password";

    /// <summary>
    /// Constructs a connection string from the configuration values.
    /// </summary>
    public string GetConnectionString()
    {
        return $"Server={Server};Port={Port};Database={Database};User Id={UserId};Password={Password};";
    }
}

/// <summary>
/// Configuration for message broker (e.g., RabbitMQ, Azure Service Bus).
/// </summary>
public class MessageBrokerConfig
{
    /// <summary>
    /// Gets or sets the message broker host/server address.
    /// Example: "localhost" (local), "rabbitmq-service" (k3d), "myservice.servicebus.windows.net" (AKS with Azure Service Bus)
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the message broker port.
    /// Default: 5672 for RabbitMQ AMQP, 15672 for management HTTP
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the broker type (e.g., "RabbitMQ", "AzureServiceBus").
    /// Useful for conditional logic in the application.
    /// </summary>
    public string BrokerType { get; set; } = "RabbitMQ";

    /// <summary>
    /// Gets or sets the username for broker authentication.
    /// Should be provided via environment variables or Key Vault in production.
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the password for broker authentication.
    /// Should NEVER be stored in appsettings files in production environments.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the connection string for Azure Service Bus (AKS with ASB).
    /// This is alternative to Host/Port/Username/Password for Azure Service Bus.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
