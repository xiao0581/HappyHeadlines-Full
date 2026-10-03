$ErrorActionPreference = "Stop"

Write-Host "HappyHeadlines cache demo" -ForegroundColor Cyan
Write-Host "Waiting for the periodic ArticleCache warm-up..."

$ready = $false
for ($i = 0; $i -lt 20; $i++) {
    try {
        $status = Invoke-RestMethod http://localhost:5001/api/articles/cache/status
        if ($status.cachedArticleCount -gt 0) { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 2
}
if (-not $ready) {
    throw "ArticleCache did not become ready. Check: docker compose ps"
}

Write-Host "`n1) ArticleCache HIT: article 1 is recent and was pre-warmed by the background process." -ForegroundColor Yellow
$r = Invoke-WebRequest -UseBasicParsing http://localhost:5001/api/articles/1
Write-Host "X-Cache = $($r.Headers['X-Cache'])"

Write-Host "`n2) ArticleCache MISS: article 20 is older than 14 days and is not in the warm cache." -ForegroundColor Yellow
$r = Invoke-WebRequest -UseBasicParsing http://localhost:5001/api/articles/20
Write-Host "X-Cache = $($r.Headers['X-Cache'])"

Write-Host "`nResetting only the CommentCache so MISS/HIT is deterministic..."
docker compose exec -T comment-cache redis-cli FLUSHALL | Out-Null

Write-Host "`n3) CommentCache MISS then HIT for article 1." -ForegroundColor Yellow
$r1 = Invoke-WebRequest -UseBasicParsing http://localhost:5002/api/comments/article/1
$r2 = Invoke-WebRequest -UseBasicParsing http://localhost:5002/api/comments/article/1
Write-Host "First request  X-Cache = $($r1.Headers['X-Cache'])"
Write-Host "Second request X-Cache = $($r2.Headers['X-Cache'])"

Write-Host "`n4) Fill CommentCache with 31 article groups to force LRU eviction." -ForegroundColor Yellow
1..31 | ForEach-Object {
    Invoke-WebRequest -UseBasicParsing "http://localhost:5002/api/comments/article/$_" | Out-Null
}
$status = Invoke-RestMethod http://localhost:5002/api/comments/cache/status
Write-Host "Cached article groups = $($status.cachedArticleCount) / $($status.maxArticles)"
Write-Host "The cache stays at 30 because the least recently used group is evicted."

Write-Host "`nOpen these in your browser:" -ForegroundColor Green
Write-Host "Website:    http://localhost:8080"
Write-Host "Grafana:    http://localhost:3000   (admin / admin)"
Write-Host "Prometheus: http://localhost:9090"
