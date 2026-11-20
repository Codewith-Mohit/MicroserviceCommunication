using MicroserviceCommunication.UI.Models;

namespace MicroserviceCommunication.UI.Services;

/// <summary>
/// Service for accessing application configuration throughout the Blazor application.
/// This service demonstrates how to consume the strongly-typed ServiceConfiguration.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the complete service configuration.
    /// </summary>
    ServiceConfiguration GetConfiguration();

    /// <summary>
    /// Gets the services endpoints configuration.
    /// </summary>
    ServicesConfig GetServices();

    /// <summary>
    /// Gets the database configuration.
    /// </summary>
    DatabaseConfig GetDatabase();

    /// <summary>
    /// Gets the message broker configuration.
    /// </summary>
    MessageBrokerConfig GetMessageBroker();

    /// <summary>
    /// Gets the current environment name (Development, K3d, Production, etc.).
    /// </summary>
    string GetEnvironment();

    /// <summary>
    /// Gets a specific microservice endpoint by name (MS1, MS2, MS3).
    /// </summary>
    /// <param name="serviceName">The microservice name (e.g., "MS1")</param>
    /// <returns>The service endpoint URL</returns>
    /// <exception cref="ArgumentException">Thrown if the service name is not found</exception>
    string GetServiceEndpoint(string serviceName);

    /// <summary>
    /// Gets the database connection string constructed from the configuration.
    /// </summary>
    string GetDatabaseConnectionString();

    /// <summary>
    /// Gets a display-friendly summary of the current configuration for debugging.
    /// </summary>
    ConfigurationSummary GetConfigurationSummary();
}

/// <summary>
/// Default implementation of IConfigurationService.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ServiceConfiguration _configuration;

    public ConfigurationService(ServiceConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public ServiceConfiguration GetConfiguration()
    {
        return _configuration;
    }

    public ServicesConfig GetServices()
    {
        return _configuration.Services;
    }

    public DatabaseConfig GetDatabase()
    {
        return _configuration.Database;
    }

    public MessageBrokerConfig GetMessageBroker()
    {
        return _configuration.MessageBroker;
    }

    public string GetEnvironment()
    {
        return _configuration.EnvironmentName;
    }

    public string GetServiceEndpoint(string serviceName)
    {
        return serviceName.ToUpper() switch
        {
            "MS1" => _configuration.Services.MS1,
            "MS2" => _configuration.Services.MS2,
            "MS3" => _configuration.Services.MS3,
            _ => throw new ArgumentException($"Unknown service: {serviceName}", nameof(serviceName))
        };
    }

    public string GetDatabaseConnectionString()
    {
        return _configuration.Database.GetConnectionString();
    }

    public ConfigurationSummary GetConfigurationSummary()
    {
        return new ConfigurationSummary
        {
            EnvironmentName = _configuration.EnvironmentName,
            MS1Endpoint = _configuration.Services.MS1,
            MS2Endpoint = _configuration.Services.MS2,
            MS3Endpoint = _configuration.Services.MS3,
            DatabaseServer = _configuration.Database.Server,
            DatabasePort = _configuration.Database.Port,
            DatabaseName = _configuration.Database.Database,
            MessageBrokerHost = _configuration.MessageBroker.Host,
            MessageBrokerPort = _configuration.MessageBroker.Port,
            MessageBrokerType = _configuration.MessageBroker.BrokerType
        };
    }
}

/// <summary>
/// Summary view of configuration for debugging and display purposes.
/// Suitable for showing in UI components without exposing sensitive data.
/// </summary>
public class ConfigurationSummary
{
    public string EnvironmentName { get; set; } = "Unknown";
    public string MS1Endpoint { get; set; } = string.Empty;
    public string MS2Endpoint { get; set; } = string.Empty;
    public string MS3Endpoint { get; set; } = string.Empty;
    public string DatabaseServer { get; set; } = string.Empty;
    public int DatabasePort { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string MessageBrokerHost { get; set; } = string.Empty;
    public int MessageBrokerPort { get; set; }
    public string MessageBrokerType { get; set; } = string.Empty;
}
