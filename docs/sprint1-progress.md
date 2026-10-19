# Sprint 1 — Progress report

Aggiornato: 2026-10-19, Europe/Rome.

## Stato

- S1-001: **completato nel solo perimetro documentale di analisi/approvazione tecnica**.
- S1-002: modelli e contratti completati, pubblicati e verificati in CI il 19 ottobre.
- S1-003: persistenza/migrazioni completate, pubblicate e verificate in CI il 19 ottobre.
- S1-004–012: non avviati. Nessun login, sessione o UI Sprint 1 presente.
- M0 chiuso con [SEC-EX-001](security-exception.md) accettata per solo sviluppo interno,
  riesame entro il 30 ottobre o prima secondo le sue condizioni, non prorogata.

## S1-001 — Decisioni e threat model

L'utente ha approvato esplicitamente il 17 ottobre: "ok parametri tecnici S1-001 approvati".
Decisioni documentate in [ADR-009](adr/ADR-009-identity-access.md) e
[baseline ID-01–07](identity-security-policy.md). Non è un'attestazione di compliance.

Modello approvato: SaaS, identità locale ASP.NET Core Identity, PostgreSQL/EF Core,
cookie first-party, account globali e membership con ruolo per azienda; collaborazione
senza entità Team/reparti, predisposizione multi-azienda, nessun CIAM obbligatorio.

Parametri tecnici approvati, non ancora implementati:

| Controllo | Baseline |
| --- | --- |
| Password | 15–128 caratteri, blocklist e hash standard; niente rotazione arbitraria |
| Anti-brute-force | 5 fallimenti consecutivi / lockout temporaneo 15 minuti; rate limit aggiuntivi da tarare |
| Email/reset | Verifica prima del tenant access; conferma 24 ore, reset 30 minuti |
| MFA | TOTP Owner/Admin prima della beta esterna; lifecycle/recovery da testare |
| Sessione | Idle 30 minuti, assoluta 12 ore, cookie HTTPS protetto e antiforgery |
| Revoca/tenant | Revoca backend, fail-closed, membership/read/write verificati server-side |
| Audit | Eventi sintetici, minimizzazione, accesso ristretto e retention da definire prima di dati reali |

Threat model e prove future: brute force/enumeration, recupero account, CSRF, session
theft/revoca, IDOR/cross-tenant, provisioning, indisponibilità dipendenze e leak nei log.
S1-001 ne definisce il disegno; non considera passati test che ancora non esistono.

## File di questo aggiornamento

- ADR-009: stato Accepted limitato al disegno locale corrente, storico managed escluso.
- identity-security-policy.md: approvazione esplicita e parametri fissati.
- tasks/SPRINT1_BACKLOG.md: S1-001 completato; task applicativi successivi non avviati.
- memory.md e handoff.md: stato e prossimi passi riallineati.
- questo report: registro di avanzamento Sprint 1.

Modifiche documentali precedenti preservate, nessun codice/configurazione/dependency
modificati. Nessun commit o push richiesto/eseguito.

## Verifiche proporzionate al blocco

Controllati whitespace, collegamenti locali e coerenza di stato/approvazione.
Build, test, runtime, audit npm e pipeline non rieseguiti per questo aggiornamento
soltanto documentale; la CI pubblicata di Foundation non è una verifica dell'auth futura.

## Limiti e gate non risolti dall'approvazione

Applicabilità NIS2, mappatura ACN, privacy/ruoli per finalità, accordi fornitori,
retention concreta, diritti, incidenti e readiness beta ancora da valutare. Nessun
claim commerciale di conformità autorizzato. Non richiesto o attivato un servizio email.
Binding/adapter del framework, keyring, benchmark hash, rate policy e prove delle
revoche restano lavoro implementativo da verificare, non garanzie implicite del default.

Il successivo blocco S1-002 è stato poi autorizzato esplicitamente, come registrato sotto.

## S1-002 — Modello e contratti, 2026-10-17

Richiesta: "Procedi esclusivamente con S1-002, mostrando prima i file previsti e la
strategia dei test". File/strategia mostrati prima del codice. Stato: completato
localmente nel perimetro di dominio/contratti, senza iniziare S1-003 o successivi.

Implementazione:

- UserAccount globale: ID, email normalizzata, stato/conferma e idoneità account;
- Tenant con Membership scoped: bootstrap primo Owner, ruoli Owner/Admin/Member,
  duplicati attivi/inattivi rifiutati e lookup della sola membership attiva;
- disattivazione membro senza rimozione Owner, collezione protetta e Member default;
- contratti di lettura in Application, nessuna credenziale, entity di modulo o ORM;
- [ownership e modello](identity-tenant-model.md) documentati, compresi vincoli DB futuri.

File creati/modificati per questo task:

| Area | File |
| --- | --- |
| Identity/Domain | UserAccount.cs |
| Tenants/Domain | Tenant.cs, Membership.cs, MembershipRole.cs, MembershipStatus.cs |
| Application/Contracts/Identity | IUserAccountReader.cs, UserAccountInfo.cs |
| Application/Contracts/Tenants | ITenantMembershipReader.cs, TenantMembershipInfo.cs, TenantAccessRole.cs |
| UnitTests | UserAccountTests.cs, TenantTests.cs; csproj con riferimenti espliciti ai due moduli |
| ArchitectureTests | DependencyBoundaryTests.cs: due controlli aggiunti, tre originali conservati |
| Documenti | identity-tenant-model.md nuovo; questo report, backlog, memory e handoff aggiornati |

Nessuna nuova dipendenza NuGet o project reference nei moduli. Nessun file solution,
API, frontend, workflow o configurazione di produzione modificato da questo task.
Le modifiche documentali già presenti nella working tree sono preservate.

### Quality gate effettivamente eseguiti

Toolchain locale effettiva: SDK **10.0.402** (il precedente 10.0.401 resta storico);
target net10.0 e global.json invariati.

- `dotnet build src/backend/Ramai.sln --configuration Release`: PASS, 0 warning/errori;
- seconda build `--no-restore` dopo self-review: PASS, 0 warning/errori;
- `dotnet test src/backend/Ramai.sln --configuration Release --no-build`: PASS,
  **49 superati, 0 falliti, 1 skipped** (39 unit, 5 architecture, 5 HTTP integration);
- `dotnet format src/backend/Ramai.sln --verify-no-changes --no-restore`: PASS;
- whitespace/link documentali e controllo file previsti: PASS.
- Gitleaks sui soli file modificati/non tracciati copiati in area temporanea: nessun
  leak rilevato. Avviso Git CRLF/LF sul README preesistente, nessun errore whitespace.

La suite ha 50 casi complessivi: i 16 baseline più 32 nuovi unit e 2 architecture.
Il test reale PostgreSQL/Redis/pgvector non è stato eseguito: opt-in non attivo e
Docker Desktop non disponibile, confermato dal fallimento della connessione alla pipe
del daemon. Nessun container/volume creato, avviato, alterato o eliminato nel task.
Non si dichiara una suite con infrastruttura tutta verde né una nuova run GitHub.
Frontend non rieseguito, essendo invariato; il debt npm rimane sotto SEC-EX-001.

### Limiti residui ed esclusioni

- Unicità globale NormalizedEmail e vincoli persistenti definiti ma non implementati:
  richiederanno repository/indici in S1-003. I test oggi provano normalizzazione e
  unicità membership nel singolo aggregato, non unicità del DB o concorrenza SQL.
- ConfirmEmail e creazione Owner sono transizioni di dominio, non verifiche token o
  autenticazione già presenti. Servizi futuri devono controllare actor/membership.
- Nessun reader implementato/registrato in DI, sessione, password store, Identity
  framework, migrazione, query filter, endpoint, team/reparto, connector o billing.
- Nessuna autorizzazione HTTP o isolamento SQL dimostrata da questi test di modello.
- Nessun commit/push richiesto o effettuato. Task successivi non avviati.

## S1-003 — Persistence e migrazioni, 2026-10-18

Richiesta: Docker avviato e prosecuzione del lavoro durante l'assenza dell'utente.
Il blocco è stato esplicitamente delimitato in apertura a S1-003; file, versioni,
layout e strategia test comunicati prima di modificare. Non è un'autorizzazione a
implementare automaticamente tutti i task dello Sprint 1 o a pubblicare modifiche.
Stato: **completato localmente**, con verifiche reali su infrastruttura temporanea.

### Implementazione e acceptance

- Due adapter degli stessi moduli sotto Persistence, senza nuove dipendenze tra i
  sette modelli di modulo o tra layer condivisi e moduli. Test originali conservati.
- IdentityStoreContext con ASP.NET Core Identity standard e unico store Users, senza
  tabella duplicata UserAccount, ruoli globali o credenziali seed.
- TenantsStoreContext con mapping dell'aggregato, PK/FK/CHECK/indici reali:
  NormalizedEmail globale univoca, TenantId/UserId unica anche inattiva, TenantId/UUID
  obbligatori, valori enum validi, al massimo un Owner attivo e delete Restrict.
- Reader implementati per contratti esistenti, coppia tenant/user e stato filtrati,
  mapping ruoli esplicito. Non registrati nell'API o spacciati per autenticazione.
- Migrazioni iniziali/snapshot e history per schema. Extension vector assicurata
  con IF NOT EXISTS su DB vuoto e mai rimossa dalle Down; nessuna Business Memory.
- DB vuoto e aggiornamento Foundation sintetica testati: sentinella dati e pgvector
  preservati. Rollback/riesecuzione iniziali e rollback transazione verificati solo
  in database usa-e-getta. Nessun reset/migrazione sui volumi preesistenti.
- [Documentazione operativa](database-migrations.md): ownership, comandi, privilegi,
  sicurezza credenziali, recovery provata e limiti espliciti.

### Versioni effettive e dipendenze

SDK 10.0.402, net10.0; Microsoft.EntityFrameworkCore/Design e relativo tool locale
10.0.12; Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.12;
Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Npgsql 10.0.3.
Pacchetti Design PrivateAssets=all e CLI fissata nel manifest, senza aggiornare
il tool globale 9.0.0 preesistente. Il restore segnala disponibile tool 10.0.13:
pin mantenuto coerente alla baseline scelta, non promessa di ultima patch.
Nuove dipendenze implementano EF/Identity/PostgreSQL già previsti da ADR-009;
nessun nuovo servizio infrastrutturale, provider auth o costo cloud.
Docker Engine 29.8.1; immagini esistenti pgvector/pgvector:0.8.6-pg17 e
redis:7.2.16-alpine3.21, Compose root invariato.

### File del blocco (distinti dal lavoro locale preesistente)

| Area | File creati/modificati |
| --- | --- |
| Tool | `.config/dotnet-tools.json` nuovo |
| Identity.Persistence | csproj, AccountUser.cs, IdentityStoreContext.cs, IdentityStoreFactory.cs, UserAccountReader.cs nuovi |
| Identity.Migrations | 20261018075342_InitialIdentity.cs, relativo Designer.cs, IdentityStoreContextModelSnapshot.cs nuovi |
| Tenants.Persistence | csproj, TenantsStoreContext.cs, TenantsStoreFactory.cs, TenantMembershipReader.cs nuovi |
| Tenants.Migrations | 20261018075226_InitialTenants.cs, relativo Designer.cs, TenantsStoreContextModelSnapshot.cs nuovi |
| Dominio Tenants | Tenant.cs: costruttore privato materializzazione e IReadOnlyList, wrapper read-only preservato |
| Solution | src/backend/Ramai.sln: inclusi solo i due adapter |
| IntegrationTests | csproj, PersistenceTests.cs e PersistenceModelTests.cs nuovi |
| ArchitectureTests | csproj e DependencyBoundaryTests.cs: nuovo controllo adapter, controlli precedenti mantenuti |
| Documentazione | database-migrations.md nuovo; questo report, repository-structure.md, identity-tenant-model.md, memory.md, handoff.md e tasks/SPRINT1_BACKLOG.md aggiornati |

README/Sprint0, decisioni e codice S1-002 già locali sono stati preservati. Nessun
file frontend, workflow, API, shared layer, Compose o init SQL modificato dal blocco.

### Quality gate e risultati locali

- `dotnet tool restore`: PASS, CLI locale fissata 10.0.12.
- `dotnet build src/backend/Ramai.sln --configuration Release --no-restore`: PASS,
  **0 warning, 0 errori**; restore eseguito anche nella prima build.
- Suite finale con RAMAI_RUN_INFRASTRUCTURE_TESTS=1: **58 superati, 0 falliti,
  0 skipped** = 39 unit + 6 architecture + 13 integration.
- Due test nuovi di metadati/script sempre eseguiti; cinque test nuovi persistence
  reali e test infrastrutturale originale eseguiti con Docker; cinque HTTP test
  originali sempre presenti. Ogni test SQL usa un DB generato separatamente.
- PostgreSQL/Redis Docker: entrambi healthy. Compose config --quiet: PASS.
- Vector presente su database vuoto e conservato dopo upgrade/rollback/reapply.
- Entrambi `dotnet ef migrations has-pending-model-changes`: nessuna differenza.
- `dotnet format ... --verify-no-changes --no-restore`: PASS.
- `dotnet list ... package --vulnerable --include-transitive`: nessuna vulnerabilità
  segnalata dalle fonti NuGet correnti, non garanzia di assenza assoluta di rischi.
- Gitleaks **8.30.1**, soli file modificati/non tracciati copiati in area temporanea,
  output redatto: **nessun leak rilevato**.
- `git diff --check`: PASS; soli avvisi CRLF/LF relativi alla policy repository.

Prima esecuzione senza opt-in eseguita durante sviluppo: test SQL esplicitamente
skipped, non presentati come superati. Prima prova SQL ha rilevato incompatibilità
List/ReadOnlyCollection del mapping EF: risolta esponendo IReadOnlyList con medesimo
wrapper protetto; test unit e round-trip reali successivi verdi. Nessun test eliminato.
Le risorse Compose temporanee sono state rimosse dopo ciascuna esecuzione; password
casuale solo in memoria/process environment, `.env` non creato/sovrascritto.

### Limiti, warning e technical debt

- **Nessuna esecuzione GitHub nuova**: risultati esclusivamente locali, nessun commit
  o push. Workflow preesistente scopre i nuovi test infrastrutturali con opt-in già
  attivo; la conferma effettiva su runner Linux richiederà pubblicazione autorizzata.
- Frontend invariato, gate npm non rieseguiti nel blocco backend. Le cinque high dev
  npm e incompatibilità peer/EOL ESLint restano quelle dell'audit del 16 ottobre sotto
  SEC-EX-001, non risolte o rivalutate qui; nessuna estensione della sua scadenza.
- No login/cookie/MFA/email, TenantContext o guard di scrittura: S1-004/005/007.
  Nessuna garanzia di autorizzazione o isolamento HTTP già completata.
- Coerenza utente/membership via contratto, provisioning atomico/idempotente e retry
  restano S1-006; niente FK o letture dirette cross-module. Indice Owner impone al
  massimo uno, non "almeno uno" contro SQL arbitrario.
- Recovery provata su migrazioni iniziali/fixture, non restore backup/PITR o RPO/RTO
  produzione. Down cancella dati di proprietà del modulo: mai eseguita su dati reali.
- Test SQL richiedono PostgreSQL di test con CREATEDB/extension; account runtime con
  privilegi minimi e deployment/migration governance restano lavoro pre-go-live.
- **S1-004 e task successivi non iniziati**. Prossimo passo da autorizzare: definire
  il primo sottoblocco autenticazione secondo ID-01–05, senza saltare gate security.

## Verifica e pubblicazione autorizzata — 2026-10-19

L'utente ha autorizzato verifica del lavoro fino a S1-003, commit/push e controllo
della pipeline GitHub. Nessuna nuova funzionalità o avvio S1-004 autorizzati nel blocco.
Riallineato il README allo stato effettivo, preservati lo storico dei report e
le decisioni locali già approvate. Pubblicazione comprensiva di documentazione M0,
SEC-EX-001/S1-001 e implementazioni S1-002/003, non solo degli ultimi file modificati.

Verifiche locali rieseguite oggi:

- remoto origin/main e HEAD inizialmente allineati a 4c441b3; nessuna divergenza;
- build Release: 0 warning/errori; format check PASS;
- **58 test backend superati, 0 falliti, 0 skipped**, con PostgreSQL/Redis temporanei;
- 7 test environment loader PASS;
- restore EF locale e controllo snapshot dei due contesti PASS;
- NuGet advisory scan, incluse transitive: nessuna segnalazione dalle fonti correnti;
- Node 24.19.0/npm 11.17.0; npm ci, lint, type-check e production build PASS;
- npm audit: stessa catena di 5 high (braces e dipendenti) sotto SEC-EX-001, 0 critical;
  npm audit --omit=dev: 0 segnalazioni; warning ESLint 9 deprecato ancora presente.

Nessun force/override di dipendenze o modifica alla scadenza dell'eccezione.
Nessun `.env`, dato cliente o credenziale effimera da pubblicare; container/volumi
temporanei rimossi e volumi preesistenti preservati.

### Evidenza effettiva GitHub, distinta dalle prove locali

Commit applicativo/documentale autorizzato:
`7da7758ea89f319c2e8c056f97f5c2e823b55476`, 47 file revisionati.
Gitleaks 8.30.1: nessun leak sia nella copia dei file candidati, sia nel diff del
commit effettivo prima del push. Pubblicazione su origin/main senza force push.

[RAMAI CI — run 39210403963](https://github.com/maffeinet/ramai/actions/runs/39210403963),
headSha esattamente 7da7758ea89f319c2e8c056f97f5c2e823b55476: **completed / success**.
Entrambi i job GitHub-hosted Ubuntu 24.04 conclusi con successo:

- backend: restore, build (0 warning/errori), format e test con servizi reali;
  **39 unit + 6 architecture + 13 integration = 58 pass, 0 fail, 0 skipped**;
- frontend: npm ci, lint, type-check e production build PASS;
- nei log frontend ancora deprecazione ESLint 9 e 5 high, non nascoste o risolte.

Questo conferma il funzionamento del nuovo codice anche sul runner Linux con SDK
10.0.401 fissato nel workflow, oltre al locale 10.0.402. Workflow invariato: secret
scan, advisory scan e test loader sopra sono verifiche locali manuali, non nuovi
gate automatici aggiunti. Non è stata effettuata alcuna distribuzione/deploy.

README, memory/handoff, stato backlog e questo report aggiornati con l'evidenza
acquisita. Il commit successivo che registra questi risultati è solo documentale:
la run linkata qui verifica il commit applicativo 7da7758, non un hash futuro.
S1-004 e task successivi non iniziati; nessuna deroga security nuova o prorogata.
