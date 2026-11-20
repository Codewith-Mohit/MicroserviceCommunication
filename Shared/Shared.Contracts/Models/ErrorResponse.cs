namespace Shared.Contracts.Models;

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? StackTrace { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Path { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, string>? AdditionalInfo { get; set; }
}
