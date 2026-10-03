using Prometheus;

namespace CommentService.Services;

public static class CommentCacheMetrics
{
    public static readonly Counter Requests = Metrics.CreateCounter(
        "comment_cache_requests_total",
        "Number of CommentCache lookups grouped by hit or miss.",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public static readonly Gauge CachedArticleGroups = Metrics.CreateGauge(
        "comment_cache_groups",
        "Number of article comment groups currently cached.");

    public static readonly Counter Evictions = Metrics.CreateCounter(
        "comment_cache_evictions_total",
        "Number of LRU evictions from CommentCache.");

    public static readonly Counter Invalidations = Metrics.CreateCounter(
        "comment_cache_invalidations_total",
        "Number of CommentCache invalidations after a new comment is posted.");
}
