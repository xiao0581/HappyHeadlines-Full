
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace NewsletterService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // 1. Configure RabbitMQ connection
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMq:Host"]
                ?? "localhost",

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

        // 3. Declare the subscriber queue
        await channel.QueueDeclareAsync(
            queue: "subscriber.created",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // 4. Process one message at a time
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false
        );

        // 5. Create consumer
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            // Copy message from RabbitMQ
            byte[] body = ea.Body.ToArray();

            try
            {
                // 6. Deserialize JSON
                string json = Encoding.UTF8.GetString(body);

                var message =
                    JsonSerializer.Deserialize<SubscriberCreatedEvent>(
                        json);

                if (message == null)
                {
                    throw new InvalidOperationException(
                        "Invalid subscriber event");
                }

                _logger.LogInformation(
                    "New subscriber received. ID: {Id}",
                    message.SubscriberId);

                // 7. Simulate sending welcome email
                _logger.LogInformation(
                    "Welcome email processed for subscriber {Id}",
                    message.SubscriberId);

                // 8. Acknowledge successful processing
                await channel.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process subscription event");

                // Reject invalid/failed message
                await channel.BasicNackAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    requeue: false
                );
            }
        };

        // 9. Start consuming
        await channel.BasicConsumeAsync(
            queue: "subscriber.created",
            autoAck: false,
            consumer: consumer
        );

        _logger.LogInformation(
            "NewsletterService is waiting for messages...");

        // 10. Keep the worker running
        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "NewsletterService is stopping.");
        }
    }
}

// Message contract
public record SubscriberCreatedEvent(
    int SubscriberId,
    string Email,
    DateTime SubscribedAtUtc
);
