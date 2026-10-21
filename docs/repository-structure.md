# Repository Structure

Aggiornamento 21 ottobre: ADR-010 permette riferimenti API agli adapter Identity
e Tenants soltanto in Composition; handler tramite contratti Application. I moduli
non leggono tabelle altrui. API workspace opt-in descritta nella
[baseline S1-008](workspace-api-baseline.md); sotto rimane anche lo storico S1-003.

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
│  │  ├─ Persistence/
│  │  │  ├─ Ramai.Modules.Identity.Persistence/
│  │  │  └─ Ramai.Modules.Tenants.Persistence/
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

S1-003 aggiunge due adapter di persistenza degli stessi moduli nel monolite, non
nuovi servizi. Modelli in Modules restano puri; ownership, schemi e procedure sono in
[database-migrations.md](database-migrations.md). I layer condivisi e l'API non
dipendono dagli adapter in questo blocco. EF CLI è fissata in `.config/dotnet-tools.json`.
