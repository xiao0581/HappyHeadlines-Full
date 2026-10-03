using System.Text.Json;
using CommentService.Models;
using StackExchange.Redis;

namespace CommentService.Services;

public class CommentCache
{
    private const string LruIndexKey = "comment-cache:lru";
    private readonly IDatabase _redis;
    private readonly int _maxArticles;

    public CommentCache(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis.GetDatabase();
        _maxArticles = configuration.GetValue("Cache:MaxArticles", 30);
    }

    private static string Key(int articleId) => $"comment-cache:article:{articleId}";
    private static double NowScore() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<IReadOnlyList<Comment>?> GetAsync(int articleId)
    {
        var value = await _redis.StringGetAsync(Key(articleId));
        if (value.IsNullOrEmpty) return null;

        await _redis.SortedSetAddAsync(LruIndexKey, articleId.ToString(), NowScore());
        return JsonSerializer.Deserialize<List<Comment>>(value.ToString()) ?? [];
    }

    public async Task StoreAsync(int articleId, IReadOnlyList<Comment> comments)
    {
        await _redis.StringSetAsync(Key(articleId), JsonSerializer.Serialize(comments));
        await _redis.SortedSetAddAsync(LruIndexKey, articleId.ToString(), NowScore());
        await EnforceLruLimitAsync();
    }

    public async Task InvalidateAsync(int articleId)
    {
        await _redis.KeyDeleteAsync(Key(articleId));
        await _redis.SortedSetRemoveAsync(LruIndexKey, articleId.ToString());
        CommentCacheMetrics.Invalidations.Inc();
        await UpdateGroupGaugeAsync();
    }

    private async Task EnforceLruLimitAsync()
    {
        while (await _redis.SortedSetLengthAsync(LruIndexKey) > _maxArticles)
        {
            var oldest = await _redis.SortedSetRangeByRankAsync(LruIndexKey, 0, 0, Order.Ascending);
            if (oldest.Length == 0) break;

            var articleIdText = oldest[0].ToString();
            if (int.TryParse(articleIdText, out var articleId))
                await _redis.KeyDeleteAsync(Key(articleId));

            await _redis.SortedSetRemoveAsync(LruIndexKey, oldest[0]);
            CommentCacheMetrics.Evictions.Inc();
        }

        await UpdateGroupGaugeAsync();
    }

    private async Task UpdateGroupGaugeAsync()
    {
        var count = await _redis.SortedSetLengthAsync(LruIndexKey);
        CommentCacheMetrics.CachedArticleGroups.Set(count);
    }

    public async Task<object> StatusAsync()
    {
        var members = await _redis.SortedSetRangeByRankWithScoresAsync(LruIndexKey, 0, -1, Order.Descending);
        return new
        {
            maxArticles = _maxArticles,
            cachedArticleCount = members.Length,
            mostRecentFirst = members.Select(x => new
            {
                articleId = x.Element.ToString(),
                lastAccessScore = x.Score
            }).ToArray()
        };
    }
}
