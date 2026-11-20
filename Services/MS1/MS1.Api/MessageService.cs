namespace MS1.Api
{
    public class MessageService
    {
        private readonly ILogger<MessageService> _logger;

        public MessageService(ILogger<MessageService> logger)
        {
            _logger = logger;
        }

        public async Task SendMessage(string receiver, string message, string correlationId)
        {
            _logger.LogInformation("Sending message to {Receiver}: {Message} with CorrelationId: {CorrelationId}", receiver, correlationId);

            // Simulate sending the message (e.g., via RabbitMQ, HTTP, etc.)

            _logger.LogInformation( "Message successfully sent to {Receiver}. CorrelationId: {CorrelationId}", receiver, correlationId);

        }

    }
}
