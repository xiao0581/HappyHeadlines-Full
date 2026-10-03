using System.Text.Json;
using ArticleService.Models;
using StackExchange.Redis;

namespace ArticleService.Services;

public class ArticleCache
{
    private const string RecentIndexKey = "article-cache:recent-index";
    private readonly IDatabase _redis;
    private readonly TimeSpan _ttl;

    public ArticleCache(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis.GetDatabase();
        var ttlSeconds = configuration.GetValue("Cache:TtlSeconds", 120);
        _ttl = TimeSpan.FromSeconds(ttlSeconds);
    }

    private static string ArticleKey(int id) => $"article-cache:article:{id}";

    public async Task<Article?> GetAsync(int id)
    {
        var value = await _redis.StringGetAsync(ArticleKey(id));
        return value.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<Article>(value.ToString());
    }

    public async Task<IReadOnlyList<Article>> GetRecentAsync(int take)
    {
        var ids = await _redis.SortedSetRangeByRankAsync(
            RecentIndexKey, 0, Math.Max(0, take - 1), Order.Descending);

        if (ids.Length == 0) return [];

        var keys = ids
            .Select(x => (RedisKey)ArticleKey(int.Parse(x.ToString())))
            .ToArray();

        var values = await _redis.StringGetAsync(keys);
        var result = new List<Article>(values.Length);

        foreach (var value in values)
        {
            if (value.IsNullOrEmpty) continue;
            var article = JsonSerializer.Deserialize<Article>(value.ToString());
            if (article is not null) result.Add(article);
        }

        return result;
    }

    public async Task ReplaceRecentAsync(IReadOnlyCollection<Article> articles)
    {
        var previousMembers = await _redis.SortedSetRangeByRankAsync(RecentIndexKey, 0, -1);
        var currentIds = articles.Select(x => x.Id).ToHashSet();

        foreach (var previous in previousMembers)
        {
            if (int.TryParse(previous.ToString(), out var id) && !currentIds.Contains(id))
                await _redis.KeyDeleteAsync(ArticleKey(id));
        }

        await _redis.KeyDeleteAsync(RecentIndexKey);

        foreach (var article in articles)
        {
            var json = JsonSerializer.Serialize(article);
            await _redis.StringSetAsync(ArticleKey(article.Id), json, _ttl);
            await _redis.SortedSetAddAsync(
                RecentIndexKey,
                article.Id.ToString(),
                new DateTimeOffset(article.PublishedAtUtc).ToUnixTimeSeconds());
        }

        ArticleCacheMetrics.CachedItems.Set(articles.Count);
    }

    public async Task<object> StatusAsync()
    {
        var ids = await _redis.SortedSetRangeByRankAsync(RecentIndexKey, 0, -1, Order.Descending);
        return new
        {
            cachedArticleCount = ids.Length,
            cachedArticleIds = ids.Select(x => x.ToString()).ToArray()
        };
    }
}
