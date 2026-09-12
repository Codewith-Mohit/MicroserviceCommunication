using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Shared.Contracts;

public class MicroserviceMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public string Sender { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageType { get; set; } = "TestMessage"; // "TestMessage" or "Acknowledgement"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LocalMessageRecord
{
    [Key]
    public int Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid CorrelationId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // "Sent", "Received", "Acknowledged"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SendMessageRequest
{
    public string Receiver { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ServiceDbContext : DbContext
{
    private readonly string _tableName;

    public ServiceDbContext(DbContextOptions<ServiceDbContext> options, string tableName = "Messages")
        : base(options)
    {
        _tableName = tableName;
    }

    public DbSet<LocalMessageRecord> Messages => Set<LocalMessageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalMessageRecord>().ToTable(_tableName);
        base.OnModelCreating(modelBuilder);
    }
}