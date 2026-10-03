using ArticleService.Data;
using ArticleService.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ArticleDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ArticleDb")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddHostedService<ArticleCacheWarmupService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseHttpMetrics();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.SeedAsync();
}

app.MapGet("/", () => Results.Ok(new { service = "ArticleService", status = "ok" }));
app.MapMetrics();

app.MapGet("/api/articles/cache/status", async (ArticleCache cache) =>
    Results.Ok(await cache.StatusAsync()));

app.MapGet("/api/articles/recent", async (
    int? take,
    ArticleCache cache,
    ArticleDbContext db,
    HttpContext http) =>
{
    var count = Math.Clamp(take ?? 10, 1, 50);
    var cached = await cache.GetRecentAsync(count);

    if (cached.Count > 0)
    {
        ArticleCacheMetrics.Requests.WithLabels("hit").Inc();
        http.Response.Headers["X-Cache"] = "HIT";
        return Results.Ok(cached);
    }

    ArticleCacheMetrics.Requests.WithLabels("miss").Inc();
    http.Response.Headers["X-Cache"] = "MISS";

    var fallback = await db.Articles.AsNoTracking()
        .OrderByDescending(x => x.PublishedAtUtc)
        .Take(count)
        .ToListAsync();

    return Results.Ok(fallback);
});

app.MapGet("/api/articles/{id:int}", async (
    int id,
    ArticleCache cache,
    ArticleDbContext db,
    HttpContext http) =>
{
    var cached = await cache.GetAsync(id);
    if (cached is not null)
    {
        ArticleCacheMetrics.Requests.WithLabels("hit").Inc();
        http.Response.Headers["X-Cache"] = "HIT";
        return Results.Ok(cached);
    }

    ArticleCacheMetrics.Requests.WithLabels("miss").Inc();
    http.Response.Headers["X-Cache"] = "MISS";

    var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    return article is null ? Results.NotFound() : Results.Ok(article);
});

app.Run();
