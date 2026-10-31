
using FeatureHubSDK;
using Microsoft.EntityFrameworkCore;
using SubscriberService.Data;
using SubscriberService.Models;
using SubscriberService.Messaging;
using System.Net.Mail;

var builder = WebApplication.CreateBuilder(args);

// 1. Register PostgreSQL
builder.Services.AddDbContext<SubscriberDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("SubscriberDb")
    );
});

// 2. Register RabbitMQ Publisher
builder.Services.AddSingleton<SubscriberEventPublisher>();

// 3. Configure FeatureHub
string edgeUrl =
    builder.Configuration["FEATUREHUB_EDGE_URL"]
    ?? throw new InvalidOperationException(
        "FeatureHub Edge URL is missing");

string apiKey =
    builder.Configuration["FEATUREHUB_API_KEY"]
    ?? throw new InvalidOperationException(
        "FeatureHub API Key is missing");

var featureHub = new EdgeFeatureHubConfig(edgeUrl, apiKey);

await featureHub.Init();

var app = builder.Build();

// 4. FeatureHub Release Toggle Middleware
app.Use(async (context, next) =>
{
    // Only protect subscriber business APIs
    if (context.Request.Path.StartsWithSegments(
        "/api/subscribers"))
    {
        bool enabled = featureHub
            .Repository["SubscriberServiceEnabled"]
            .IsEnabled;

        if (!enabled)
        {
            context.Response.StatusCode = 503;

            await context.Response.WriteAsJsonAsync(new
            {
                error = "SubscriberService is disabled"
            });

            return;
        }
    }

    await next(context);
});

// 5. Create database tables
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<SubscriberDbContext>();

    await db.Database.EnsureCreatedAsync();
}

// Service status
app.MapGet("/", () =>
    Results.Ok(new
    {
        service = "SubscriberService",
        status = "running"
    })
);

// Health check
app.MapGet("/health", () =>
    Results.Ok(new { status = "healthy" })
);

// Count active subscribers
app.MapGet("/api/subscribers/count",
    async (SubscriberDbContext db) =>
{
    int count = await db.Subscribers
        .CountAsync(x => x.IsActive);

    return Results.Ok(new { count });
});

// Get all active subscribers
app.MapGet("/api/subscribers",
    async (SubscriberDbContext db) =>
{
    var subscribers = await db.Subscribers
        .AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Id)
        .Select(x => new
        {
            x.Id,
            x.Email,
            x.SubscribedAtUtc
        })
        .ToListAsync();

    return Results.Ok(subscribers);
});

// POST: Subscribe
app.MapPost("/api/subscribers",
    async (
        SubscribeRequest request,
        SubscriberDbContext db,
        SubscriberEventPublisher publisher) =>
{
    string email = request.Email?.Trim().ToLowerInvariant()
                   ?? string.Empty;

    if (!MailAddress.TryCreate(email, out var address)
        || address.Address != email)
    {
        return Results.BadRequest(new
        {
            error = "Invalid email address"
        });
    }

    var existing = await db.Subscribers
        .FirstOrDefaultAsync(x => x.Email == email);

    if (existing != null)
    {
        if (existing.IsActive)
        {
            return Results.Conflict(new
            {
                error = "Email already subscribed"
            });
        }

        existing.IsActive = true;
        existing.SubscribedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Subscription reactivated",
            subscriber = existing
        });
    }

    var subscriber = new Subscriber
    {
        Email = email,
        SubscribedAtUtc = DateTime.UtcNow,
        IsActive = true
    };

    db.Subscribers.Add(subscriber);
    await db.SaveChangesAsync();

    // Publish event to RabbitMQ
    await publisher.PublishCreatedAsync(subscriber);

    return Results.Created(
        $"/api/subscribers/{subscriber.Id}",
        subscriber
    );
});

// DELETE: Unsubscribe
app.MapDelete("/api/subscribers/{id:int}",
    async (
        int id,
        SubscriberDbContext db) =>
{
    var subscriber = await db.Subscribers.FindAsync(id);

    if (subscriber == null)
    {
        return Results.NotFound(new
        {
            error = "Subscriber not found"
        });
    }

    subscriber.IsActive = false;

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();

public record SubscribeRequest(string Email);
