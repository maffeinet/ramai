# RAMAI — Sprint 0 Backlog

## S0-001 Repository baseline
Struttura cartelle, `.gitignore`, root README, naming.

## S0-002 Backend solution
Solution .NET 10, API, Domain/Application/Infrastructure e moduli placeholder. `dotnet build` green.

## S0-003 Frontend baseline
Next.js + TypeScript, pagina iniziale placeholder, lint/type-check/build green.

## S0-004 PostgreSQL + pgvector-ready
PostgreSQL in Docker, DB `ramai_dev`, health, env config, connessione test backend.

## S0-005 Redis
Redis in Docker, health, env config, verifica backend.

## S0-006 Docker Compose
PostgreSQL + Redis, network, volumes, healthcheck. `docker compose up -d` da macchina pulita.

## S0-007 Configuration & secrets
`.env.example`, configurazioni local/dev, nessun secret committato.

## S0-008 Health & diagnostics
`/health`, check DB, check Redis, correlation id.

## S0-009 Logging baseline
Structured logging, environment/correlation id, nessun payload sensibile.

## S0-010 Testing baseline
Unit, integration skeleton, architecture tests. `dotnet test` green.

## S0-011 CI baseline
Build/test backend, frontend install/build/lint, fallimento su errori.

## S0-012 Developer bootstrap
README completo e script opzionali dev-up/dev-down.

## Exit gate
Non iniziare Sprint 1 finché S0-001..S0-012 non sono chiusi e verificati.
