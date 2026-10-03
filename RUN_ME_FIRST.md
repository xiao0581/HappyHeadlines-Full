# Run me first — Windows / VS Code

## Requirements

Install:

1. Docker Desktop
2. VS Code

You do **not** need to install PostgreSQL, Redis, Prometheus, Grafana or .NET locally when you run the project with Docker.

## Start

Open this whole folder in VS Code.

Open **Terminal -> New Terminal** and make sure the terminal is in the folder that contains `docker-compose.yml`.

Run:

```powershell
docker compose up --build
```

The first build can take several minutes because Docker downloads .NET, PostgreSQL, Redis, Prometheus and Grafana images and restores NuGet packages.

When the services are running, open:

- Website: http://localhost:8080
- ArticleService: http://localhost:5001
- CommentService: http://localhost:5002
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000
  - username: `admin`
  - password: `admin`

Grafana's `HappyHeadlines Cache Monitoring` dashboard is provisioned automatically.

## Demo the assignment

Open a second VS Code terminal and run:

```powershell
.\demo.ps1
```

The script demonstrates:

- Article cache HIT for a recent article
- Article cache MISS for an article older than 14 days
- Comment cache MISS followed by HIT
- Filling 31 comment groups so LRU evicts one and keeps only 30

Then open Grafana and watch the metrics.

## Stop

```powershell
docker compose down
```

To also delete all database/Redis/Grafana volumes and start completely clean next time:

```powershell
docker compose down -v
```
