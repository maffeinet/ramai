# RAMAI

Monolite modulare RAMAI. Il repository è attualmente nella fase **Sprint 0 / Foundation**.

## Obiettivo
A fine Sprint 0 deve essere possibile clonare il repository, configurare `.env`, avviare PostgreSQL e Redis con Docker Compose, avviare API ASP.NET Core e frontend Next.js, verificare `/health` ed eseguire test/lint con esito positivo.

## Ordine di lettura
1. `AGENTS.md`
2. `PROMPT_CODEX_SPRINT0.md`
3. `tasks/SPRINT0_BACKLOG.md`
4. `docs/definition-of-done.md`
5. `docs/repository-structure.md`
6. `docs/adr/*`
7. `reference_docs/11_Codex_Development_Specification_v1.0.pdf`
8. `reference_docs/04_Technical_Architecture_v1.0.pdf`

Non implementare ancora connector reali, AI Engine, Business Memory funzionale, billing o automazioni autonome.

## Prerequisiti attuali

- Git e PowerShell (Windows PowerShell 5.1 o PowerShell 7).
- .NET 10 SDK: CI verificata con 10.0.401; vedere `global.json`.
- Node.js 24.19.0 (vedi `.nvmrc`) con npm 11.
- Docker Desktop avviato, engine Linux e Compose v2 con supporto `--wait`.
- Porte libere: PostgreSQL 5432, Redis 6379, API 8080, frontend 3000.

## Bootstrap da un nuovo clone (PowerShell)

```powershell
git clone https://github.com/maffeinet/ramai.git
cd ramai
Copy-Item .env.example .env
notepad .env
```

Sostituire il placeholder della password, salvare e chiudere l'editor. Non sovrascrivere
un `.env` esistente. Usare una password locale casuale, ad esempio esadecimale, senza
`$`, interpolazioni o commenti inline. Il loader supporta `KEY=value`, commenti su righe
separate e valori racchiusi in apici; non espande variabili né esegue il contenuto.

Nella stessa shell, dalla root:

```powershell
. .\scripts\Import-DevEnvironment.ps1
docker compose config --quiet
docker compose up -d --wait --wait-timeout 120
docker compose ps
dotnet restore .\src\backend\Ramai.sln
dotnet build .\src\backend\Ramai.sln --no-restore
dotnet run --no-build --project .\src\backend\Ramai.Api\Ramai.Api.csproj
```

Lasciare questa shell aperta: l'API ascolta su `http://localhost:8080`.
In una seconda shell dalla root avviare il frontend:

```powershell
Set-Location .\src\frontend\ramai-web
npm ci
npm run dev
```

Aprire `http://localhost:3000`. In una terza shell verificare l'API:

```powershell
$response = Invoke-WebRequest http://localhost:8080/health -UseBasicParsing `
  -Headers @{ 'X-Correlation-ID' = 'ramai-bootstrap-check' }
$response.StatusCode
$response.Headers['X-Correlation-ID']
$response.Content
```

Attesi: HTTP 200, stesso correlation ID e stato Healthy per applicazione, PostgreSQL e
Redis. Il frontend è un placeholder: non chiama ancora l'API. Le variabili URL
frontend/API sono predisposizioni, non integrazioni già implementate.

Per fermare API e frontend usare Ctrl+C nelle rispettive shell, poi dalla root:

```powershell
docker compose down
```

Questo conserva i volumi. Non usare `down --volumes` nel normale flusso di sviluppo.
Gli script opzionali dev-up/dev-down non sono necessari: i comandi restano espliciti.

## Configurazione locale

1. Copiare `.env.example` in `.env`.
2. Sostituire `RAMAI_DB_PASSWORD` con una password esclusivamente locale.
3. Non aggiungere `.env`, credenziali, certificati o chiavi al repository.

ASP.NET Core carica la configurazione standard da `appsettings.json`, dal file specifico
dell'ambiente e dalle variabili d'ambiente. PostgreSQL e Redis sono collegati tramite
Infrastructure. Il file `.env` viene letto da Compose, non automaticamente da `dotnet`.

## Backend

```powershell
dotnet restore .\src\backend\Ramai.sln
dotnet build .\src\backend\Ramai.sln --no-restore
. .\scripts\Import-DevEnvironment.ps1
dotnet run --project .\src\backend\Ramai.Api\Ramai.Api.csproj
```

L'API Foundation espone la route radice e `/health`. Il progetto Infrastructure registra
il verificatore di connettività PostgreSQL/Redis usato dall'health check applicativo.

## Health e diagnostica

Con API, PostgreSQL e Redis avviati:

```powershell
Invoke-WebRequest http://localhost:8080/health `
  -Headers @{ "X-Correlation-ID" = "ramai-local-check" }
```

`/health` verifica applicazione, PostgreSQL e Redis e restituisce un JSON sintetico. Gli
health check applicativi sono distinti dagli health check dei container Docker.

Ogni risposta HTTP contiene `X-Correlation-ID`. Un valore esterno composto da caratteri
alfanumerici, `-`, `_`, `.` o `:` e lungo al massimo 128 caratteri viene riutilizzato;
altrimenti l'API genera un nuovo identificatore.

Il logging usa il JSON console formatter standard ASP.NET Core. I log delle richieste
includono timestamp UTC, livello, categoria, environment e correlation ID; non devono
contenere credenziali, token, connection string o payload sensibili.

## PostgreSQL, pgvector e Redis

Creare la configurazione locale e impostare una password non condivisa:

```powershell
Copy-Item .env.example .env
```

Quindi avviare l'infrastruttura:

```powershell
docker compose config --quiet
docker compose up -d --wait --wait-timeout 120
docker compose ps
```

PostgreSQL usa un volume persistente e abilita `vector` durante la prima inizializzazione.
Redis usa un volume persistente con AOF abilitato. Lo script di inizializzazione non crea
tabelle applicative o Business Memory.

Per verificare pgvector:

```powershell
docker compose exec postgres psql -U ramai -d ramai_dev -tAc `
  "SELECT extversion FROM pg_extension WHERE extname = 'vector';"
```

Gli script in `docker-entrypoint-initdb.d` vengono eseguiti da PostgreSQL soltanto quando
il volume dati è vuoto. Un volume creato prima dell'aggiunta dello script deve essere
inizializzato manualmente o ricreato consapevolmente.

## Frontend

```powershell
cd .\src\frontend\ramai-web
npm ci
npm run dev
```

Quality gate frontend:

```powershell
npm run lint
npm run type-check
npm run build
```

La pagina iniziale è un placeholder statico realizzato con Next.js App Router. Non contiene
autenticazione, Business Memory, AI, connector o billing.

## Confini architetturali

- `Ramai.Api` è il composition root.
- `Ramai.Domain`, `Ramai.Application` e `Ramai.Infrastructure` costituiscono la baseline condivisa.
- i progetti sotto `Modules` sono placeholder dei moduli previsti;
- nessun modulo accede direttamente alle tabelle interne di un altro modulo;
- i placeholder non implementano funzionalità di Sprint 1.

## Stato Sprint 0

La baseline copre S0-001 fino a S0-012. S0-011 è verde anche sui runner GitHub:
[run verificata del commit d7cb535](https://github.com/maffeinet/ramai/actions/runs/37431050731).
S0-012 documenta il bootstrap locale; Sprint 1 non avviato.

## Test backend

Eseguire dalla root; i test ordinari non richiedono Docker:

```powershell
dotnet build .\src\backend\Ramai.sln
dotnet test .\src\backend\Ramai.sln --no-build
dotnet format .\src\backend\Ramai.sln --verify-no-changes --no-restore
.\scripts\Test-DevEnvironment.ps1
```

La suite comprende unit test del correlation ID, test HTTP in-memory di `/health` e
architecture test dei riferimenti dichiarati e compilati. Un test infrastrutturale
viene segnalato come skipped nella suite ordinaria.

Per eseguire anche il test reale, avviare i servizi con `docker compose up -d --wait`,
esportare nella shell le variabili `RAMAI_DB_*` e `RAMAI_REDIS_*` corrispondenti ai
container, inclusa la password locale, quindi:

```powershell
$env:RAMAI_RUN_INFRASTRUCTURE_TESTS = '1'
. .\scripts\Import-DevEnvironment.ps1
dotnet test .\src\backend\Ramai.sln --no-build
Remove-Item Env:\RAMAI_RUN_INFRASTRUCTURE_TESTS
```

Il test reale esegue PostgreSQL `SELECT 1`, Redis `PING` e verifica `pg_extension`.
Non crea tabelle e non modifica dati applicativi.

## CI baseline — S0-011

Il workflow `.github/workflows/ci.yml` viene eseguito su push e pull request con
runner ospitati da GitHub (`ubuntu-24.04`). Due job indipendenti verificano:

- backend .NET 10: restore, build Release, format e tutti i test, inclusi quelli
  infrastrutturali contro PostgreSQL/pgvector e Redis avviati tramite Compose;
- frontend: Node da `.nvmrc`, `npm ci`, lint, type-check e production build.

Un gate fallito rende fallito il job. La password del database CI è generata per run
e mascherata; non sono necessari secret repository. I volumi del progetto Compose
`ramai-ci` sono effimeri e rimossi al termine, senza toccare i volumi locali `ramai`.
Permessi limitati alla lettura dei contenuti, azioni fissate a SHA immutabili.
La CI non configura branch protection, non pubblica e non esegue deploy.

La prima run GitHub è completata con successo per il commit `d7cb535`:
[RAMAI CI](https://github.com/maffeinet/ramai/actions/runs/37431050731).
Questa run verifica S0-011, non le modifiche S0-012 ancora locali.

## Troubleshooting e limiti Foundation

- Docker non raggiungibile: avviare Docker Desktop e attendere che l'engine Linux sia pronto.
- Script PowerShell bloccato: verificare la policy aziendale; non modificarla globalmente.
  Il loader modifica solo l'ambiente del processo: ripeterlo in ogni shell backend/test.
- Porta occupata: chiudere il processo concorrente oppure modificare `.env`; importare
  nuovamente il file e riavviare i servizi. Per Next usare `npm run dev -- --port 3001`
  se necessario; aggiornare gli URL di verifica in modo coerente.
- `/health` HTTP 503: controllare Docker health, password e variabili importate.
  Cambiare `POSTGRES_PASSWORD` non cambia la password dentro un volume già inizializzato:
  non eliminare volumi per tentativi; riallineare le credenziali consapevolmente.
- `vector` assente su un volume precedente: eseguire consapevolmente
  `CREATE EXTENSION IF NOT EXISTS vector;` nel database di sviluppo; non crea tabelle.
- Non pubblicare configurazioni complete, `.env` o log contenenti secret.
- Restano i warning/vulnerabilità ESLint documentati nel report. Non eseguire
  `npm audit fix --force` senza valutare le modifiche incompatibili.
- Nessuna autenticazione, tenancy operativa, AI, Business Memory, connector o billing;
  i moduli sono placeholder, non funzionalità già disponibili.
