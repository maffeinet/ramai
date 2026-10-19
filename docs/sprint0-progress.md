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

## Riallineamento finale e audit — 2026-10-16

Questa sezione aggiorna gli stati storici precedenti senza cancellarli: S0-012 non
è più soltanto locale. Commit/push autorizzati erano stati completati su `main` con
`6d5f697b75eba409198f6cdfb402d152d7e55997`.
[Run GitHub 37586023391](https://github.com/maffeinet/ramai/actions/runs/37586023391):
completed/success, job backend e frontend success, riconfermato tramite GitHub CLI
il 16 ottobre. Nessuna nuova run è stata avviata in questa sessione.

### Stato rispetto a backlog e Definition of Done

S0-001–012 risultano implementati e pubblicati. Le evidenze precedenti coprono
struttura e stack, configurazione environment, Docker health/connettività reale,
pgvector, API /health, correlation ID/logging, frontend, test e bootstrap pulito.
La run del commit S0-012 verifica build/format e 16 test backend senza skipped,
installazione/lint/type-check/build frontend. I 7 test loader restano verifiche
locali, non comprese nel workflow attuale.

Il disallineamento documentale sull'avvenuta pubblicazione è corretto nel README e
in questo aggiornamento. Stato di chiusura: Foundation tecnicamente verificata,
in attesa di conferma umana della chiusura M0 e dell'accettazione del debito noto.
Non viene dichiarato implicitamente accettato il rischio né autorizzato Sprint 1.

### Audit npm attuale, senza aggiornamenti

Comandi eseguiti sul lockfile con `--ignore-scripts` e cache temporanea esterna al
repository: `npm audit --package-lock-only --json` e variante `--omit=dev`.

- totale: 5 high, 0 critical; produzione: 0 vulnerabilità segnalate;
- catena dev: eslint-config-next → @next/eslint-plugin-next → fast-glob → micromatch
  → braces; cinque pacchetti segnalati non equivalgono a cinque falle indipendenti;
- [advisory braces](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm): esaurimento
  dello stack con pattern profondamente annidati, versioni <=3.0.3; nessuna patch
  indicata. Il registry restituisce ancora braces 3.0.3 come versione latest;
- npm propone eslint-config-next 14.2.35 come modifica major/downgrade incompatibile:
  non applicato. Zero risultati production non sono garanzia assoluta di sicurezza;
- ESLint 9.39.5 confermato deprecato dal registry e fuori supporto dalla
  [policy ufficiale ESLint](https://eslint.org/version-support/);
- eslint-config-next 16.4.2 disponibile: metadati letti, non installata; il plugin
  corrispondente dipende ancora da fast-glob 3.3.1. Non è una correzione dimostrata;
- il peer della configurazione è eslint >=9.0.0: da solo non dimostra compatibilità
  dell'intero albero con ESLint 10. Serve una prova isolata prima della migrazione;
- unrs-resolver 1.12.2 dichiara postinstall `node postinstall.js`: warning già noto,
  script non approvato né eseguito da questo audit. Analisi del codice ancora da fare;
- npm segnala disponibilità della major 12.2.0 rispetto alla locale 11.17.0: è un
  avviso di aggiornamento, non un gate fallito; nessun upgrade globale effettuato.

### File e verifiche di questa sessione

README e questo report riallineati; `docs/memory.md` e `docs/handoff.md` aggiornati
come contesto di ripresa (creati nella sessione documentale precedente, ancora locali).
Nessun manifest, lockfile, workflow, script o sorgente applicativo modificato.
Controlli documentali: whitespace e link locali verificati. Build/test/runtime non
rieseguiti per una modifica solo documentale; evidenze storiche distinte dall'audit
registry odierno. Nessun nuovo commit/push; Sprint 1 non avviato.

Prossima decisione: accettare esplicitamente il debt per chiudere M0 oppure richiedere
una verifica/correzione isolata delle dipendenze prima della chiusura. Non usare
`npm audit fix --force` o approvazioni automatiche degli script.

## Tentativo di riduzione del debt frontend — 2026-10-16

Richiesta utente: verificare e risolvere il debt ove possibile, senza nuove feature.
Risultato: **riduzione parziale**, non risoluzione delle cinque segnalazioni high.

### Prove isolate e decisioni

- Creata una copia temporanea del solo frontend, senza node_modules iniziale e senza
  modificare le versioni del repository. Lockfile della copia identico all'originale
  dopo la prova (SHA256 confrontati).
- Provato `npm install --save-dev --save-exact eslint@10.12.0 --strict-peer-deps
  --ignore-scripts --package-lock-only` nella sola copia: FAIL atteso, ERESOLVE.
  eslint-plugin-import 2.32.0 ammette ESLint fino a v9. Anche i metadati latest di
  eslint-plugin-react 7.37.5 e eslint-plugin-jsx-a11y 6.10.2 non dichiarano v10.
  eslint-plugin-react-hooks e typescript-eslint dichiarano v10, ma non bastano a
  rendere compatibile l'intero albero. Nessun force/legacy-peer-deps usato.
- braces latest resta 3.0.3, affetto dall'advisory già citato e senza patch indicata.
  Nessun downgrade, fork, override di versioni non corrette o rimozione di regole lint.
  Non è stata trovata una soluzione compatibile verificata per eliminare i 5 high.
- Letti postinstall.js di unrs-resolver e il relativo helper napi-postinstall:
  preparano il binding nativo e prevedono fallback con installazione npm/download.
  Scelta conservativa: negare questo script, non approvarlo automaticamente.

### Correzione applicata e verifica effettiva

`src/frontend/ramai-web/package.json`: aggiunto soltanto
`"allowScripts": { "unrs-resolver": false }`. Policy interpretata e verificata con
npm 11.17.0, coerente con il codice della CLI installata. Non equivale a dichiarare
sicuro lo script o a correggere braces. Nessuna dipendenza o lockfile aggiornata.

Prima nella copia pulita, poi nel repository locale:

- `npm ci --strict-peer-deps`: PASS, nessun peer override e nessun warning su script
  unrs-resolver non ancora revisionato; resta la deprecazione ESLint;
- `npm approve-scripts --allow-scripts-pending --json`: `{"allowScripts":[]}`;
  questa modalità elenca soltanto, non approva né esegue script;
- lint, type-check e production build: PASS;
- `npm ls eslint --all`: PASS con ESLint 9.39.5, nessun peer invalid;
- audit lockfile: ancora 5 high dev, 0 critical; audit production: 0 segnalazioni.
  Il gate audit completo resta fallito (exit 1); non viene mascherato come risolto.

File toccati in questo blocco: package.json, README, questo report, memory e handoff.
Le modifiche documentali precedenti sono preservate. Nessuna modifica a package-lock,
sorgenti, backend, regole lint o workflow. Gate backend non rieseguiti perché invariati.
Controlli finali whitespace/link e coerenza JSON eseguiti.

Limiti: prova su Windows con binding optional installati da npm ci. Linux/GitHub per
questa nuova policy non ancora verificati; la run verde pubblicata riguarda ancora
il commit 6d5f697, non queste modifiche locali. Copia temporanea conservata; nessun
commit/push o avvio Sprint 1. Il debt ESLint/braces e la chiusura formale M0 restano
aperti, con decisione umana necessaria sulle alternative o accettazione temporanea.

### Pubblicazione e conferma Linux — 2026-10-16

Il precedente limite "policy non ancora verificata su GitHub" è superato.
Commit `258e6035f7cf996e9f5d750e284925feddc229f7` pubblicato su main con i cinque
file concordati, inclusi memory/handoff; Gitleaks sul contenuto staged: nessun leak.
[RAMAI CI 38887367228](https://github.com/maffeinet/ramai/actions/runs/38887367228)
completed/success, entrambi i job verdi. Log controllati: nessun warning allow-scripts,
installazione/lint/type-check/build frontend superati su Ubuntu; backend 0 warning,
0 errori e 16 test superati, 0 falliti, 0 skipped.

Restano nei log la deprecazione ESLint 9 e le 5 high dev: la correzione è parziale,
non accettazione automatica del rischio né chiusura formale M0. Sprint 1 non avviato.
Questo aggiornamento documenta la run del commit di correzione, non una nuova
esecuzione locale; eventuali commit solo documentali successivi non cambiano il codice.

## Pianificazione controllata successiva — 2026-10-16

Predisposti [SEC-EX-001](security-exception.md) e il
[backlog Sprint 1](../tasks/SPRINT1_BACKLOG.md), entrambi **proposte non approvate**.
Non vengono registrate una firma umana del rischio o una chiusura M0 non espresse.
Nessuna implementazione Sprint 1 avviata.

La lettura PDF (skill PDF, estrazione testuale) delle specifiche 04/08/11/12 ha guidato
il piano: Identity + Tenant + DB baseline, sessioni sicure, membership, ruoli,
test cross-tenant e gate di sicurezza/release. Non è stata verificata graficamente
la fedeltà dei layout PDF; la pianificazione usa contenuti testuali e sezioni indicate.
I PDF sono stati letti senza modificarli; il parser ha recuperato gli indici xref
segnalati come non corretti. Nessuna dipendenza installata per questa lettura.

Ambiguità documentate nel backlog: varianti layout/numerazione Foundation nei PDF,
auth locale versus OIDC/OAuth/IdP da decidere con ADR, moduli futuri non da aggiungere
ora e target backup differenti da risolvere prima del rilascio. Il progetto mantiene
AGENTS.md, ADR e confini attuali; nessuna scelta infrastrutturale implicita.
Rettificato il contesto memory/handoff: SaaS europeo e cloud EU sono già target delle
specifiche, mentre provider e deployment concreto non sono ancora definiti.

Eccezione proposta: solo tooling noto per sviluppo interno, approvatore da assegnare,
riesame/scadenza 30 ottobre 2026 o prima se fine Sprint 1/clienti reali; nessuna
estensione automatica a beta o commercializzazione. Nuovi rischi non sono coperti.

File del blocco: nuovi docs/security-exception.md e tasks/SPRINT1_BACKLOG.md;
aggiornati questo report, memory e handoff. Verifiche proporzionate: whitespace,
link locali e coerenza stati proposta/non implementato. Nessuna build/test/audit
registry rieseguita per queste sole modifiche documentali; nessun commit o push.

## Chiusura M0 con eccezione accettata — 2026-10-16

L'utente ha risposto esplicitamente "si procediamo accettando il debito fino al
riesame" alla richiesta di chiudere Sprint 0 con la deroga per il solo sviluppo
interno. [SEC-EX-001](security-exception.md) è ora accettata temporaneamente fino al
30 ottobre 2026, o prima se fine Sprint 1 o prima di clienti/dati reali. Le condizioni
di revoca/riesame anticipato restano valide. Nessuna approvazione alla commercializzazione.

**Sprint 0 / M0: chiuso con eccezione documentata sul tooling frontend.**
S0-001–012 restano verificati dalle evidenze precedenti: baseline repository/stack,
PostgreSQL/Redis/pgvector, config/secrets, health/logging, test, CI e bootstrap.
Ultima run pubblicata riconfermata oggi tramite GitHub CLI:
[38887663889](https://github.com/maffeinet/ramai/actions/runs/38887663889), commit
`4c441b32eac4aaeb4e34761a21cce7518d8ca1b0`, completed/success, job backend/frontend success.
Non è una nuova run né un test delle modifiche documentali ancora locali.

Le 5 high dev e la deprecazione ESLint restano aperte, non corrette. L'accettazione
non copre nuove vulnerabilità, auth/tenancy o runtime. Nessun gate è stato disabilitato
e nessun criterio della Definition of Done riscritto per nascondere un fallimento.
Le sezioni storiche "in attesa di accettazione" sono superate da questo aggiornamento.

Aggiornati eccezione, README, questo report, memory/handoff, prerequisito M0 del
backlog Sprint 1 e riferimento nell'ADR-009. Controlli documentali link/whitespace;
nessun nuovo audit npm, build/test/runtime, codice, migration, commit o push.
S1-001 resta da completare sui dettagli security; chiusura M0 non approva integralmente
l'ADR-009 né autorizza in blocco l'implementazione dei task Sprint 1.
