
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SubscriberService.Models;

namespace SubscriberService.Messaging;

// Event sent when a new subscriber is created
public record SubscriberCreatedEvent(
    int SubscriberId,
    string Email,
    DateTime SubscribedAtUtc
);

public class SubscriberEventPublisher
{
    private readonly IConfiguration _configuration;

    public SubscriberEventPublisher(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishCreatedAsync(
        Subscriber subscriber)
    {
        // 1. Configure RabbitMQ connection
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMq:Host"]
                       ?? "subscriber-queue",

            UserName = _configuration["RabbitMq:Username"]
                       ?? "subscriber",

            Password = _configuration["RabbitMq:Password"]
                       ?? throw new InvalidOperationException(
                           "RabbitMQ password is missing")
        };

        // 2. Connect to RabbitMQ
        using var connection =
            await factory.CreateConnectionAsync();

        using var channel =
            await connection.CreateChannelAsync();

        // 3. Declare durable queue
        await channel.QueueDeclareAsync(
            queue: "subscriber.created",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // 4. Create subscription event
        var message = new SubscriberCreatedEvent(
            subscriber.Id,
            subscriber.Email,
            subscriber.SubscribedAtUtc
        );

        // 5. Convert event to JSON bytes
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        // 6. Mark message as persistent
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        // 7. Publish event to RabbitMQ
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "subscriber.created",
            mandatory: true,
            basicProperties: properties,
            body: body
        );
    }
}
