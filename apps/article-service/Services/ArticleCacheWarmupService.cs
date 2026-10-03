using ArticleService.Data;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Services;

public class ArticleCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ArticleCache cache,
    IConfiguration configuration,
    ILogger<ArticleCacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshSeconds = configuration.GetValue("Cache:RefreshSeconds", 30);
        var recentDays = configuration.GetValue("Cache:RecentDays", 14);
        var interval = TimeSpan.FromSeconds(refreshSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-recentDays);

                var recentArticles = await db.Articles
                    .AsNoTracking()
                    .Where(x => x.PublishedAtUtc >= cutoff)
                    .OrderByDescending(x => x.PublishedAtUtc)
                    .ToListAsync(stoppingToken);

                await cache.ReplaceRecentAsync(recentArticles);
                ArticleCacheMetrics.Refreshes.WithLabels("success").Inc();
                ArticleCacheMetrics.LastSuccessfulRefreshUnix.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                logger.LogInformation("Article cache refreshed with {Count} articles from the last {Days} days.", recentArticles.Count, recentDays);
            }
            catch (Exception ex)
            {
                ArticleCacheMetrics.Refreshes.WithLabels("error").Inc();
                logger.LogError(ex, "Article cache refresh failed. Existing cached values are left untouched where possible.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
