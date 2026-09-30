# Repository Structure

```text
ramai/
├─ AGENTS.md
├─ README.md
├─ .env.example
├─ docker-compose.yml
├─ src/
│  ├─ backend/
│  │  ├─ Ramai.sln
│  │  ├─ Ramai.Api/
│  │  ├─ Ramai.Domain/
│  │  ├─ Ramai.Application/
│  │  ├─ Ramai.Infrastructure/
│  │  └─ Modules/
│  │     ├─ Ramai.Modules.Identity/
│  │     ├─ Ramai.Modules.Tenants/
│  │     ├─ Ramai.Modules.Connectors/
│  │     ├─ Ramai.Modules.Memory/
│  │     ├─ Ramai.Modules.Insights/
│  │     ├─ Ramai.Modules.Assistant/
│  │     └─ Ramai.Modules.Billing/
│  └─ frontend/ramai-web/
├─ tests/
│  ├─ Ramai.UnitTests/
│  ├─ Ramai.IntegrationTests/
│  └─ Ramai.ArchitectureTests/
├─ infra/
├─ scripts/
└─ docs/adr/
```
