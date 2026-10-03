# HappyHeadlines — complete cache assignment demo

This is a self-contained implementation of the caching exercise shown in the course requirements.

## Included

- `ArticleService` (.NET 8)
- `ArticleDatabase` (PostgreSQL)
- `ArticleCache` (Redis)
  - periodic background warm-up
  - latest 14 days
  - refresh every 30 seconds
  - stale-data handling with TTL + refresh/removal
- `CommentService` (.NET 8)
- `CommentDatabase` (PostgreSQL)
- `CommentCache` (Redis)
  - cache-aside on cache miss
  - all comments for the 30 most recently accessed articles
  - LRU eviction
  - invalidation when a new comment is posted
- Prometheus metrics
- Grafana dashboard
- Small website for manual testing
- PowerShell demo script

## Architecture

```text
                         +---------------------+
                         |      Prometheus     |
                         +----------^----------+
                                    |
                            metrics | /metrics
                                    |
              +---------------------+---------------------+
              |                                           |
       +------+--------+                           +------+--------+
       | ArticleService|                           | CommentService|
       +------+--------+                           +------+--------+
              |                                           |
          first read                                  first read
              |                                           |
       +------v--------+                           +------v--------+
       | Redis          |                           | Redis          |
       | ArticleCache   |                           | CommentCache   |
       +------+--------+                           | LRU max 30     |
              |                                    +------+--------+
      periodic warm-up                                   | MISS
              |                                           |
       +------v--------+                           +------v--------+
       | Article DB    |                           | Comment DB    |
       | PostgreSQL    |                           | PostgreSQL    |
       +---------------+                           +---------------+
```

Grafana reads Prometheus and displays cache hit ratios and supporting cache metrics.

## Start

See [RUN_ME_FIRST.md](RUN_ME_FIRST.md).

## Oral explanation

See [docs/EXPLANATION.md](docs/EXPLANATION.md).
