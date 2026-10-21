# RAMAI — Sprint 1 proposto: Identity, Tenant e DB baseline

Data piano: 2026-10-16. Aggiornamento: 2026-10-20.
Checkpoint pubblicazione 21 ottobre: C/D e S1-005/006/007/008 pubblicati con
autorizzazione esplicita nel commit `06f054d`;
[CI reale verde](https://github.com/maffeinet/ramai/actions/runs/39564651224),
278 test/0 skipped e gate backend/frontend PASS. S1-009/UI non iniziato; nessuna
beta o chiusura complessiva Sprint 1. Le note locali sotto sono checkpoint storici.
Stato: **S1-001 COMPLETATO; S1-002/003 PUBBLICATI; S1-004A/B COMPLETATI E PUBBLICATI; S1-004 APERTO**.
Aggiornamento 20 ottobre: **S1-004C implementato localmente**, non pubblicato;
S1-004D MFA ora implementato localmente, verifica finale nel report; UI e isolamento
restano da implementare, non è completato l'intero accesso sicuro.
Ultima pubblicazione applicativa autorizzata del 19 ottobre, commit `ca7edf4`;
[CI verde con 112 test senza skipped](https://github.com/maffeinet/ramai/actions/runs/39230830575).
Le sezioni dei singoli task mantengono lo storico delle implementazioni locali.
Questo piano non modifica AGENTS.md e non autorizza feature fuori Sprint 0.

## Obiettivo

Preparare una prima verticale interna: utente autenticato, workspace aziendale e
operazioni minime protette, con isolamento tra aziende dimostrato da test reali.
Non è ancora una release commerciale o una beta con dati cliente.

## Fonti e coerenza

- `reference_docs/04_Technical_Architecture_v1.0.pdf`: sezioni 4, 12, 14, 19.1 e 23;
  Sprint 1 previsto come Identity + Tenant + DB baseline; EF Core raccomandato.
- `reference_docs/08_Security_GDPR_AI_Act_v1.0.pdf`: sezioni 2–4, 10–11 e 14;
  deny by default, ruoli, sessioni, audit e gate umani.
- `reference_docs/11_Codex_Development_Specification_v1.0.pdf`: sezioni 4–12 e 17;
  migration/test, piccoli task, review security e confini di modulo.
- `reference_docs/12_MVP_Test_Plan_v1.0.pdf`: sezioni 3–5 e 14;
  account/workspace, test IDOR, brute force e critical path.
- [AGENTS.md](../AGENTS.md), [ADR](../docs/adr/),
  [DoD Sprint 0](../docs/definition-of-done.md) e struttura repository corrente.

## Prerequisiti e decisioni umane

1. M0 chiuso il 16 ottobre con [SEC-EX-001](../docs/security-exception.md) accettata
   temporaneamente per sviluppo interno; rispettare riesame entro il 30 ottobre o prima.
   La decisione non approva integralmente questo backlog o l'ADR del security model.
2. Approvare questo scope di pianificazione e poi il singolo task da implementare.
3. ADR-009 e policy tecnica approvati il 17 ottobre: identità locale standard,
   cookie first-party, senza CIAM obbligatorio o server token/password hashing artigianali.
4. Implementare e verificare cookie/CSRF, keyring, revoca, HTTPS, email/reset nei task
   autorizzati; canale email reale, deployment e nuove risorse restano da scegliere
   e approvare prima del loro uso. La policy approvata non attiva servizi esterni.
5. Modello approvato: User globale, Tenant e membership con ruolo per azienda; uno
   workspace attivo autorizzato. Confini persistence concreti e contratti in S1-002/003.

L'approvazione conclude S1-001 documentale, non esegue S1-002+. Gli altri task non
possono inventare servizi o deroghe mancanti. Ogni nuova dipendenza infrastrutturale
richiede ADR e approvazione secondo AGENTS.md.

## Scope ed esclusioni

In scope proposto: Identity/Tenants, contratti minimi tra moduli, PostgreSQL/EF Core
con migrazioni, sessioni, TenantContext, policy autorizzative, web flow minimo e test.
User globale versus entità tenant-scoped deve essere motivato; TenantId obbligatorio
su ogni entità tenant-scoped, non aggiunto meccanicamente alle identità globali.

Fuori scope: connector/Microsoft OAuth per Mail o Calendar, Graph, Gmail, ingestion,
Business Memory, AI, insight, billing/pagamenti, worker/queue/cloud aggiuntivi, deploy
pubblico, inviti email, amministrazione avanzata ruoli e tenant delete/purge.
Non creare una finta entità Business Memory per provare i query filter: usare dati
tenant-scoped reali del modello membership/settings approvato.

## Task proposti, ordine di dipendenza

| Task | Risultato | Dipende da |
| --- | --- | --- |
| S1-001 | Decisione auth/tenancy e threat model minimo | Gate umani sopra |
| S1-002 | Modello Identity/Tenants e application contracts | 001 approvato |
| S1-003 | Persistence e migrazioni iniziali PostgreSQL | 002 |
| S1-004 | Autenticazione/sessioni secondo ADR | 001, 003 |
| S1-005 | TenantContext fail-closed da identità/membership | 002, 004 |
| S1-006 | Provisioning workspace e primo Owner | 003, 004, 005 |
| S1-007 | Isolamento letture/scritture tenant-scoped | 003, 005, 006 |
| S1-008 | API minime e RBAC server-side | 004–007 |
| S1-009 | UI login/workspace/area protetta/logout | 008 |
| S1-010 | Suite security trasversale e percorso E2E | 004–009 |
| S1-011 | Gate CI sicurezza ed eccezione puntuale | 001; integrato con 010 |
| S1-012 | Bootstrap aggiornato, review e handoff | Tutti i precedenti |

S1-011 va preparato presto insieme a 001, non lasciato interamente alla fine.
Ogni task include da subito i propri test; 010 completa la suite, non rinvia i test.
Se un task supera una feature verticale piccola, dividerlo prima dell'implementazione.

### S1-001 — Decisioni e threat model

Analisi documentale autorizzata il 16 ottobre; parametri tecnici approvati il 17 ottobre
con risposta "ok parametri tecnici S1-001 approvati". **Task documentale completato**.
Output: [ADR-009 Accepted nel perimetro locale](../docs/adr/ADR-009-identity-access.md)
e [baseline ID-01–07 approvata](../docs/identity-security-policy.md).
Nessuna implementazione o migration svolta/approvata in blocco.
Input confermati: modello SaaS e fino a 500 MAU iniziali. L'utente chiede identità
locale per validare il mercato senza CIAM esterno obbligatorio. Proposti ASP.NET Core
Identity, PostgreSQL/EF Core e cookie web protetti nel monolite, non un token server
artigianale. Parametri sessioni/revoca, password policy ed email/reset approvati;
implementazione e taratura dei dettagli affidate ai rispettivi task.
Account con qualsiasi email verificata; social login futuro e connector separati.
La raccomandazione managed precedente non approvata è superata per l'MVP.

Output completato: ADR locale e policy approvati, flussi trust-boundary e threat model;
questioni normative/operative future esplicitamente separate dai parametri tecnici.
Acceptance: approvazione umana modello auth/tenancy; gestione credenziali/sessioni,
CSRF e lifecycle definite; nessun servizio esterno o protocollo scelto implicitamente.
Verifica: review documentale/security. Nessun codice o migration in questo task.

Indirizzo utente: policy con riferimenti NIS2/GDPR. Output aggiunto:
[baseline identità/security/privacy](../docs/identity-security-policy.md),
ID-01–07 approvati tecnicamente, non attestazione di conformità. Numeri password/sessione sono scelte RAMAI,
non obblighi inventati della normativa; applicability NIS2 e governance da validare.

### S1-002 — Modello e contratti

Stato: **COMPLETATO LOCALMENTE il 17 ottobre**, dopo istruzione esplicita di eseguire
solo S1-002. [Modello/ownership](../docs/identity-tenant-model.md) e
[verifiche/limiti](../docs/sprint1-progress.md) documentati. Modelli e reader contracts
senza persistence o auth; 49 test pass e 1 infrastrutturale skipped, format/build verdi.

Output: utenti, tenant, membership e ruoli definiti secondo ADR; ownership tabelle
e application contracts espliciti. Identity non legge tabelle private Tenants e viceversa.
Acceptance: invarianti, unicità, membership e bootstrap Owner definiti e testati;
nessun campo tenant-scoped senza TenantId. Unit e architecture test aggiornati,
senza eliminare i test di confine esistenti solo per rendere verde la suite.

Indirizzo di prodotto confermato dall'utente su single/multi user e mono/multi azienda:
User globale + Tenant aziendale + Membership molti-a-molti con ruolo per azienda.
Single-user usa lo stesso modello, non un percorso che salta il TenantId. Mono-azienda
non cambia la natura multi-tenant della piattaforma SaaS. Aggiungere test per stesso
utente Owner in A/Member in B e isolamento di un secondo utente senza accesso ad A.
"Team" indica collaborazione nello stesso workspace; gruppi/reparti esclusi dal primo
blocco, nessuna entità Team. Modello multi-azienda predisposto; scope UI da definire.
Limiti commerciali e billing non fanno parte di questa modellazione.

M0/SEC-EX-001 e S1-001 sono stati approvati nei limiti documentati; S1-002 è stato
poi autorizzato e implementato esclusivamente come modello/contratti. Vincoli DB e
prove con PostgreSQL restano in S1-003; non iniziati qui. Nessuna migration eseguita.

### S1-003 — Database baseline

Stato: **COMPLETATO LOCALMENTE il 18 ottobre**, dopo richiesta di prosecuzione e
conferma Docker disponibile. Adapter EF separati per modulo, migrazioni/snapshot,
reader e test PostgreSQL reali. Nessuna migrazione sui dati/volumi preesistenti;
nessun login o task successivo. [Procedure e limiti](../docs/database-migrations.md),
[risultati](../docs/sprint1-progress.md). Nessun commit/push.

Output: DbContext/EF Core e migrazioni dei soli moduli autorizzati; versioni NuGet
motivate e coerenti con .NET 10/Npgsql. Layout per modulo scelto esplicitamente.
Acceptance: migrazione su DB vuoto e aggiornamento della Foundation senza perdere
pgvector o dati; recovery verificata in ambiente di test; nessun reset di volumi.
Test: integration PostgreSQL reale, vincoli membership/TenantId e architecture.

### S1-004 — Autenticazione e sessione

Suddivisione concordata, con autorizzazione separata per ciascun blocco:

- **S1-004A completato localmente il 19 ottobre**: account/password, validator NFC,
  denylist locale con provenienza, Identity hash/rehash, lockout e rate limit Redis
  interni, test reali e log minimizzati. [Dettagli](../docs/account-security-baseline.md)
  e [quality gate](../docs/sprint1-progress.md). Nessun endpoint o sessione.
- **S1-004B completato il 19 ottobre**: conferma/reset interni con token Identity
  purpose-bound 24h/30min, solo ultimo link valido e consumo atomico, sender in memoria
  nei test. [Dettagli](../docs/account-recovery-baseline.md). Nessun endpoint o email reale.
- S1-004C: login/logout e sessioni/cookie/CSRF/revoca implementati localmente il
  20 ottobre, opt-in con keyring protetto, nessun push. [Limiti e test](../docs/account-session-baseline.md).
- S1-004D: MFA TOTP/enrollment, recovery monouso, cifratura/replay/lock e revoca
  implementati localmente il 20 ottobre dopo autorizzazione esplicita. [Baseline D](../docs/account-mfa-baseline.md).
  Verifiche nel report; niente commit/push. Obbligo MFA per ruolo ancora da integrare.

Il completamento A/B non completa il trasporto HTTP o l'intero S1-004. Alla fine di B
l'utente ha autorizzato commit/push A+B e controllo GitHub; non durante implementazione.
S1-005+ non iniziati.

Output dipendente dall'ADR: login/logout, scadenza e revoca, provisioning credenziali
o callback IdP. Token sensibili non in localStorage, URL o log.
Acceptance: identità valida verificata server-side; fallimenti generici senza account
enumeration; protezioni brute force, cookie Secure/HttpOnly e CSRF se cookie-based;
logout/revoca verificabili. Se auth locale, includere hash standard e lifecycle reset
con decisione esplicita, senza email di produzione o credenziali di test persistenti.
Test: credenziali errate, sessione scaduta/revocata/manomessa, rate limit, redazione log.

Usare ID-01–05 della baseline approvata come criteri da implementare e dimostrare.
Separare in sottotask password/lockout, email/reset, cookie/revoca e MFA se necessario;
non dichiarare completato il lifecycle solo perché il login funziona. Sender test in
memoria per dati sintetici; email reale e MFA privilegiati sono gate prima della beta.

### S1-005 — TenantContext

**COMPLETATO LOCALMENTE nel perimetro interno il 20 ottobre**, dopo autorizzazione
esplicita. 178 test backend verdi/0 skipped, build/format e gate frontend PASS.
[Baseline e confini](../docs/tenant-context-baseline.md); nessun endpoint workspace
o nuovo riferimento API→Tenants. Composition/HTTP da integrare in S1-008 con review.
Nessun commit/push; S1-006+ non iniziati.

Output: contratto del contesto richiesto dalle operazioni tenant-scoped.
Acceptance: identità/membership inattiva o contesto assente rifiutati; header, query,
route o body arbitrari non possono assegnare il tenant. Eventuale selezione workspace
verificata contro membership server-side e sessione aggiornata secondo ADR.
Test: claim/tenant manomessi, appartenenza assente, contesti concorrenti distinti.

### S1-006 — Provisioning workspace

**COMPLETATO LOCALMENTE nel perimetro interno il 20 ottobre**, dopo autorizzazione
esplicita. 201 test backend verdi/0 skipped; build/format/frontend e modello EF PASS.
[Baseline e migrazione](../docs/workspace-provisioning-baseline.md). Nessun nuovo
endpoint/wiring API→Tenants o S1-007+; nessun commit/push.

Output: creazione controllata tenant e prima membership Owner dal soggetto autenticato.
Acceptance: atomicità/coerenza secondo confini dei moduli approvati, retry non duplica
workspace, nessuna possibilità di autoproclamarsi Owner di un tenant esistente.
Test: successo, duplicato/retry, fallimento parziale, tentativo su tenant altrui.

### S1-007 — Isolamento dati

**COMPLETATO LOCALMENTE il 20 ottobre**, 222 test verdi/0 skipped, Release/format/
frontend/modello EF PASS. [Baseline e limiti](../docs/tenant-isolation-baseline.md).
Autorizzati pubblicazione dopo gate/scan e successivo S1-008 nella notte 20/21;
UI/S1-009 non autorizzata. Esiti effettivi di scan/push/CI nel report.

Output: query filter e protezioni scrittura per i dati tenant-scoped reali dello scope.
Acceptance: letture, liste, update/delete applicabili non attraversano tenant; mismatch
su scrittura rifiutato, non solo filtro sulle letture. Nessun bypass raw SQL o filtro
disabilitato senza review. Non introdurre RLS o un nuovo tenancy model implicitamente.
Test: tenant A/B, ID validi dell'altro tenant, lista vuota, update, contesto mancante.

### S1-008 — API e autorizzazioni

**COMPLETATO E PUBBLICATO il 21 ottobre nel perimetro API/RBAC**, dopo approvazione
dei sospesi ADR-010/pubblicazione. [Baseline](../docs/workspace-api-baseline.md),
278 test totali verdi/0 skipped, Release/format/frontend/EF PASS e secret scan senza
leak. Commit/push/CI effettivi nel report (06f054d, run 39564651224 success);
nessuna UI/S1-009 o beta. Sotto: checkpoint
interno precedente al completamento HTTP.

**AVVIATO, PARZIALE INTERNO**, ripresa notturna 20 ottobre: contratti, servizio RBAC,
adapter TenantDataContext e 28 test nuovi; 250 test totali verdi/0 skipped.
[Piano/decisioni host](../docs/workspace-api-plan.md). Nessun nuovo endpoint/wiring
HTTP: attende conferma estensione host-only ADR-010 e policy trasporto. Non Done;
UI/S1-009 non iniziata, nessun nuovo commit/push.

Output: contratti versionati /api/v1 per profilo, workspace e settings minimi approvati;
ProblemDetails/correlation ID ed errori coerenti. Owner/Admin/Member lato server.
Acceptance: 401 senza sessione, 403 per ruolo insufficiente; risorsa di altro tenant
non divulgata secondo policy 403/404 approvata. Nessun campo arbitrario per ruolo o
TenantId accettato tramite mass assignment. Mutazioni solo con protezioni del modello auth.
Test: HTTP integration/contract, matrice ruoli, CSRF ove applicabile, IDOR e input invalidi.

### S1-009 — UI verticale minima

Output: login, provisioning/selezione workspace se approvata, area protetta, logout.
Acceptance: UI gestisce 401/403/scadenza/rete indisponibile senza esporre token;
guard UI non sostituisce authz API. Nessuna dashboard business fittizia o feature extra.
Test: component test ed E2E del percorso critico con dati sintetici, lint/tipi/build.

### S1-010 — Security regression

Output: suite cross-tenant bloccante e test sessione/autorizzazione/IDOR/leak log;
percorso utente reale minimo, non soltanto mock dell'autenticazione.
Acceptance: tentativi tenant A → B negati su tutti gli endpoint dello scope; nessun
test saltato in CI; isolamento dimostrato con PostgreSQL reale e ID validi.
Review umana delle aree security prima di merge. Nessun dato reale nei fixture.

### S1-011 — CI e debt

Output proposto: scan dipendenze, secret scan e test PowerShell/security pertinenti;
branch protection/required checks solo con specifica autorizzazione repository.
Acceptance: nuove violazioni non ignorate; SEC-EX-001 identificata puntualmente e con
scadenza verificata se approvata, non generico audit-level che nasconde tutte le high.
Nessun token aggiuntivo hardcoded, privilegi minimi, scansioni e test negativi dei gate.
Le cinque segnalazioni restano visibili anche durante un'eventuale deroga valida.

### S1-012 — Review e passaggio di consegne

Output: README, contratti/migrazioni, report Sprint 1 e memory/handoff aggiornati.
Acceptance: clone pulito avviabile con soli dati sintetici; gate verdi con infrastruttura
reale; rischi e limiti documentati; security review e decisione debt registrate.
Nessuna dichiarazione di commercial readiness basata solo sulla CI verde.

## Ambiguità rilevate: da non risolvere con codice implicito

- I PDF 04/11 mostrano layout e backlog Foundation diversi dal repository e dal
  backlog Sprint 0 concordato. Conservare struttura corrente; non rinominare progetti
  o dichiarare automaticamente mancanti task Foundation sulla base della numerazione PDF.
- OIDC/OAuth e auth locale sono entrambi citati: serve ADR concreto, non JWT artigianali
  o un nuovo IdP assunto come già approvato. Microsoft login non implica consenso Mail/Calendar.
  ADR-009 ora propone cookie-only per il primo web con Identity locale come richiesto
  dall'utente. Non equivale a implementare OIDC: confermare perimetro prima del codice.
- Il PDF 04 menziona più moduli del set attuale: non aggiungerli in questo sprint.
- ADR-007 corrente copre Compose locale; cloud EU è target dei PDF, non deploy già pronto.
- Target backup dei PDF 04/08 differiscono (15 minuti versus baseline iniziale 24 ore):
  da decidere prima del go-live, fuori implementazione Sprint 1 proposta.
- Le feature account/reset/ruoli del test plan coprono l'MVP complessivo: non presumere
  che tutte le integrazioni operative debbano entrare in questa prima verticale.

## Exit gate proposto

Auth e tenant model approvati; schema/migrazioni testati; flusso interno login →
workspace → operazione autorizzata → logout funzionante; isolamento A/B dimostrato;
nessun secret o test security skipped; build/test/format/lint/tipi/build web verdi;
scan senza nuove violazioni non accettate; eccezione non scaduta se ancora necessaria;
documentazione e review aggiornate. Nessuna beta esterna o release commerciale implicita.
