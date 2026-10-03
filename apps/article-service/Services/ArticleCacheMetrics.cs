using Prometheus;

namespace ArticleService.Services;

public static class ArticleCacheMetrics
{
    public static readonly Counter Requests = Metrics.CreateCounter(
        "article_cache_requests_total",
        "Number of ArticleCache lookups grouped by hit or miss.",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public static readonly Gauge CachedItems = Metrics.CreateGauge(
        "article_cache_items",
        "Number of articles currently present in the ArticleCache warm set.");

    public static readonly Counter Refreshes = Metrics.CreateCounter(
        "article_cache_refresh_total",
        "Number of ArticleCache refresh attempts grouped by result.",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public static readonly Gauge LastSuccessfulRefreshUnix = Metrics.CreateGauge(
        "article_cache_last_successful_refresh_unixtime",
        "Unix timestamp of the latest successful ArticleCache refresh.");
}
