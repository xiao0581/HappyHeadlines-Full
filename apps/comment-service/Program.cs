using CommentService.Data;
using CommentService.Models;
using CommentService.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CommentDb")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

builder.Services.AddSingleton<CommentCache>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseHttpMetrics();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.SeedAsync();
}

app.MapGet("/", () => Results.Ok(new { service = "CommentService", status = "ok" }));
app.MapMetrics();

app.MapGet("/api/comments/cache/status", async (CommentCache cache) =>
    Results.Ok(await cache.StatusAsync()));

app.MapGet("/api/comments/article/{articleId:int}", async (
    int articleId,
    CommentCache cache,
    CommentDbContext db,
    HttpContext http) =>
{
    var cached = await cache.GetAsync(articleId);
    if (cached is not null)
    {
        CommentCacheMetrics.Requests.WithLabels("hit").Inc();
        http.Response.Headers["X-Cache"] = "HIT";
        return Results.Ok(cached);
    }

    CommentCacheMetrics.Requests.WithLabels("miss").Inc();
    http.Response.Headers["X-Cache"] = "MISS";

    var comments = await db.Comments.AsNoTracking()
        .Where(x => x.ArticleId == articleId)
        .OrderBy(x => x.CreatedAtUtc)
        .ToListAsync();

    await cache.StoreAsync(articleId, comments);
    return Results.Ok(comments);
});

app.MapPost("/api/comments", async (
    CreateCommentRequest request,
    CommentCache cache,
    CommentDbContext db) =>
{
    if (request.ArticleId <= 0 || string.IsNullOrWhiteSpace(request.Author) || string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "ArticleId, Author and Text are required." });

    var comment = new Comment
    {
        ArticleId = request.ArticleId,
        Author = request.Author.Trim(),
        Text = request.Text.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Comments.Add(comment);
    await db.SaveChangesAsync();

    // Avoid stale comment data. The next read becomes a MISS and repopulates from the database.
    await cache.InvalidateAsync(request.ArticleId);
    return Results.Created($"/api/comments/article/{request.ArticleId}", comment);
});

app.Run();
