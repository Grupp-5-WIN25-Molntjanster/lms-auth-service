using Azure.Messaging.ServiceBus;
using Lms.Auth.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Lms.Auth.Infrastructure.Messaging;

/// <summary>
/// Publishes verification email messages to Azure Service Bus.
/// The Email Service (other student) consumes these messages.
/// </summary>
public class ServiceBusPublisher : IServiceBusPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public ServiceBusPublisher(IConfiguration configuration)
    {
        var connectionString = configuration["AzureServiceBus:ConnectionString"]!;
        var queueName = configuration["AzureServiceBus:VerifyQueueName"] ?? "verify-queue";

        var client = new ServiceBusClient(connectionString);
        _sender = client.CreateSender(queueName);
    }

    public async Task PublishVerificationEmailAsync(VerificationMessage message)
    {
        var json = JsonSerializer.Serialize(message);

        var serviceBusMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            Subject = "EmailVerification"
        };

        // Custom properties for filtering
        serviceBusMessage.ApplicationProperties.Add("MessageType", "EmailVerification");
        serviceBusMessage.ApplicationProperties.Add("UserId", message.UserId);

        //await _sender.SendMessageAsync(serviceBusMessage);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}