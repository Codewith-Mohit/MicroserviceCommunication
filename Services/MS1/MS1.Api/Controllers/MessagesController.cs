using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace MS1.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly RabbitMqService _rabbitMqService;
        private readonly ServiceDbContext _serviceContext;
        private readonly string _serviceName ="MS1";

        public MessagesController(RabbitMqService rabbitMqService, ServiceDbContext serviceContext)
        {
            _rabbitMqService = rabbitMqService;
            _serviceContext = serviceContext;
        }

        [HttpGet("/health")]
        public IActionResult GetHealth()
        {
            return Ok(new { Status = "Healthy" });
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
          
            if(string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.Receiver))
                return BadRequest("Message & Receiver cannot be empty.");

            var msg = new MicroserviceMessage
            {
                Sender = _serviceName,
                Receiver = request.Receiver.ToUpper(),
                Message = request.Message,
                MessageType = "Test Message"
            };

            _serviceContext.Messages.Add( new LocalMessageRecord()
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

            await _serviceContext.SaveChangesAsync();

            await _rabbitMqService.PublishMessageAsync(msg);

            var timeout = DateTime.UtcNow.AddSeconds(5);
            LocalMessageRecord? ackRecord = null;

            while (DateTime.UtcNow < timeout)
            {
                await Task.Delay(300);

                ackRecord = await _serviceContext.Messages
                    .Where(m => m.MessageId == msg.MessageId && m.Status == "Acknowledged")
                    .FirstOrDefaultAsync();
                if (ackRecord != null) break;
            }


            return Ok( new
            {
                Status = ackRecord != null ? "Success" : "Pending/Timeout",
                Sender = msg.Sender,
                Receiver = msg.Receiver,
                OriginalMessage = msg.Message,
                Response = ackRecord?.Message ?? "No ACK received within timeout",
                CorrelationId = msg.CorrelationId
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var history = await _serviceContext.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .ToListAsync();

            return Ok(history);
        }
    }
}
