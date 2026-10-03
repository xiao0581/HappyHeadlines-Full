#!/usr/bin/env sh
set -eu

echo "Waiting for ArticleCache warm-up..."
i=0
while [ "$i" -lt 20 ]; do
  count=$(curl -sf http://localhost:5001/api/articles/cache/status | sed -n 's/.*"cachedArticleCount":\([0-9][0-9]*\).*/\1/p' || true)
  if [ -n "$count" ] && [ "$count" -gt 0 ]; then break; fi
  sleep 2
  i=$((i+1))
done

printf '\nArticleCache HIT (recent article):\n'
curl -si http://localhost:5001/api/articles/1 | grep -i '^X-Cache:' || true
printf '\nArticleCache MISS (older than 14 days):\n'
curl -si http://localhost:5001/api/articles/20 | grep -i '^X-Cache:' || true

docker compose exec -T comment-cache redis-cli FLUSHALL >/dev/null
printf '\nCommentCache MISS then HIT:\n'
curl -si http://localhost:5002/api/comments/article/1 | grep -i '^X-Cache:' || true
curl -si http://localhost:5002/api/comments/article/1 | grep -i '^X-Cache:' || true

printf '\nFill 31 comment groups to trigger LRU eviction...\n'
i=1
while [ "$i" -le 31 ]; do curl -s "http://localhost:5002/api/comments/article/$i" >/dev/null; i=$((i+1)); done
curl -s http://localhost:5002/api/comments/cache/status
printf '\n\nWebsite: http://localhost:8080\nGrafana: http://localhost:3000 (admin/admin)\nPrometheus: http://localhost:9090\n'
