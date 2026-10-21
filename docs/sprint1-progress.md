# Sprint 1 — Progress report

Aggiornato: 2026-10-21, Europe/Rome.

## Stato corrente — 21 ottobre, completamento S1-008

Utente approva "tutto quanto è in sospeso" dopo richiesta di estensione host-only
ADR-010 e pubblicazione dei prerequisiti C/D/005/006/007 con S1-008. Nessuna UI/S1-009,
altro task, deploy o migrazione tooling. Blocchi di approvazione precedenti superati
dalla nuova istruzione, non aggirati. S1-008 **completato localmente nel perimetro
API/RBAC e pubblicato/verificato in CI**, [baseline e contratti](workspace-api-baseline.md).

File nuovi del completamento HTTP:

- src/backend/Ramai.Api/Composition/WebTenantsRegistration.cs;
- src/backend/Ramai.Api/Composition/WorkspaceRequestBudget.cs;
- src/backend/Ramai.Api/Workspaces/WorkspaceEndpoints.cs e WorkspaceHttpInput.cs;
- tests/Ramai.IntegrationTests/WorkspaceHttpTests.cs;
- docs/workspace-api-baseline.md.

Modificati Program.cs/progetto API, WebIdentityRegistration (reader già esistente),
AccountTransportMiddleware (estensione HTTPS/body/errors), DependencyBoundaryTests
(sole eccezioni host approvate, divieti fra moduli mantenuti), AccountSecurityFixture
(cleanup esclusivo namespace test quota). README/ADR-010/policy/struttura e documenti
di continuità aggiornati. File del primo step interno elencati nel checkpoint sotto;
pubblicazione include anche prerequisiti C/D/005/006/007 e documentazione collegata.

Gate locali Windows: **278 pass, 0 fail, 0 skipped** (101 unit, 169 integration,
8 architecture), Release 0 warning/errori, format verify PASS. Aggiunti 28 test HTTP,
incluso Kestrel HTTPS, ruolo/membership/account revocati, IDOR/CSRF/input e quota
distribuita fra host. Primo giro fallito per IUserAccountReader non registrato:
corretta DI con implementazione reale esistente; nessun test soppresso/mock auth.
Nessun nuovo NuGet/schema/servizio. EF Identity/Tenants nessuna modifica modello.
SDK 10.0.402, Node 24.19.0, npm 11.17.0, Next 16.3.8/React 19.3.0 invariati;
EF/Identity 10.0.12, Npgsql EF 10.0.3 e Redis client 3.3.1 invariati.

Frontend npm ci/lint/type-check/build PASS. Audit del 21: stessa catena 5 high dev,
0 critical, audit production 0; warning ESLint 9 fuori supporto. SEC-EX-001 aperta
senza proroga né soppressioni. Gitleaks 8.30.1 sui 72 candidati iniziali e poi
sul diff staged completo di 73 file senza leak (aggiunta documentazione struttura);
link locali e whitespace verificati. Docker dedicato
ramai-s1008-final healthy, vector 0.8.6 e Redis PONG; dopo i test container/rete/
volumi propri rimossi e assenza verificata. Nessun dato/volume utente modificato.

Commit e push effettuati: `06f054dc57e6be0495323acdfd131e7851d5542d`, 73 file,
secret scan staged e del commit senza leak; working tree pulito dopo pubblicazione.
[Run del commit](https://github.com/maffeinet/ramai/actions/runs/39564651224)
**success** su GitHub-hosted Ubuntu 24.04, SHA verificato: backend 0 warning/errori,
format e **278 test PASS/0 skipped** (101 unit, 169 integration, 8 architecture);
frontend npm ci/lint/type-check/build PASS. Cleanup servizi/volumi CI PASS.
Il verde Linux deriva dalla CI reale, non da un nuovo clone Linux locale.
Questo aggiornamento documentale registra la run del codice 06f054d: un successivo
commit documentale non è lo SHA testato da quella run e avrà la propria pipeline.
Restano limiti operativi/security e beta non
autorizzata nella baseline; UI/S1-009 è il prossimo task da autorizzare separatamente.

## Checkpoint precedenti

- Ripresa notturna 23:07: **S1-008 PARZIALE INTERNO**, non API completa.
  Contratti, RBAC applicativo e adapter scoped verificati: 250 test verdi/0 skipped
  (101 unit, 141 integration, 8 architecture), Release/format/frontend/EF PASS.
  [Piano, file e proposta di composizione](workspace-api-plan.md). Per integrare
  HTTP serve approvazione estensione host-only ADR-010, attualmente Identity-only.
  Nessuna modifica del grafo/riferimento, nessun nuovo endpoint o push effettuati.

- Notte 20/21 ottobre: **S1-007 completato localmente**, [baseline isolamento](tenant-isolation-baseline.md).
  222 test pass/0 skipped (79 unit, 135 integration, 8 architecture), Release
  0 warning/errori, format e frontend lint/tipi/build PASS; EF nessun cambiamento.
  Autorizzati S1-007 e poi S1-008, documentazione per step e commit/push pertinenti
  dopo scan/gate verdi e CI reale verificata; inclusione prerequisiti C/D/005/006
  da confermare dopo il rifiuto automatico documentato sotto.
  Nessuna UI/S1-009 o deploy. Le note successive conservano checkpoint storici.

## S1-008 — Primo step indipendente, ripresa notturna

File creati, nessun altro codice produttivo modificato in questo step:

- src/backend/Ramai.Application/Contracts/Tenants/WorkspaceOperations.cs;
- src/backend/Ramai.Application/Tenancy/WorkspaceOperationsService.cs;
- src/backend/Persistence/Ramai.Modules.Tenants.Persistence/WorkspaceDataStore.cs;
- tests/Ramai.UnitTests/WorkspaceOperationsTests.cs;
- tests/Ramai.IntegrationTests/WorkspaceOperationsIntegrationTests.cs;
- docs/workspace-api-plan.md.

Report/memory/handoff/backlog aggiornati. Nessun nuovo NuGet/schema/riferimento.
Servizio risolve sessione/membership ogni volta, legge workspace per tutti i membri
ammessi e autorizza settings/solo nome a Owner/Admin con MFA. Nessun input Role,
UserId o TenantId sulla mutazione; porta interna riceve un contesto verificato,
adapter usa TenantDataContext e non il percorso bootstrap globale. Validazione nome
1–256 UTF-16 dopo trim, controlli/caratteri sostitutivi rifiutati senza troncamenti.
Porta che restituisce scope/ruolo non corrispondente causa rifiuto, non un fallback.

22 unit + 6 integration nuovi PASS; suite totale 250 PASS, 0 fail/skipped, Release
0 warning/errori, format verify e frontend lint/type-check/build PASS. Test reali
PostgreSQL/Redis con login/MFA esistenti, non auth simulata; Owner A/Member B,
Admin, modifica lecita solo del tenant corrente, downgrade/revoca membership e
account rifiutati alla richiesta seguente. Non sono test HTTP dei nuovi endpoint.
EF Identity e Tenants nessun cambiamento; Docker ramai-s1008-internal healthy,
pgvector 0.8.6 e Redis PONG. Nessun dato/volume utente toccato.

S1-008 resta aperto: host, endpoint /api/v1, ProblemDetails e matrice HTTP/CSRF/IDOR
non implementati. ADR-010 attuale permette soltanto adapter Identity nel composition
root: proposta estensione Tenants documentata, attende decisione umana secondo
AGENTS 18. Nessun aggiramento con altri layer/reflection o indebolimento test.
Pubblicazione resta in attesa della distinta conferma sui prerequisiti dopo rifiuto
automatico precedente; nessun tentativo git nuovo. Nessuna CI/Linux/clone nuovo.
SEC-EX-001/catena npm dev restano aperte senza proroga; audit non rieseguito.
Gitleaks 8.30.1 su tutti i 65 file locali candidati senza leak, link locali di
17 documenti Markdown e whitespace PASS. Container/rete/volumi del solo progetto
ramai-s1008-internal rimossi, assenza verificata; fixture ricreabili e dati utente
intatti. HEAD 31129fa invariato, index vuoto; nessun nuovo commit o pipeline.

## S1-007 — Isolamento letture/scritture, 20 ottobre

Creati TenantDataContext, TenantDataContextFactory, TenantQueryBoundary, TenantsModel
nel persistence adapter Tenants, TenantIsolationTests e tenant-isolation-baseline.md.
Modificati TenantsStoreContext (mapping condiviso), helper WithIdentity dei test
provisioning (riuso interno), DependencyBoundaryTests (bootstrap allowlist), procedure
migrazioni (specificare contesto), report/backlog/memory/handoff. Nessun NuGet nuovo.
Versioni restano SDK 10.0.402, EF 10.0.12, Npgsql EF 10.0.3, PostgreSQL/pgvector
0.8.6-pg17, Redis 7.2.16-alpine3.21; frontend Next 16.3.8 invariato.

20 nuovi test isolamento verdi; corretto un test che attendeva il rifiuto di una
chiave manomessa troppo tardi: EF lo rifiuta già all'assegnazione. Aggiunti insert
lecito, current key e batch misto senza scrittura parziale; nessun test soppresso.
Suite completa 222/222 senza skipped; format verify PASS e build 0 warning/errori.
Frontend lint/type-check/build PASS; Docker progetto isolato ramai-s1007-check
healthy, pgvector 0.8.6 e Redis PONG. Nessun dato/volume utente o schema nuovo.
EF pending-model PASS con --context TenantsStoreContext; primo comando senza
contesto ambiguo dopo il nuovo contesto operativo, istruzioni aggiornate.
Gitleaks 8.30.1 sui 59 file locali candidati: nessun leak; git diff --check PASS.
Pubblicazione/CI ancora da registrare: i verdi storici non verificano
questo codice. Nessuna nuova prova clone pulito/Linux locale. SEC-EX-001 e cinque
high npm dev note non risolte né prorogate; audit non rieseguito in questo step.
Limiti ORM/RBAC/revoca in corso espliciti nella baseline; nessuna beta autorizzata.

Tentativo di pubblicazione: comando staging/scan-staged/commit/push respinto prima
dell'esecuzione dalla revisione automatica: include prerequisiti C/D/005/006, scope
giudicato più ampio dell'autorizzazione esplicita 007/008. Nessun nuovo commit/push
o CI; chiesta conferma del perimetro, nessun aggiramento. Scan su directory dei
59 file era già PASS; scan staged non eseguito. S1-008 non iniziato in questa ripresa.
Risorse temporanee S1-007 rimosse (container/rete/volumi propri), assenza verificata;
nessun dato utente eliminato. Le fixture sintetiche possono essere ricreate dai test.

## Checkpoint precedenti

- Blocco corrente: **S1-006 completato nel perimetro interno**, 201 test locali verdi,
  [baseline](workspace-provisioning-baseline.md). Nuova migrazione Tenants per
  idempotenza, nessuna applicazione ai dati utente o nuova API. Scope S1-007+ escluso;
  nessun commit/push, modifiche C/D/S1-005/demo preservate.

- Ultima ripresa: **S1-005 completato nel perimetro applicativo interno**,
  [baseline e limiti](tenant-context-baseline.md). 178 test backend locali verdi;
  nessun nuovo endpoint workspace o wiring API→Tenants, da integrare in S1-008.
  C/D/demo locali preservati; nessun commit/push. S1-006 aggiunto nel blocco successivo.

- Ripresa diurna 20 ottobre: **S1-004D implementato localmente**, autorizzazione
  esplicita MFA/TOTP e recupero; [baseline D](account-mfa-baseline.md). Modifiche C/demo
  preservate. Nessun commit/push o nuova CI; S1-005 aggiunto nella ripresa successiva,
  nessuna UI. Questo
  aggiornamento prevale sullo stato storico "D non iniziato" sottostante.

- Aggiornamento 20 ottobre: S1-004C implementato localmente (API/sessioni opt-in),
  seguito da D/MFA, gate locali verdi registrati sotto. Nessun commit/push;
  tenant isolation e UI non iniziati. Le sezioni A/B/C successive conservano lo storico.
- S1-001: **completato nel solo perimetro documentale di analisi/approvazione tecnica**.
- S1-002: modelli e contratti completati, pubblicati e verificati in CI il 19 ottobre.
- S1-003: persistenza/migrazioni completate, pubblicate e verificate in CI il 19 ottobre.
- S1-004A/B: componenti interni account/password e conferma/reset completati;
  pubblicati nel commit `ca7edf4`, CI verde con 112 test senza skipped. S1-004 complessivo aperto,
  C/D e S1-005/006 interni implementati localmente; S1-007+ non iniziati.
- Endpoint/sessioni API C e MFA D presenti solo con configurazione opt-in; nessuna UI.
- M0 chiuso con [SEC-EX-001](security-exception.md) accettata per solo sviluppo interno,
  riesame entro il 30 ottobre o prima secondo le sue condizioni, non prorogata.

## S1-006 — Provisioning workspace e primo Owner, 20 ottobre

Autorizzazione esplicita per il solo S1-006. Annunciati file, transazione, retry,
MFA e test prima delle modifiche; riletti AGENTS, handoff, S1-005, modello/policy,
acceptance e procedure migrazioni. Nessun endpoint o nuovo wiring API→Tenants,
nessuna modifica stack/boundary e nessun nuovo NuGet o servizio.

File creati:

- Application/Contracts/Tenants/WorkspaceProvisioning.cs;
- Application/Tenancy/SessionSubject.cs e WorkspaceProvisioningService.cs;
- Persistence/Ramai.Modules.Tenants.Persistence/WorkspaceProvisioningRequest.cs
  e WorkspaceProvisioningStore.cs;
- Migrations/20261020123949_WorkspaceProvisioning.cs e relativo Designer.cs;
- tests/Ramai.UnitTests/WorkspaceProvisioningTests.cs;
- tests/Ramai.IntegrationTests/WorkspaceProvisioningIntegrationTests.cs;
- docs/workspace-provisioning-baseline.md.

Percorsi Application/Persistence relativi a src/backend. Modificati:
TenantContextResolver (piccola estrazione dei controlli sessione già esistenti,
senza modificarne la policy), TenantsStoreContext e snapshot, PersistenceTests e
PersistenceModelTests per due migrazioni/nuova tabella posseduta dal modulo.
README/backlog/report/memory/handoff/procedure migrazioni aggiornati. File precedenti
C/D/S1-005/demo ancora locali: non attribuire a questo task tutto git status.

Admission: sessione valida, account attivo/confermato via contract e MFA richiesta
per il nuovo Owner; ID/ruolo creator non forniti dal client. Non serve già un tenant
attivo per creare il primo workspace. Nome trim/limiti, chiave non vuota; TenantId
casuale server. Unica transazione nel modulo Tenants per Tenant+Owner+registrazione,
nessuna query o scrittura diretta a Identity dall'adapter Tenants.

Registro tenant-scoped con TenantId e PK actor/request. Retry stesso payload → stesso
TenantId, diverso payload → Conflict; concorrenti risolti dalla PK senza tenant orfani.
Contesto DB separato per tentativo, errore pre-commit annulla tutto; esito commit
incerto si riconcilia con stessa chiave. Replay ricontrolla Owner attivo e il servizio
ripete admission, mai successo dopo revoca. Log sintetico con correlation ID, nessun
nome workspace/request key/password. Non è audit segregato o esposizione HTTP pronta.

### Migrazione e qualità S1-006

Migrazione aggiuntiva 20261020123949_WorkspaceProvisioning, solo schema Tenants;
Up preserva tenant/membership esistenti, Identity e pgvector. Down rifiuta registri
non vuoti (55000, wrapper InvalidOperationException del provider verificato), non
resetta la memoria dei retry. La recovery con dati richiede review/backup; non
svuotare le righe per forzare il rollback. Nessuna migrazione sui volumi utente.

SDK 10.0.402; EF/Identity e CLI 10.0.12, Npgsql EF 10.0.3, stack frontend e immagini
Compose invariati. Build Release 0 warning/errori; format PASS. Coerenza modello EF
PASS e script idempotente generato in artifacts/s1006-tenants-idempotent.sql (ignorato
da Git, non applicato a dati utente). Frontend lint/tipi/build PASS.

Test coprono admission/creazione Owner reale, retry/conflict, 12 tentativi concorrenti,
errore dopo SaveChanges prima del commit con zero righe residue, takeover con tenant ID
come request key, scoping actor, revoca/replay, upgrade/rollback con dati sintetici.
Prime iterazioni hanno rilevato extension ASP.NET non disponibile nel layer Application
(usato FindFirst.Value standard, senza dipendenza extra), provider log fixture diverso
dall'host e wrapper dell'errore migrazione. Corretti senza soppressioni/test skipped.
Gate finali locali:

- **201 pass, 0 fail, 0 skipped**: 79 unit, 115 integration, 7 architecture;
  incremento 23 test (15 unit + 8 integration) rispetto ai 178 di S1-005.
- Release 0 warning/errori; format --verify-no-changes PASS; nessuna nuova dipendenza.
- Modello EF senza modifiche pendenti, script idempotente generato, upgrade/
  rollback vuoto preservano i dati; rollback con registrazioni rifiutato senza perdita.
- Frontend lint, type-check e production build PASS; nessun cambio frontend/tooling/CI.
- Compose config PASS; PostgreSQL/Redis healthy, vector 0.8.6 e Redis PONG verificati.
  Password Docker solo in memoria; risorse ramai-s1006-check rimosse, nessun dato o
  volume utente cancellato. Nessun .env scritto o auto-migrate del runtime.
- Gitleaks 8.30.1 su 53 file locali candidati, inclusi untracked C/D/S1-005/demo:
  nessun leak, output redatto; link Markdown e git diff --check PASS. Solo avvisi Git
  CRLF→LF della policy esistente, non warning della build.
- Nessun commit/push, nuova esecuzione GitHub o verifica Linux/clone pulito.

Limiti: integrazione HTTP/CSRF/quote pubbliche e selezione sessione in S1-008, filtri
e write guards S1-007 non iniziati. Admission Identity è una verifica tramite contratto,
non una transazione/lock cross-module: revoche di operazioni già ammesse non annullano
retroattivamente il commit, come documentato nella policy. Ancora niente UI/email
reale/deploy o beta esterna. Nessuna nuova CI/Linux/clone pulito per il codice locale;
SEC-EX-001/warning npm invariati, nessuna proroga o audit npm nuova.

## S1-005 — TenantContext interno, 20 ottobre

Utente autorizza esclusivamente S1-005. Riletti AGENTS, handoff, acceptance,
modello/contratti e ADR-009/010. Piano file/test annunciato prima del codice,
incluso il limite composition: nessuna nuova dipendenza API→Tenants implicita.
Scope interno richiesto dal task; trasporto HTTP/DI host e salvataggio nel ticket
protetto sono integrazioni S1-008, non una selezione browser già disponibile.

Creati:

- src/backend/Ramai.Application/Contracts/Tenants/TenantContext.cs;
- src/backend/Ramai.Application/Tenancy/TenantContextResolver.cs;
- tests/Ramai.UnitTests/TenantContextTests.cs;
- tests/Ramai.IntegrationTests/TenantContextIntegrationTests.cs;
- docs/tenant-context-baseline.md.

Modificati per il codice/test: AccountSessions.cs e RedisSessionTicketStore.cs
per condividere la stessa durata assoluta di dodici ore, senza cambiarla;
MfaTests.cs rende internal il solo generatore test TOTP per riusarlo nella prova
del contesto Owner, senza cambiare l'implementazione MFA. Documenti aggiornati:
README, policy, identity-tenant-model, report, memory, handoff e backlog.
Non attribuire a S1-005 tutte le modifiche git locali preesistenti di C/D/demo.

Resolve controlla principal cookie univoco, scadenza assoluta, stato account/stamp
e membership attiva a ogni chiamata. Nessuna selezione implicita; mismatch/ruolo
ignoto rifiutati. Select tratta TenantId solo come proposta, restituisce una copia
verificata del principal senza ruoli e preserva inizio/stamp; nessun input mutato.
Owner/Admin richiedono MFA; ruolo solo dal DB. Contesto immutabile, nessuno stato
globale/AsyncLocal o query diretta a un altro modulo; eccezioni senza fallback grant.

### Acceptance e qualità S1-005

- Contesto assente: SelectionRequired con Context null, nessun primo-tenant fallback.
- Identità/account/membership invalidi: rifiutati; verifiche reali di disattivazione,
  conferma revocata, lockout, stamp, membership inattiva e cambio ruolo.
- Claim duplicati/malformati/mismatch e ruolo client: non conferiscono contesto.
  Il servizio non legge HTTP: firma/TTL idle/ticket Redis sono compito del cookie
  handler C; non si spaccia un principal finto per prova crittografica.
- Selezione interna validata; persistenza nel ticket e CSRF del futuro endpoint
  restano S1-008. Nessun test HTTP/claim browser o E2E workspace dichiarato completo.
- Venti richieste concorrenti con scope e DbContext distinti su A/B non condividono
  né sovrascrivono contesto. Owner A/Member B con MFA reale e accesso a C negato.
- Build Release **0 warning/0 errori**; suite **178 pass, 0 fail, 0 skipped**:
  64 unit, 107 integration, 7 architecture. Incremento: 25 unit + 7 integration.
  Include regressione completa C/D, test MFA su HTTPS Kestrel e confini invariati.
- Primo build ha rilevato un nullable warning bloccante nel nuovo test; corretto
  verificando il tipo del principal. Nessuna soppressione, test escluso o nuovo NuGet.
- SDK 10.0.402; versioni stack, manifest, lockfile, Compose e schema invariati.
- PostgreSQL/pgvector e Redis di test isolati (ramai-s1005-check), password solo
  nel processo, nessun .env scritto o volume utente toccato.

- Format --verify-no-changes PASS; frontend lint/type-check/production build PASS.
- Docker Compose config PASS, PostgreSQL/Redis healthy, vector 0.8.6 e Redis PONG.
  Rimossi soltanto container/rete/volumi temporanei del progetto ramai-s1005-check.
- Gitleaks 8.30.1 sui 38 file locali candidati C/D/S1-005/documentazione (inclusi
  untracked): nessun leak; output redatto. Link locali e git diff --check PASS.
  Gli avvisi Git CRLF→LF sono normalizzazione della policy esistente, non errori.
- Nessuna esecuzione Linux/clone pulito/GitHub per questo codice locale.

Una review finale ha esteso anche alla selezione il rifiuto dei claim tenant
malformati, mantenendo i test e ripetendo i gate. Nessuna regola/test soppressi.
Restano SEC-EX-001/5 high dev npm già documentate (nessuna nuova audit o proroga),
integrazione host S1-008, provisioning S1-006, filtri/protezioni write S1-007, UI,
email/recovery operativo e review security prima di esterni. Prossimo task S1-006,
non iniziato automaticamente.

## S1-004D — MFA TOTP e recupero, 20 ottobre

Annunciati perimetro, file, dipendenze e test prima delle modifiche. Riletti AGENTS,
policy ID-04/05, backlog, sessioni/ADR e report/handoff/memory alla ripresa richiesta.
Nessun nuovo NuGet: provider Authenticator e recovery standard Identity, Data
Protection e infrastruttura già approvata. SDK 10.0.402, runtime test 10.0.13;
Identity/EF 10.0.12, Npgsql EF 10.0.3 e StackExchange.Redis 3.3.1 invariati.

Creati per D:

- src/backend/Ramai.Application/Contracts/Identity/AccountMfa.cs;
- src/backend/Persistence/Ramai.Modules.Identity.Persistence/Sessions/AccountMfaService.cs;
- src/backend/Persistence/Ramai.Modules.Identity.Persistence/Sessions/ProtectedIdentityTokenStore.cs;
- src/backend/Ramai.Api/Authentication/MfaEndpoints.cs;
- tests/Ramai.IntegrationTests/MfaTests.cs;
- docs/account-mfa-baseline.md.

Modificati per D: AccountSessions.cs, AccountSessionServices.cs,
WebIdentityRegistration.cs, AccountEndpoints.cs e WebSessionTests.cs, oltre a README,
account-session-baseline, policy, questo report, backlog, memory e handoff.
Molti file C sono ancora untracked: non attribuire a D tutti i file di git status.
Nessun file frontend/CI/laboratorio o nuova migrazione modificati in D.

Enrollment pending 10 minuti legato allo stamp; conferma con password e nuovo TOTP,
chiave precedente conservata fino a conferma riuscita. Token MFA cifrati per utente/
slot, nessun fallback plaintext. Row lock e consumo atomico impediscono replay su
istanze concorrenti; digest TOTP tenuti tre minuti, recovery standard consumati una
volta. Cinque errori MFA/15 minuti indipendenti dal contatore password, quote Redis.
Conferma/rotazione/recovery cambiano stamp e revocano sessioni precedenti. Nessuna
sessione password-only con MFA attiva, nessun endpoint per disabilitarla senza prova.

Il login recovery include una prova server-side monouso, legata allo stamp e valida
cinque minuti per un enrollment/rigenerazione con password nuovamente verificata:
anche l'ultimo recovery rimasto può ripristinare il dispositivo. Non esposta nel JSON.
Gestione con un ulteriore recovery esplicito richiede nuovo login dopo revoca;
documentati tutti i passaggi. Non effettuare logout/login nello stesso handler:
il ticket store mantiene Renew XX anti-resurrezione, non indebolito per il recupero.

Problemi individuati e corretti durante i cicli: import composition mancante,
analizzatore xUnit2031, fixture C con entità obsoleta dopo aggiornamento concorrente,
e logout/login nella stessa richiesta incompatibile con Renew XX. Aggiunti test,
nessun controllo soppresso o test saltato. Primo checkpoint dedicato 10/10 verde,
poi 142/142 e 143/143 verdi; gate della suite finale estesa registrati sotto.

### Gate D — esclusivamente locali

- Build Release PASS: **0 warning, 0 errori**; format --verify-no-changes PASS.
- Suite completa finale **146 pass, 0 fail, 0 skipped**: 39 unit, 100 integration,
  7 architecture. D aggiunge 19 casi rispetto ai 127 test di C, senza rimuoverne.
  Include ultimo recovery/prova monouso, corse tra host e percorso MFA completo
  su socket HTTPS Kestrel reale; nessuna modifica al trust store o bypass TLS.
- Frontend invariato: lint, type-check e production build PASS (Next 16.3.8).
- Docker Compose config PASS; container dedicati ramai-s1004d-check healthy,
  pgvector 0.8.6 e Redis PONG verificati. Immagini già previste: pgvector/pgvector
  0.8.6-pg17 e Redis 7.2.16-alpine3.21, nessun cambio Compose.
- Secret Docker casuale solo in memoria; nessun .env scritto o dati utente modificati.
- Gitleaks 8.30.1 (download ufficiale con SHA-256 verificato): 32 file candidati
  C/D/documentazione, inclusi untracked, nessun leak. Output redatto; non garanzia
  assoluta né nuova scansione della storia Git. Whitespace e link locali verificati.
- Nessun commit/push; non attribuire a C/D i verdi storici di GitHub. Rimossi
  container/rete/volumi Docker dedicati ai test, senza toccare volumi utente.

Limiti: niente Linux/clone pulito/GitHub per C/D, obbligo MFA Owner/Admin da collegare
a ruoli server-side, isolamento e UI non iniziati. Mancano email reale/self-service
HTTP completo, recupero assistito con tutti i fattori persi, audit segregato/retention,
runbook keyring e review umana. SEC-EX-001/5 high npm dev già documentate restano
aperte senza proroga: nessun nuovo audit npm o migrazione tooling in questo blocco.
I warning non equivalgono a un'autorizzazione agli utenti esterni.

## S1-004C — Sessioni API, 20 ottobre

Utente autorizza il piano C dopo chiarimento sulla sicurezza. File/dipendenze/test
annunciati prima del codice. ADR-010 consente il riferimento API→adapter soltanto
nel composition root; handler tramite contratti Application e test architetturale
aggiuntivo. [Baseline dettagliata](account-session-baseline.md).

Creati: contratti AccountSessions, tre componenti Sessions nell'adapter, Composition/
WebIdentityRegistration, Authentication/AccountEndpoints, AccountTransportMiddleware,
WebSessionFixture/WebSessionTests, ADR-010 e baseline sessioni. Modificati Program e
csproj API, registration/esito/credential snapshot A, fixture test per pulizia dei
namespace generati, test architettura, backlog, README/policy/report/memory/handoff.
Piano demo locale preesistente preservato e aggiornato con target 30 novembre e
nome pubblico Agentilio; nessuna rinomina o acquisto/attivazione provider.

Cookie host-only Secure/HttpOnly/Strict, ticket Redis cifrato con TTL/rinnovo atomico
senza resurrezione dopo logout, 30 minuti idle/12 ore assolute; stamp/account da DB
per richiesta. Password reset e revoca cambiano l'idoneità della sessione. MFA attiva
non aggirabile dalla via password-only. Antiforgery anche sul login; body/origin/HTTPS,
rate A e errori 401/429/503 senza dettagli. Nessun principal business/ruolo tenant.
Auth disabilitata di default; configurazione completa/certificato/keyring necessari
per attivarla, nessuna chiave effimera di produzione o schema DB nuovo.

Nessun nuovo NuGet: framework ASP.NET Core e versioni EF/Identity/Npgsql/Redis
preesistenti. Primo ciclo test ha rilevato configurazione
TestServer tardiva (404), poi Redis disposed reso 503; prova TLS Windows richiedeva
DefaultKeySet solo nel certificato test SChannel. Nessun test saltato/soppresso,
nessun downgrade TLS runtime. MFA, UI, account self-service HTTP e review beta restano
da completare. Nessuna pubblicazione GitHub: i verdi storici non testano C locale.

### Gate finali C — locali, non GitHub/Linux

- SDK 10.0.402; target .NET 10 invariato. Nessuna versione NuGet aggiornata.
- docker compose config PASS; PostgreSQL/pgvector e Redis healthy su progetto
  temporaneo ramai-s1004c-check, secret generato solo nel processo.
- Build Release PASS: 0 warning, 0 errori; format --verify-no-changes PASS.
- Suite completa: **127 pass, 0 fail, 0 skipped**: 39 unit, 7 architecture,
  81 integration. Incremento 15 test rispetto ad A/B: 14 sessioni/HTTP e un confine
  architetturale. Include Kestrel HTTPS reale, non soltanto TestServer.
- Rinnovo concorrente/logout (20 interleaving), idle/assoluto, stamp/reset/revoca,
  account non idonei/MFA, CSRF/origine/body, quota account e 120/min socket con
  X-Forwarded-For falsificati, ticket cifrati/manomessi, restart keyring e guasti.
- Frontend invariato: lint, type-check, production build PASS. Nessuna migrazione
  tooling o nuovo lockfile frontend; SEC-EX-001 resta aperta senza proroga.
- Rimossi esclusivamente container/rete/volumi temporanei creati per la prova,
  database e namespace test generati. Nessun dato utente rimosso.
- Gitleaks 8.30.1: 26 file locali candidati, nessun leak rilevato, output redatto.
  Include le cinque modifiche del piano demo preesistente, preservate. Whitespace
  PASS e collegamenti locali di 10 documenti verificati. Nessun nuovo secret reale.
  Avvisi Git soltanto di normalizzazione line ending già definita in .gitattributes.

Non eseguita CI GitHub di C o verifica Linux di C, nessun commit/push. Prima della
beta: MFA/D, UI/E2E, tenant isolation, operatività email/keyring/backup/audit e review
umana security. Prossimo blocco: S1-004D, non dichiarare completa la demo.

## Storico S1-001 — Decisioni e threat model

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

## S1-004A — Account, password e anti-brute-force, 2026-10-19

Autorizzato esclusivamente questo sottoblocco, con preview file/dipendenze/test prima
di scrivere. **Completato localmente come componenti interni**, non come login pubblico.
S1-004 complessivo resta aperto; B/C/D, TenantContext e task successivi non iniziati.
Nessun commit/push, migrazione, dato cliente o nuovo servizio esterno.

### Risultato e acceptance del sottoblocco

- IdentityCore/UserManager usa lo store account S1-003: creazione con email non
  confermata, hash standard, email normalizzata univoca e risposta generica ai duplicati.
- Password 15–128 scalari Unicode NFC, senza trim/troncamento o composizione imposta;
  denylist offline versionata/licenziata e controlli contestuali. Dataset assente o
  invalido non consente fallback permissivo.
- IdentityV3 PBKDF2-HMAC-SHA512 a 220.000 iterazioni, salt standard, verifica/rehash
  testati. Benchmark locale 7 campioni: mediana hash 71,0 ms, verify 71,6 ms;
  non benchmark del futuro hosting o garanzia capacità 500 MAU.
- Lockout standard 5 fallimenti/15 minuti, anche con password troppo corta;
  reset su password corretta e scadenza dimostrati. Account disattivati, bloccati,
  non confermati o con lockout disabilitato non ricevono esito credenziali Accepted.
- Row lock parametrizzato sulla propria riga Identity serializza incrementi e rehash
  fra richieste concorrenti; scope con refresh dello stato persistito, niente contatori
  singleton/in-memory o nuove tabelle.
- Lua Redis atomico: per operazione 300/min, origine server 120/min, account 10/min,
  finestra fissa TTL 60s, HMAC per account/IP, RetryAfter generico 60s.
  Quote condivise fra istanze; cap globale prima della creazione di partizioni nuove.
- Account assente/errato/bloccato/disattivato/non confermato: stesso esito generico;
  hash dummy per ridurre percorsi veloci. Risposte throttle equivalenti anche per
  account inesistente; guasti DB/Redis fail-closed, senza dettagli nelle risposte.
- Eventi strutturati operation/outcome e correlation ID; test catturano anche scope,
  eccezioni/log framework e verificano assenza di password/hash/email/IP e connessione.

Documentazione di design, prerequisiti e limiti:
[account-security-baseline.md](account-security-baseline.md).

### Dipendenze, corpus e file

Nuova sola PackageReference **StackExchange.Redis 3.3.1** nel proprio adapter Identity,
versione già presente in soluzione. Identity/EF 10.0.12 e Npgsql/provider 10.0.3
invariati. Redis già approvato per short-lived state, nessuna infrastruttura aggiunta.
SDK 10.0.402/runtime 10.0.13 locali. API/host non registrano ancora il componente:
nessun SignInManager, cookie scheme, token provider, sender, endpoint o UI.

Corpus pubblico SecLists/NCSC MIT fissato a commit e checksum, 99.839 voci non vuote;
329 digest distinti per voci compatibili con il minimo 15 caratteri. Le altre sono
già escluse dalla lunghezza: non si tratta di una lista manuale di pochi esempi.
Non è comunque un controllo universale di tutti i breach. Licenza inclusa nel progetto
e negli output; script di aggiornamento non riceve né invia password utente.

File creati/modificati (24):

| Area | File |
| --- | --- |
| Identity.Persistence | csproj: Redis e risorse/licenza, nessuna modifica al modello EF |
| AccountSecurity | AccountCredentialService.cs, AccountOperationResult.cs, AccountPasswordValidator.cs, AccountRateLimiter.cs, AccountRequestContext.cs, AccountSecurityRegistration.cs, DummyPasswordWork.cs, NormalizedPasswordHasher.cs, PasswordText.cs |
| AccountSecurity/Data | ncsc-eligible.sha256, SecLists-LICENSE.txt |
| Script | scripts/Update-PasswordBlocklist.ps1 |
| IntegrationTests | AccountPasswordTests.cs, AccountSecurityFixture.cs, AccountSecurityTests.cs |
| Documentazione | account-security-baseline.md nuovo; README, identity-security-policy, memory, handoff, questo report e tasks/SPRINT1_BACKLOG.md aggiornati |
| Line ending | .gitattributes: LF esplicito per .sha256, necessario al checksum riproducibile |

### Quality gate locali effettivi

- Build Release: **0 warning/errori**; formatter applicato, verify-no-changes PASS.
- Suite finale con servizi PostgreSQL/Redis reali temporanei:
  **82 pass, 0 fail, 0 skipped** = 39 unit + 6 architecture + 37 integration.
  I 58 casi precedenti conservati più 24 nuovi (15 senza infrastruttura e 9 opt-in).
- Policy/hash/resource/normalizzazione, flussi account, guasti, concorrenza, quote,
  TTL, log e timing verificati senza disabilitare gate o test precedenti.
- Nessuna variazione del modello rispetto alla migration Identity esistente.
- Scan NuGet anche transitive: nessuna advisory rilevata dalle fonti correnti.
- Compose config --quiet e health dei servizi temporanei PASS; pgvector e baseline
  infrastrutturale rimangono nella suite esistente. Nessun volume preesistente toccato.
- Esecuzione distinta senza opt-in: **67 pass / 15 skipped espliciti**, non confusa
  con la suite reale sopra. Le prove infra richiedono RAMAI_RUN_INFRASTRUCTURE_TESTS=1.
- Gitleaks **8.30.1** sui soli file modificati/non tracciati in copia temporanea,
  output redatto: nessun leak; git diff --check PASS, soli avvisi LF/CRLF di Git.

Il test di guasto iniziale ha rilevato NpgsqlException wrappata da InvalidOperationException;
aggiunto il catch filtrato specifico senza mascherare tutte le eccezioni di programmazione.
Prove successive verdi. Il confronto timing locale sintetico (3 campioni per caso)
ha dato mediana account presente 94,8 ms, assente 77,7 ms: query/lock/update diversi,
**nessuna prova constant-time o di eliminazione dell'enumerazione temporale**.

### Limiti residui e confine dei blocchi

- Il composition point è usato dai test, **non** da Program.cs: nessun endpoint già
  protetto o login utilizzabile. Rate policy HTTP, trusted proxy/IP, limiti input/body,
  response mapping e test del trasporto vanno integrati nei blocchi autorizzati futuri.
- Chiave HMAC server casuale >=32 byte, stabile/condivisa fra istanze e fuori repo/log
  richiesta all'integrazione; nei test solo memoria. Gestione/rotazione e hosting
  non attivati implicitamente; nessun `.env` creato.
- Rate a finestra fissa non è sliding window; soglie/NAT, timing, carico e CPU hashing
  richiedono verifica sull'hosting scelto prima di beta. Nessuna garanzia anti-DoS assoluta.
- Corpus comune/compromesso noto limitato e storico: copertura e aggiornamento da
  riesaminare prima di utenti reali. Nessun servizio di reputazione acquistato o password
  inviata in rete. Non dichiarare che una password assente sia "non compromessa".
- ID-03/04/05, audit segregato/retention e gate privacy non completati. Nessuna attestazione
  NIS2/GDPR, nessuna deroga auth introdotta o coperta da SEC-EX-001.
- Frontend e workflow invariati: gate npm non rieseguiti qui. Debt ESLint/npm rimane
  quello dell'audit del 19 ottobre precedente al blocco, riesame entro 30 ottobre o prima.
- **Nessun commit/push né nuova run GitHub**: le run 39210403963/39210620565 verificano
  la baseline pubblicata fino a S1-003, non queste modifiche locali.

## S1-004B — Conferma email e recupero password, 2026-10-19

L'utente ha autorizzato esclusivamente B, preview prima del codice e pubblicazione
GitHub dopo completamento/verifiche. La frase finale "una volta completato, pubblica"
è stata esplicitata come autorizzazione post-gate, non durante l'implementazione.
La pubblicazione include anche A già locale, necessario a B; nessun task C/D/UI.
Stato: componenti interni completati e pubblicati; CI GitHub verificata verde.

### Implementazione

- Provider Identity/Data Protection distinti per conferma **24h** e reset **30min**;
  nessun AddDefaultTokenProviders, cookie, SignInManager, MFA o sessione.
- Generazione/verifica e modifica account affidate a UserManager, non token/hashing
  artigianali. Keyring esplicito fornito dal chiamante; soltanto effimero nei test.
- Solo ultimo link valido per utente/scopo: digest del token opaco in UserTokens
  esistente, nessun token persistito in chiaro e nessuna migration/tavola nuova.
- Lock/transazione sulla propria riga Users serializzano reissue/consumo; conferma
  e reset una sola volta, reset aggiorna password/SecurityStamp atomicamente al consumo.
- Email non verificata impedisce reset; account inattivi non recuperabili da questi
  flussi. Password policy A applicata, nessuna riattivazione o sblocco lockout implicito.
- Sender richiesto esplicitamente, in memoria solo nei test. Risposte request equivalenti
  per account assente/esistente; nessun token/UserId nella risposta. Guasto sender
  mascherato con log sintetico, non si dichiara consegna email garantita.
- Quote Redis distinte: request 60/min globale, 20/min origine, 3/15min account;
  consume 120/min globale, 60/min origine, 5/15min account. RetryAfter recovery
  conservativo 900s; A conserva quote e retry originali.
- Link solo da origine HTTPS server, niente email nella query, return path locale
  canonico; schemi/host arbitrari, backslash, escape, query e fragment rifiutati.
- Code limitato e decodificato senza dettagli nei rifiuti; messaggi senza ToString
  sensibile; eventi strutturati con correlation ID e test log su valori/token/link.

[Design, operazioni e limiti B](account-recovery-baseline.md).

### File e dipendenze

20 file toccati da B, di cui 10 nuovi; insieme ad A la pubblicazione prevede 34 file.

| Area | File |
| --- | --- |
| AccountRecovery nuovi | AccountRecoveryRegistration.cs, AccountRecoveryService.cs, IAccountRecoverySender.cs, RecoveryLinks.cs, RecoveryTokenProviders.cs |
| AccountSecurity | AccountRateOperation.cs nuovo; AccountRateLimiter.cs e AccountOperationResult.cs aggiornati mantenendo compatibilità A |
| Progetto | Identity.Persistence.csproj: FrameworkReference Microsoft.AspNetCore.App, nessun nuovo NuGet B |
| Test | AccountRecoveryTests.cs, RecoveryLinksTests.cs, InMemoryRecoverySender.cs nuovi; AccountSecurityFixture.cs estesa con opt-in recovery, A resta senza provider |
| Documenti | account-recovery-baseline.md nuovo; README, identity-security-policy, backlog, questo report, memory e handoff aggiornati |

Versioni invariate: Identity/EF 10.0.12, Npgsql/provider 10.0.3, Redis 3.3.1 già aggiunto
in A e già previsto nello stack. SDK locale 10.0.402/runtime 10.0.13; immagini Compose
pgvector/pgvector:0.8.6-pg17 e redis:7.2.16-alpine3.21, root Compose invariato.
Nessun riferimento host → adapter aggiunto, nessun avvio automatico delle migrazioni.

### Gate locali, distinti dall'esecuzione GitHub

- Build Release: 0 warning/errori; format check verde.
- Suite con PostgreSQL/Redis reali: **112 pass, 0 fail, 0 skipped**;
  39 unit + 6 architecture + 67 integration. I 82 casi A conservati più 30 nuovi B
  (17 senza infrastruttura, 13 opt-in). Prima prova a 111 casi prima dell'ultimo test quote.
- Scadenze provate con timestamp standard protetti nel passato, solo fixture: 23h/25h
  per conferma, 29min/31min per reset; purpose/utente/keyring errati e tampering rifiutati.
- Reissue concorrente lascia un solo marker corrente; reset concorrenti hanno un
  solo vincitore. Replay, vecchio link, cambio password, policy password non conforme,
  richieste generiche/quote e guasti sender/DB/Redis verificati.
- Log applicativi/framework, eccezioni e scope catturati: nessun email/password/hash,
  SecurityStamp, bearer URL/code o connessione rilevati; nessuna stampa token.
- Snapshot EF Identity invariato, nessuna migration necessaria.
- 7 test environment loader superati.
- Frontend: npm ci, lint, type-check e production build PASS, file frontend invariati.
- NuGet advisory scan incluse transitive: nessuna segnalazione dalle fonti correnti.
- npm audit: stessa catena 5 high dev (GHSA-vfj7-8cjw-p6xm), 0 critical;
  --omit=dev 0 segnalazioni. ESLint 9 deprecato e limiti peer già documentati, non risolti.

Solo database/namespace e Compose temporanei con credenziali casuali in memoria;
risorse di verifica rimosse, volumi esistenti e `.env` non alterati.
Gitleaks 8.30.1 sui 34 file A+B candidati, in copia temporanea con output redatto:
nessun leak; whitespace e link locali PASS. Nessun gate/advisory nascosto o forzato.

### Pubblicazione ed evidenza GitHub — 2026-10-19

Commit applicativo A+B: `ca7edf4c156901a47a09fff36d15570799ffe652`, 34 file.
Gitleaks 8.30.1 anche sul commit effettivo prima del push: nessun leak rilevato.
Push su origin/main completato senza force; repository pulito/allineato dopo il push.

[RAMAI CI — run 39230830575](https://github.com/maffeinet/ramai/actions/runs/39230830575)
verifica esattamente quel commit: **completed / success**, job backend e frontend verdi.
Log GitHub verificati: restore/build Release (0 warning, 0 errori), format check,
**112 test backend: 39 unit + 6 architecture + 67 integration, 0 fail/0 skipped**,
con infrastruttura PostgreSQL/Redis; npm ci, lint, type-check e production build verdi.
Anche npm ci su GitHub segnala ESLint 9 deprecato e 5 vulnerabilità high già note:
non risolte dalla CI verde, SEC-EX-001 resta limitata allo sviluppo interno.
Secret scan, audit NuGet e 7 test loader sopra sono verifiche locali, non nuovi gate CI.
Questo aggiornamento documentale registra la run del codice: il suo commit successivo
non è il commit applicativo verificato da quella run. Nessun task C/D iniziato.

### Limiti e prossimo passo

Program.cs non collega il componente: nessuna route/pagina dei link, API di token,
login, sessione, MFA o UI. Invio SMTP/provider email, keyring persistente protetto,
delivery/retention affidabili, body/HTTP limit, referrer/access log e proxy sono gate
da implementare/verificare separatamente, non readiness commerciale implicita.
Invio post-commit senza outbox non è atomicità DB/email; retry/consegna non garantiti.
Soglie/NAT/timing e DoS restano da tarare prima della beta, senza claim constant-time.
SecurityStamp cambiato non dimostra revoca immediata sessioni: S1-004C dovrà provarla.
SEC-EX-001 non prorogata né estesa ad auth. S1-004 complessivo ancora aperto.
**Prossimo blocco da autorizzare: S1-004C — login/logout, cookie/sessioni, CSRF e revoca**;
non iniziato da questa richiesta. S1-004D e S1-005+ non iniziati.

## Riesame vulnerabilità frontend dopo S1-004B — 2026-10-19

Richiesta di correzione esaminata, nessuna feature aggiunta. Repository inizialmente
pulito sul commit documentale `79996b4`. Esito: **nessuna correzione compatibile
dimostrata**, non dichiarare il debito risolto.

- Audit attuale: 5 high dev sulla sola catena Next/fast-glob/micromatch/braces,
  0 critical. Registry braces latest 3.0.3, nessuna versione corretta pubblicata;
  [advisory upstream](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) senza patch.
- Prova isolata ESLint 10.13.0 + eslint-config-next 16.4.2 con strict-peer-deps:
  ERESOLVE, plugin import 2.32.0 esclude ESLint 10; anche react/jsx-a11y non lo supportano.
- Seconda prova isolata, config Next 16.4.2 con ESLint 9: lock risolvibile, ma audit
  ancora 5 high. Solo prove package-lock, script disabilitati, nessun gate attribuito
  a queste versioni candidate e nessuna modifica trasferita al frontend.
- Nessun audit fix --force, override, soppressione o rimozione di regole.
- Gate locali ripetuti sulla baseline invariata: npm ci --strict-peer-deps, lint,
  type-check e production build PASS; npm audit --omit=dev 0 segnalazioni.
  Audit completo non verde: 5 high; warning deprecazione ESLint 9 confermato.

File documentali aggiornati: security-exception.md, questo report, memory.md e
handoff.md. Manifest, lockfile, codice e workflow invariati. Nessun commit/push o
nuova run GitHub eseguiti per questo riesame. SEC-EX-001 non prorogata: riesame entro
30 ottobre/fine Sprint 1/prima di clienti o dati reali, al primo evento.
Prossima scelta sul debito: attendere patch upstream entro le condizioni approvate,
oppure autorizzare analisi separata di sostituzione/fork del tooling con copertura
equivalente; non cambiare framework per nascondere l'audit. S1-004C non iniziato.

## Analisi alternativa tooling frontend — 2026-10-19

Autorizzata esclusivamente analisi di una strada alternativa con controlli equivalenti.
[Report completo](frontend-tooling-analysis.md): inventario reale 86 regole TS/TSX,
confronto Oxlint 1.88.0/Biome 2.5.15 e prototipo ibrido con ESLint 10.13.0,
typescript-eslint 8.71.2 e Hooks 7.1.1; stack runtime invariato.

17 casi sintetici (14 negativi, 3 positivi): baseline rileva 14/14; Oxlint e ibrido
12/14, Biome 9/14. Ibrido senza la catena braces nel lock di laboratorio, audit 0,
npm install e npm ci strict/ignore-scripts, lint, type-check e build PASS sulla
copia dell'app. Warning root Next da lockfile annidati del laboratorio, documentato.
Restano due gap diagnostici osservati: React no-deprecated e Next navigazione
relativa; gestione JSX e mapping/opzioni restanti da verificare esaustivamente.

Esito: candidato fattibile, **non adottare senza conformità dimostrata**. Creato
frontend-tooling-analysis.md e aggiornati security-exception, memory, handoff e questo
report. Nessun manifest/lock/config lint/workflow/codice del progetto modificato.
Solo prove Windows locali; nessuna nuova CI, commit/push o task applicativo.
Debito reale e condizioni SEC-EX-001 invariati. Proposta di migrazione da autorizzare
separatamente con criterio di arresto se perde controlli. S1-004C non iniziato.

## Prototipo conformità e chiusura gap osservati — 2026-10-19

Autorizzato esclusivamente il laboratorio, non la sostituzione del tooling e non
S1-004C. Creato tooling/frontend-conformance con manifest/lock isolati, inventario,
config in memoria, bridge MIT di quattro regole, provenienza/hash, test e README.
[Report tecnico aggiornato](frontend-tooling-analysis.md),
[comandi e limiti](../tooling/frontend-conformance/README.md).

Gap osservati Next/React e riferimenti JSX recuperati nei test. Test aggiuntivi hanno
scoperto API rimosse in ESLint 10 (getFilename/JSDoc): due helper adattati e coperti,
senza riscrivere il peer range dei vecchi plugin o introdurre la catena vulnerabile.

- Laboratorio: npm install/npm ci strict e ignore-scripts PASS; lint PASS.
- **173 test del prototipo superati, nessuno fallito/skipped**: 27 full preset,
  34 Next, 88 fixture upstream, 18 edge case, 5 inventario/integrità e 1 isolamento.
- Audit completo del lock laboratorio: 0 segnalazioni note, nessun ESLint 9 o
  braces/fast-glob/micromatch/preset Next nell'albero candidato.
- Mapping 86 TS/TSX e 82 JS/MJS con opzioni/severità conservate; non equivalenza
  esaustiva delle implementazioni native o degli autofix. readyForAdoption=false.
- Frontend originale: lint, type-check, production build PASS; audit production 0.
  Manifest, lockfile, config lint, codice applicativo e CI invariati.

Nessuna nuova prova backend/Linux/GitHub/clone pulito. Comparatore con baseline
esterna ancora sotto SEC-EX-001; audit del laboratorio non bonifica l'oracolo.
Le 5 high dev e ESLint 9 restano nel progetto attivo. Nessuna proroga/chiusura del
debito, commit/push, S1-004C o altro task applicativo. Aggiornati anche memory,
handoff, security-exception e analisi, preservando le modifiche documentali preesistenti.
Prossimo passo separato: review/estensione conformità e validazione isolata Windows/Linux
prima di approvare una futura migrazione e la manutenzione dei bridge.

Controlli finali: Gitleaks 8.30.1 su 45 file candidati senza leak, link e whitespace
verificati; nessuna diff in src/ o workflow. Audit completo baseline riconfermato
5 high/0 critical sulla stessa advisory. 40 nuovi file nel solo laboratorio,
5 documenti aggiornati inclusa l'analisi già locale; nessun contenuto precedente perso.

## Review estesa e ripresa notturna — 2026-10-20

[Review completa](frontend-tooling-review.md). Copertura aggiuntiva e cloni puliti
Windows/Linux del 19 ottobre hanno dato 221 casi: 219 superati, 2 falliti, 0 skipped.
Lint/install/audit laboratorio e gate frontend baseline verdi su entrambi i sistemi;
non è CI GitHub. Corretto il noexec della prima prova Linux e ripetuta la verifica.
Il checkout aggiornato, con un ulteriore test di bootstrap, dà 222: 220 pass/2 fail.

Restano react/display-name su memo anonimo e no-duplicate-head su Document funzionale.
Il laboratorio rimane bloccato, senza suppress/skip. Rafforzati guardia contro cicli
Next, integrità vendor, report fail-closed e riproduzione con snapshot esplicito.
L'ultimo bootstrap aggiornato necessita nuova prova in clone. Tooling attivo invariato.

Utente autorizza riprese notturne e pubblicazione GitHub delle modifiche pertinenti
dopo quality gate/secret scan verdi. Programmate riprese orarie limitate alla notte
e punto alle 09:00 del 20 ottobre Europe/Rome; non garantita esecuzione in assenza
di PC/app, permessi o capacità. Nessun nuovo commit/push al momento, nessuna
migrazione o S1-004C. SEC-EX-001 aperta senza proroga.

### Ripresa 02:18 — laboratorio verde, migrazione non approvata

370/370 test, zero skipped, in locale e nuovi cloni puliti Windows/Linux.
display-name originale upstream con chiusura vendor motivata nella
[review](frontend-tooling-review.md): 14 sorgenti aggiunti invariati, hash/MIT;
sei dipendenze helper esatte e Hermes 0.37.0 solo per la fixture Flow upstream.
109 fixture upstream (anche opzioni/messageId/posizioni), 24 casi wrapper nuovi,
isolamento bridge rafforzato. Manifest/lock soltanto del laboratorio aggiornati.
Install strict/ignore-scripts, lint e audit lab PASS, zero advisory note;
install/lint/type-check/build e audit production baseline PASS nei due cloni.
Backend Release 0 warning/errori, format PASS, 112 test PASS/0 skipped con
infrastruttura temporanea reale, poi rimossa senza toccare dati utente.
Report, memory, handoff, eccezione, README laboratorio e provenienza aggiornati.
Pubblicato bcaca48d3afcceff06af553889d76b1f9dd726a8, 70 file pertinenti,
Gitleaks 8.30.1 senza leak e whitespace/link PASS. Run reale
[39347649384](https://github.com/maffeinet/ramai/actions/runs/39347649384) success,
backend/frontend, 112 test/0 skipped. Pipeline attiva non esegue il lab.
Cloni del commit pubblicato ripetuti: 370/370 Windows/Linux e tutti i gate verdi;
evidenze e limiti nella review. Questo aggiornamento documentale è successivo
al commit testato dalla run citata; nessuna estensione implicita di quell'evidenza.
SEC-EX-001 aperta, nessuna migrazione o S1-004C. I risultati sotto sono storici.

### Ripresa 01:19 — Next chiuso, React ancora non equivalente

Modifiche solo al laboratorio e documentazione: no-duplicate-head upstream 16.3.8
aggiunto al vendor/inventario/bridge con hash originale e nessuna nuova dipendenza;
sette test di scope e molteplicità. Otto test display-name aggiuntivi conservano
le evidenze del confronto, incluso forwardRef anonimo non segnalato dal nativo.
Lint e audit laboratorio verdi. Suite locale e nuovi cloni puliti Windows/Linux:
**237 casi ciascuno, 235 pass/2 fail/0 skipped**, fallimenti memo/forwardRef anonimi.
Install/lint/audit laboratorio e install/lint/type-check/build/audit production
frontend originali verdi su entrambi. Lo script di clone termina con errore per
la conformità rossa, non mascherata. Backend non rieseguito.

React-x 5.28.2 provato in artifacts isolato con install strict/ignore-scripts e
audit 0: non segnala i due export anonimi e sommato al nativo duplica il caso
memo assegnato a variabile. Non aggiunto al manifest/lock laboratorio o app.
Chiusura vendor React ampia non introdotta. [Dettagli e riproduzione](frontend-tooling-review.md).
Nessun commit/push o nuova run GitHub; sorgenti/workflow attivi invariati.
SEC-EX-001 aperta senza proroga, nessuna migrazione o S1-004C.
Scansione Gitleaks 8.30.1 dei 54 file candidati senza leak; git diff --check PASS.
