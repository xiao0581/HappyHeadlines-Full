# HappyHeadlines caching — explanation for the oral demo

## 1. ArticleCache

The ArticleCache sits between `ArticleService` and `ArticleDatabase`.

It is **pre-populated by a periodic background process** (`ArticleCacheWarmupService`). Every 30 seconds the worker queries the database for articles published during the latest 14 days and writes them to Redis. A Redis sorted set keeps the IDs ordered by publish time.

Normal request path:

```text
User -> ArticleService -> Redis ArticleCache
                         | HIT  -> return article
                         | MISS -> ArticleDatabase -> return article
```

Important: the request path does not populate the ArticleCache. The periodic process does. This matches the requirement that the cache is filled by an offline/periodic process.

Stale-data handling: cached article objects have a TTL of 120 seconds while the worker refreshes every 30 seconds. The refresh also removes IDs that are no longer inside the 14-day window.

Trade-off: a shorter refresh interval gives fresher data but creates more database/network traffic. A longer interval reduces load but increases the chance of stale data.

## 2. CommentCache

The CommentCache sits between `CommentService` and `CommentDatabase`.

It uses **cache-aside/lazy loading**. It starts empty. When a request arrives:

```text
Request comments
      |
      v
Check Redis
  | HIT  -> update LRU access time -> return
  | MISS -> query DB -> put all comments for that article in Redis -> return
```

The cache stores comment groups for a maximum of **30 recently accessed articles**.

## 3. LRU

LRU means **Least Recently Used**. Redis keeps a sorted set in which the score is the last access time. When the cache contains more than 30 article groups, the group with the oldest score is removed.

Example: if 30 article groups are cached and article 31 is requested, the least recently accessed group is evicted, so the cache stays at 30 groups.

When a new comment is posted, that article's cached comment group is deleted. The next read becomes a MISS, reloads all comments from the database and stores a fresh group. This prevents stale comment data.

## 4. Monitoring

Both APIs expose `/metrics`. Prometheus scrapes those endpoints every 5 seconds. Grafana reads Prometheus and shows:

- Article cache hit ratio
- Comment cache hit ratio
- HIT vs MISS counters
- Number of warm articles
- Number of comment groups in the LRU cache
- LRU evictions
- Comment invalidations
- Article refresh errors

Hit ratio formula:

```text
hits / (hits + misses) * 100
```

## 5. Why the two caches use different strategies

**ArticleCache:** recent news is predictably important, so it is useful to pre-warm the latest 14 days. Users can get a HIT even on the first request after the warm-up.

**CommentCache:** it is hard to predict which article comments users will open. Lazy loading avoids filling memory with comment groups nobody reads. LRU keeps the most recently used 30 groups.

## 6. Geographic idea

In a real deployment, each region could run its own Redis cache close to its local ArticleService/CommentService. A user in Denmark can then read from a European cache instead of making every request to a North American database. The included Docker project simulates the architecture locally; it does not create real cloud regions.
