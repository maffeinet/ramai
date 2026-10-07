# Sprint 0 — Progress report

## Scope

Questa modifica realizza esclusivamente:

- S0-001: baseline del repository;
- S0-002: solution .NET 10, API, livelli condivisi e moduli placeholder;
- S0-003: baseline Next.js con App Router, React, TypeScript e npm;
- S0-004: PostgreSQL 17 predisposto per pgvector;
- S0-005: Redis per cache e stato a breve durata;
- S0-006: Docker Compose locale con rete, volumi e health check;
- S0-007: configurazione locale tramite environment variable e protezione dei secret.
- S0-008: health check applicativo e correlation ID;
- S0-009: logging strutturato baseline.

S0-010 aggiunge la baseline di test. S0-011 aggiunge la CI, ora verificata anche su
GitHub. S0-012 completa il developer bootstrap. I risultati cronologici sono riportati
nelle sezioni finali; Sprint 1 non è incluso.

## Housekeeping

- il report progressivo è stato rinominato in `docs/sprint0-progress.md` senza perdere
  lo storico dei task precedenti;
- `.gitattributes` definisce CRLF per file Windows nativi (`.sln`, `.cmd`, `.bat`, `.ps1`),
  LF per sorgenti e configurazioni cross-platform e nessuna normalizzazione per i binari.

## Technical debt non bloccante

- npm segnala vulnerabilità alte transitive nel solo tooling ESLint sotto
  `eslint-config-next`; le dipendenze di produzione non risultano vulnerabili;
- ESLint 9 è segnalato come non più supportato, mentre ESLint 10 produce peer override
  con i plugin correnti di Next.js. La combinazione compatibile resta temporaneamente
  ESLint 9.39.5 con eslint-config-next 16.3.8.

## Decisioni applicate

- monolite modulare, senza servizi distribuibili separatamente;
- target framework centralizzato su `net10.0`;
- dipendenze condivise unidirezionali: Application → Domain e Infrastructure → Application;
- API usata esclusivamente come composition root;
- moduli funzionali presenti come assembly placeholder senza riferimenti reciproci;
- nessun provider AI, connector, billing, memory, insight o agente implementato;
- nessun secret o valore di produzione nei file versionati.

## Configurazione

`.env.example` contiene esclusivamente valori e placeholder per lo sviluppo locale. `.env`
e i comuni formati di chiavi e certificati sono esclusi tramite `.gitignore`.

Docker Compose richiede esplicitamente `RAMAI_DB_PASSWORD`: non contiene una password
di fallback incorporata.

Le opzioni PostgreSQL e Redis e il verificatore di connettività sono registrati dal
composition root e usati dal `/health` applicativo introdotto con S0-008.

## PostgreSQL, Redis e Docker Compose

Versioni configurate:

- PostgreSQL 17 tramite `pgvector/pgvector:0.8.6-pg17`;
- estensione pgvector 0.8.6, abilitata con `CREATE EXTENSION IF NOT EXISTS vector`;
- Redis 7.2.16 tramite `redis:7.2.16-alpine3.21`;
- Npgsql 10.0.3;
- StackExchange.Redis 3.3.1.

Implementazione:

- `docker-compose.yml` resta nella root e definisce rete bridge, volumi persistenti e
  health check per PostgreSQL e Redis;
- `infra/postgres/init/001-enable-vector.sql` abilita esclusivamente pgvector e non crea
  tabelle;
- la password PostgreSQL deve essere fornita tramite `RAMAI_DB_PASSWORD` e non ha fallback;
- Redis usa AOF sul volume persistente;
- `Ramai.Infrastructure` espone `IInfrastructureConnectivityVerifier`, con verifiche
  esplicite `SELECT 1` e Redis `PING`, senza endpoint HTTP o startup gate.

File creati:

- `.gitattributes`;
- `infra/postgres/init/001-enable-vector.sql`;
- `Ramai.Infrastructure/Configuration/PostgreSqlOptions.cs`;
- `Ramai.Infrastructure/Configuration/RedisOptions.cs`;
- `Ramai.Infrastructure/Connectivity/ConnectivityCheckResult.cs`;
- `Ramai.Infrastructure/Connectivity/IInfrastructureConnectivityVerifier.cs`;
- `Ramai.Infrastructure/Connectivity/InfrastructureConnectivityVerifier.cs`;
- `Ramai.Infrastructure/DependencyInjection.cs`.

File modificati:

- `docker-compose.yml`;
- `Ramai.Infrastructure.csproj`;
- `Ramai.Api/Program.cs`;
- `README.md`;
- `docs/sprint0-progress.md` (rinominato dal report precedente).

## Frontend baseline

Il frontend si trova in `src/frontend/ramai-web` e usa App Router con una singola pagina
statica Foundation. Non effettua chiamate API e non contiene autenticazione, Business
Memory, AI, connector o billing.

Versioni effettivamente installate:

- Node.js 24.19.0;
- npm 11.17.0;
- Next.js 16.3.8;
- React e React DOM 19.3.0;
- TypeScript 6.0.3;
- ESLint 9.39.5 ed eslint-config-next 16.3.8;
- `@types/node` 26.6.4, `@types/react` e `@types/react-dom` 19.3.0.

File creati per S0-003:

- `.npmrc`, `.nvmrc`, `package.json` e `package-lock.json`;
- `next.config.ts`, `next-env.d.ts`, `tsconfig.json` ed `eslint.config.mjs`;
- `app/layout.tsx`, `app/page.tsx` e `app/globals.css`.

File modificati per S0-003:

- `.gitignore`, per escludere anche `*.tsbuildinfo`;
- `README.md`, con prerequisiti e comandi frontend;
- questo report Sprint 0.

## Limiti intenzionali

- nessun health check;
- nessuna implementazione di autenticazione o funzionalità di Sprint 1;
- nessuna pipeline CI, prevista da S0-011.

## Verifica locale del 3 ottobre 2026

- SDK utilizzato: .NET SDK 10.0.401;
- `dotnet build .\src\backend\Ramai.sln`: completato con 0 avvisi e 0 errori;
- `dotnet test .\src\backend\Ramai.sln --no-build`: nessun test project disponibile,
  come previsto prima di S0-010;
- gli artefatti `bin` e `obj` prodotti dalla build sono esclusi da Git;
- controllo dei file di configurazione: nessuna credenziale reale introdotta.

Il quality gate di compilazione previsto da S0-002 è soddisfatto.

## Verifica S0-003 del 3 ottobre 2026

- `npm install`: completato; 346 pacchetti verificati nella risoluzione finale;
- `npm run lint`: completato con esito positivo;
- `npm run type-check`: completato con esito positivo;
- `npm run build`: completato con esito positivo; route `/` prerenderizzata staticamente;
- `npm audit --omit=dev`: 0 vulnerabilità nelle dipendenze di produzione.

Warning residui:

- npm segnala 5 vulnerabilità alte transitive, tutte nel tooling di sviluppo sotto
  `eslint-config-next` (`fast-glob`, `micromatch` e `braces`); non esiste una versione
  corretta di `braces` nel registry al momento della verifica e il fix automatico proposto
  richiede un downgrade breaking a eslint-config-next 14.2.35;
- ESLint 9.39.5 è segnalato da npm come non più supportato, ma ESLint 10 genera peer
  override con i plugin correnti di Next.js; la versione 9 è mantenuta temporaneamente
  perché è la combinazione che rende verde il lint senza forzature;
- npm segnala lo script post-install di `unrs-resolver` come non ancora approvato nella
  policy `allow-scripts`; l'installazione e tutti i quality gate risultano comunque verdi.

## Verifica S0-004, S0-005 e S0-006 del 5 ottobre 2026

- `dotnet build .\src\backend\Ramai.sln`: completato con 0 avvisi e 0 errori;
- `dotnet test .\src\backend\Ramai.sln --no-build`: exit code 0, nessun test project
  disponibile prima di S0-010;
- restore NuGet completato per Npgsql 10.0.3 e StackExchange.Redis 3.3.1;
- controllo statico: lo script PostgreSQL contiene soltanto
  `CREATE EXTENSION IF NOT EXISTS vector` e non crea tabelle;
- Docker Desktop 4.93.0, Docker Engine/CLI 29.8.1 e Docker Compose 5.5.1;
- `docker compose config`: completato con esito positivo;
- `docker compose up -d`: completato con creazione di rete e volumi persistenti;
- PostgreSQL: container `healthy`, `pg_isready` conferma connessioni accettate;
- Redis: container `healthy`, `redis-cli ping` restituisce `PONG`;
- pgvector: query a `pg_extension` restituisce `vector:0.8.6`.

S0-004, S0-005 e S0-006 sono completati e i relativi quality gate risultano verdi.

Problemi residui:

- gli script `docker-entrypoint-initdb.d` operano solo su un volume PostgreSQL vuoto;
  su un volume preesistente l'estensione deve essere abilitata manualmente o il volume
  deve essere ricreato con una decisione esplicita;
## Health e logging applicativi

Implementazione S0-008 e S0-009:

- `/health` verifica separatamente applicazione, PostgreSQL e Redis;
- i check PostgreSQL e Redis riusano `IInfrastructureConnectivityVerifier` e restano
  distinti dagli health check Docker;
- `CorrelationIdMiddleware` accetta `X-Correlation-ID` esterni validi, genera un GUID per
  valori assenti/non validi, aggiorna `HttpContext.TraceIdentifier` e restituisce sempre
  l'header nella response;
- il JSON console formatter standard ASP.NET Core include scope e timestamp UTC;
- ogni log applicativo di richiesta include correlation ID ed environment nello scope,
  oltre a livello e categoria prodotti dal formatter;
- il log applicativo registra solo status code e durata, senza body, query string,
  credenziali, token, secret o connection string.

File creati:

- `Ramai.Api/Middleware/CorrelationIdMiddleware.cs`;
- `Ramai.Api/Health/PostgreSqlHealthCheck.cs`;
- `Ramai.Api/Health/RedisHealthCheck.cs`;
- `Ramai.Api/Health/HealthResponseWriter.cs`.

File modificati:

- `Ramai.Api/Program.cs`;
- `Ramai.Api/appsettings.json`;
- `Ramai.Api/appsettings.Development.json`;
- `README.md`;
- `docs/sprint0-progress.md`.

Dipendenze aggiunte: nessuna. L'implementazione usa esclusivamente ASP.NET Core,
`Microsoft.Extensions.Diagnostics.HealthChecks` e `Microsoft.Extensions.Logging` inclusi
nel framework condiviso.

## Verifica S0-008 e S0-009 del 5 ottobre 2026

- `dotnet build .\src\backend\Ramai.sln`: completato con 0 avvisi e 0 errori;
- `dotnet test .\src\backend\Ramai.sln --no-build`: exit code 0, nessun test project
  disponibile prima di S0-010;
- API avviata su `http://127.0.0.1:8080` in ambiente Development;
- `/health`: HTTP 200, stato complessivo `Healthy`;
- check `application`: `Healthy`;
- check `postgresql`: `Healthy`;
- check `redis`: `Healthy`;
- health Docker PostgreSQL e Redis: entrambi `healthy`;
- correlation ID fornito `ramai-final-health-check`: riutilizzato nella response e nel log;
- correlation ID non valido: sostituito con un GUID generato;
- log verificato in JSON con `Timestamp`, `LogLevel`, `Category`, `Environment` e
  `CorrelationId`.

Output sintetico verificato:

```json
{
  "status": "Healthy",
  "correlationId": "ramai-final-health-check",
  "checks": {
    "application": { "status": "Healthy" },
    "postgresql": { "status": "Healthy" },
    "redis": { "status": "Healthy" }
  }
}
```

Warning e technical debt:

- la copertura automatica del middleware e della health response è stata aggiunta con
  S0-010; le verifiche riportate sopra conservano gli esiti storici di S0-008/009;
- la policy di validazione del correlation ID è intenzionalmente conservativa e potrà
  essere centralizzata come contratto condiviso se emergeranno altri host;
- i log di startup ASP.NET Core non hanno correlation ID perché non appartengono a una
  richiesta HTTP;
- il JSON health espone soltanto stato, descrizioni non sensibili, durata ed eventuale
  tipo sintetico di errore; non espone dettagli di eccezione o configurazione.

## S0-010 — Testing baseline, 6 ottobre 2026

Task completato: unit, integration skeleton e architecture tests sono inclusi nella
solution .NET 10 e vengono scoperti da `dotnet test`. S0-011 e S0-012 non avviati.

File creati:

- `tests/Directory.Build.props`: importa le policy backend e configura il test runner;
- `tests/Ramai.UnitTests/Ramai.UnitTests.csproj`;
- `tests/Ramai.UnitTests/CorrelationIdTests.cs`;
- `tests/Ramai.IntegrationTests/Ramai.IntegrationTests.csproj`;
- `tests/Ramai.IntegrationTests/HealthEndpointTests.cs`;
- `tests/Ramai.IntegrationTests/InfrastructureTests.cs`;
- `tests/Ramai.ArchitectureTests/Ramai.ArchitectureTests.csproj`;
- `tests/Ramai.ArchitectureTests/DependencyBoundaryTests.cs`.

File modificati: `src/backend/Ramai.sln`, `README.md` e questo report.
Nessuna modifica al codice di produzione.

Dipendenze esclusivamente di test:

- Microsoft.NET.Test.Sdk 17.14.1;
- xunit 2.9.3;
- xunit.runner.visualstudio 3.1.3 (PrivateAssets=all);
- Microsoft.AspNetCore.Mvc.Testing 10.0.12 per il server HTTP in-memory.

Toolchain: SDK 10.0.401, runtime test .NET 10.0.12.

Copertura:

- 7 unit test: ID valido riutilizzato in contesto/header/scope log; ID assente, invalido,
  non ASCII e troppo lungo sostituito; environment nello scope; query e token sentinella
  non inclusi nel messaggio applicativo;
- 5 test HTTP: combinazioni PostgreSQL/Redis disponibili o indisponibili, aggregazione
  Healthy/Unhealthy, HTTP 200/503, JSON/header coerenti e correlation ID anche su 404;
- 1 test infrastrutturale opt-in: PostgreSQL e Redis reali e presenza di pgvector;
- 3 architecture test: riferimenti dei livelli condivisi, assenza di dipendenze
  infrastrutturali nel Domain compilato, isolamento dei sette placeholder modulari.

Quality gate:

- `dotnet build src/backend/Ramai.sln`: PASS, 0 warning e 0 errori;
- suite ordinaria: 15 superati, 0 falliti, 1 skipped infrastrutturale esplicito;
- suite con `RAMAI_RUN_INFRASTRUCTURE_TESTS=1`: 16 superati, 0 falliti, 0 skipped;
- `dotnet format src/backend/Ramai.sln --verify-no-changes --no-restore`: PASS.

La prima prova reale è fallita perché Docker Desktop era spento. Dopo l'avvio di Docker
e `compose up -d --wait`, entrambi i container healthy e la suite completa sono verdi.
Nessun volume eliminato, nessuna credenziale scritta nel repository o stampata.

Limiti e technical debt:

- i test cross-tenant richiesti da ADR-003 restano da introdurre insieme a TenantContext,
  autenticazione ed entità tenant-scoped: queste funzionalità non esistono nella Foundation;
- i test architetturali verificano dipendenze di progetto/assembly; non dimostrano ancora
  isolamento tra tabelle, perché non esistono tabelle applicative;
- la verifica del logging testa livello, messaggio e scope del middleware; non è ancora
  un test end-to-end del formatter JSON console né di tutte le categorie framework;
- la verifica reale richiede opt-in, servizi operativi e variabili d'ambiente coerenti;
  un test skipped nella suite ordinaria non equivale a una verifica delle dipendenze reali;
- rimane il technical debt ESLint già registrato. Nessun nuovo warning di compilazione.

## S0-011 — CI baseline (2026-10-06)

Stato: implementazione completata e gate locali verdi; esecuzione effettiva sui runner
GitHub non ancora verificata. Nessun commit o push eseguito. S0-012 e Sprint 1 non avviati.

File creati/modificati esclusivamente per questo task:

- `.github/workflows/ci.yml`: nuovo workflow;
- `README.md`: stato corrente e funzionamento della CI;
- `docs/sprint0-progress.md`: questo aggiornamento, preservando lo storico.

Pipeline su `push` e `pull_request`, con due job indipendenti su runner GitHub-hosted
`ubuntu-24.04`, timeout di 20 minuti e cancellazione delle run obsolete sullo stesso ref:

- backend: restore, build Release, format check e suite completa con
  `RAMAI_RUN_INFRASTRUCTURE_TESTS=1`;
- PostgreSQL/pgvector e Redis avviati dal Compose root con progetto isolato `ramai-ci`,
  attesa degli health check Docker e rimozione dei soli servizi/volumi CI al termine;
- frontend: `npm ci`, lint, type-check e production build;
- ogni comando fallito interrompe il job: nessun `continue-on-error`;
- permessi `contents: read`, checkout senza persistenza delle credenziali;
- password database casuale per run, mascherata prima dell'esportazione in `GITHUB_ENV`;
  nessun secret hardcoded e validazione Compose silenziosa per non mostrare credenziali.

Versioni configurate: SDK .NET 10.0.401; Node 24.19.0 da `.nvmrc`;
immagini `pgvector/pgvector:0.8.6-pg17` e `redis:7.2.16-alpine3.21` già presenti nel Compose.
Checkout v7, setup-dotnet v6 e setup-node v7 fissati ai rispettivi SHA ufficiali verificati.
Nessuna nuova dipendenza applicativa/NuGet o modifica alle funzionalità.

Verifiche **locali Windows**, non equivalenti a una run GitHub/Linux:

- restore e build Release: PASS, 0 warning e 0 errori;
- `dotnet format --verify-no-changes --no-restore`: PASS;
- Compose config e avvio con attesa health: PASS; PostgreSQL e Redis healthy;
- test Release con infrastruttura reale: 16 superati, 0 falliti, 0 skipped;
  inclusi PostgreSQL SELECT 1, Redis PING e presenza dell'estensione vector;
- `npm ci`: PASS, npm locale 11.17.0;
- lint, type-check e production build Next.js 16.3.8: PASS;
- actionlint 1.7.12: PASS senza diagnostiche, pacchetto ufficiale verificato via SHA256;
- controllo diff whitespace: PASS.

Warning/limiti/technical debt:

- persistono 5 vulnerabilità high nelle dipendenze frontend di sviluppo e il warning
  ESLint 9 non più supportato; migrazione ESLint 10/peer dependencies Next.js già
  registrata, non affrontata in questo task;
- npm segnala lo script postinstall di `unrs-resolver` non ancora approvato;
  i gate locali sono comunque verdi, nessuna approvazione automatica aggiunta;
- la prima run ospitata dovrà confermare disponibilità toolchain/immagini, comportamento
  su Linux e gate verdi: nessun risultato GitHub viene dichiarato come già ottenuto;
- il workflow fallisce sui gate, ma non configura branch protection/required checks:
  queste impostazioni repository richiedono una scelta e autorizzazione separate;
- limiti dei test e debt precedenti restano invariati. Nessun secret aggiunto ai file.
- Git segnala conversioni LF/CRLF su alcuni file della working copy preesistente;
  nessun errore whitespace rilevato, nessuna normalizzazione estesa in questo task.

### Conferma GitHub S0-011 — 2026-10-06

Il precedente stato "in attesa" è superato: commit/push autorizzati e completati su
`main`, commit `d7cb53548cf10185f7f17766ce23755a1916832d`.
[RAMAI CI, run 37431050731](https://github.com/maffeinet/ramai/actions/runs/37431050731)
è completed/success, verificato tramite GitHub CLI, non soltanto dal riscontro utente.
Entrambi i job backend e frontend e tutti i rispettivi gate sono success.
La run è iniziata il 6 ottobre 2026 alle 09:39:40 Europe/Rome.
Questo risultato riguarda S0-011 e il commit indicato, non le successive modifiche locali.

## S0-012 — Developer bootstrap (2026-10-06)

Implementato esclusivamente il bootstrap Foundation. Nessuna modifica applicativa,
nessuna nuova dipendenza, nessun avvio di Sprint 1. Nuovi commit/push non eseguiti.

File modificati/creati:

- `README.md`: prerequisiti/versioni, nuovo clone, configurazione password, tre shell
  per API/frontend/verifica, quality gate, arresto conservativo e troubleshooting;
- `scripts/Import-DevEnvironment.ps1`: importa solo chiavi documentate nell'ambiente
  del processo; nessuna valutazione del contenuto o stampa di credenziali;
- `scripts/Test-DevEnvironment.ps1`: 7 casi ripetibili senza dipendenze di test extra;
- `docs/sprint0-progress.md`: conferma GitHub e questa registrazione.

Ambiguità risolta: `.env` viene letto da Compose ma non automaticamente da ASP.NET Core.
Il README precedente avviava l'API senza importarlo: ora il passaggio è esplicito.
Gli script dev-up/dev-down sono opzionali nel backlog e non sono stati aggiunti:
avvio/arresto dei processi restano espliciti, senza supervisori o processi nascosti.

Verifica locale effettuata in un clone temporaneo del commit pubblicato, con i soli
file bootstrap aggiornati copiati sopra: nessun bin/obj/node_modules preesistente.
PostgreSQL e Redis usavano volumi nuovi e un progetto Compose isolato; porte di prova
15432/16379/18080/13000 per non interferire con i servizi locali. Password casuale
generata soltanto nel `.env` ignorato del clone temporaneo, mai stampata.

Quality gate e risultati:

- loader: 7 casi PASS (valido/apici/commenti, placeholder, vuoto, duplicato,
  chiave non consentita, riga invalida, file assente); nessun output sensibile,
  file invalidi respinti prima di modificare l'ambiente;
- parser PowerShell: PASS per entrambi gli script;
- Compose config silenzioso, inizializzazione da volumi vuoti e health Docker: PASS;
- restore/build backend: PASS, 0 warning e 0 errori;
- 16 test backend con infrastruttura reale: PASS, 0 falliti e 0 skipped;
  PostgreSQL/Redis raggiungibili ed estensione vector verificata;
- format backend: PASS;
- API avviata tramite configurazione importata: `/health` HTTP 200, Healthy per
  application/postgresql/redis, correlation ID `ramai-bootstrap-check` nella response
  e nello scope del log JSON;
- `npm ci`, lint, type-check e production build: PASS;
- frontend development avviato: HTTP 200, pagina RAMAI presente;
- arresto API/frontend e `docker compose down` del solo progetto di prova: PASS,
  volumi persistenti conservati, infrastruttura originale non modificata;
- whitespace diff: PASS; nessun secret aggiunto ai file versionabili.

Toolchain invariata: SDK 10.0.401, Node 24.19.0, npm 11.17.0, Next.js 16.3.8;
immagini pgvector 0.8.6-pg17 e Redis 7.2.16-alpine3.21.

Limiti/technical debt:

- persistono i 5 high di sviluppo ESLint, deprecazione ESLint 9 e warning postinstall
  unrs-resolver: nessun aggiornamento forzato o approvazione automatica;
- test eseguiti su Windows con toolchain e cache pacchetti già installate: non si
  dichiara una prova su un sistema operativo appena installato o senza cache;
- loader PowerShell: formato locale limitato, non parser dotenv universale;
  usare password esadecimale senza interpolazioni per coerenza con Compose;
- i 7 test PowerShell sono locali/documentati, non ancora inclusi nella CI Linux;
- la run GitHub verde è quella S0-011: S0-012 non ancora pubblicato/eseguito su GitHub;
- clone temporaneo e volumi di prova conservati per non eliminare dati autonomamente;
- limiti Foundation e debito precedenti restano validi. Nessuna feature fuori Sprint 0.
