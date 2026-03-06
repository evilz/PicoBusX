using Azure.Messaging.ServiceBus;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public class MessageSenderService
{
    private readonly ServiceBusClientFactory _factory;
    private readonly ILogger<MessageSenderService> _logger;

    public MessageSenderService(ServiceBusClientFactory factory, ILogger<MessageSenderService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task SendMessageAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var sender = client.CreateSender(request.Destination);

        var message = new ServiceBusMessage(request.JsonPayload)
        {
            ContentType = "application/json"
        };

        if (!string.IsNullOrWhiteSpace(request.Subject)) message.Subject = request.Subject;
        if (!string.IsNullOrWhiteSpace(request.MessageId)) message.MessageId = request.MessageId;
        if (!string.IsNullOrWhiteSpace(request.CorrelationId)) message.CorrelationId = request.CorrelationId;
        if (!string.IsNullOrWhiteSpace(request.SessionId)) message.SessionId = request.SessionId;
        if (request.ScheduledEnqueueTime.HasValue)
            message.ScheduledEnqueueTime = request.ScheduledEnqueueTime.Value;

        if (request.ApplicationProperties is not null)
        {
            foreach (var kv in request.ApplicationProperties)
            {
                message.ApplicationProperties[kv.Key] = kv.Value;
            }
        }

        await sender.SendMessageAsync(message, ct);
        _logger.LogInformation("Sent message to {Destination}", request.Destination);
    }
}
