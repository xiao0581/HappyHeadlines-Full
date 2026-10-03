# Teacher requirements checklist

## Part 1 — Article Cache

- [x] Redis is used as the cache.
- [x] Cache is between ArticleService and ArticleDatabase.
- [x] Cache is filled by a periodic background/offline process.
- [x] Cache contains articles from the latest 14 days.
- [x] Refresh frequency is configurable (default 30 seconds).
- [x] Stale data is handled with refresh + TTL and removal of items outside the 14-day window.

## Part 2 — Comment Cache

- [x] Redis is used as the cache.
- [x] Cache is between CommentService and CommentDatabase.
- [x] Cache is populated only after a cache miss.
- [x] Each entry contains all comments for one article.
- [x] Cache keeps at most 30 most recently accessed article groups.
- [x] LRU eviction removes the least recently used group when full.
- [x] Posting a comment invalidates that article's cache to avoid stale comments.

## Part 3 — Monitoring

- [x] Article cache HIT/MISS metrics.
- [x] Comment cache HIT/MISS metrics.
- [x] Hit ratio can be displayed for both cache layers.
- [x] Prometheus collects metrics.
- [x] Grafana dashboard is provisioned automatically.
- [x] `demo.ps1` demonstrates cache HIT, MISS and LRU.
- [x] Strengths, limitations and trade-offs are documented in `docs/EXPLANATION.md`.
