# RAMAI — Memoria del progetto

Aggiornato: 2026-10-21, Europe/Rome.

## Ultimo aggiornamento operativo

21 ottobre: approvati i sospesi (ADR-010 host-only Tenants e pubblicazione
prerequisiti C/D/005/006/007 con S1-008). **S1-008 completato localmente**,
[baseline](workspace-api-baseline.md). API opt-in profilo/provisioning/selezione/
workspace/settings, contratti chiusi, ruolo/MFA dal contesto verificato, CSRF,
ProblemDetails/correlation, quota provisioning Redis distribuita e test HTTPS reali.
278 test verdi/0 skipped (101/169/8), Release 0 warning/errori, format/frontend/EF
PASS, Gitleaks staged 73 file senza leak. Audit 5 high dev/0 production riconfermato,
nessuna proroga. Risorse test proprie rimosse, volumi utente intatti. Pubblicato
`06f054d`, 73 file e scan del commit senza leak;
[CI 39564651224](https://github.com/maffeinet/ramai/actions/runs/39564651224) **success**,
SHA verificato, 278 test/0 skipped anche su GitHub-hosted Linux, build/format/frontend
verdi. Questa nota documentale successiva non è lo SHA testato da quella run.
S1-009/UI non iniziata,
nessuna esposizione esterna o completamento commerciale impliciti. Stato sotto storico.

Ripresa notturna 23:07: **S1-008 parziale interno**, [piano e decisione necessaria](workspace-api-plan.md).
Contratti/servizio RBAC/adapter tenant-scoped e 28 nuovi test; suite totale 250
PASS/0 skipped, Release/format/frontend/EF PASS. Endpoint/wiring HTTP non introdotti.
Gitleaks 65 file senza leak, link/whitespace PASS, Docker temporaneo S1-008 rimosso;
dati utente intatti. Nessuna nuova CI/Linux o modifica al tooling attivo.
Per continuare serve conferma estensione ADR-010 al composition root Tenants;
non registrata come già approvata. Distinta conferma di pubblicazione prerequisiti
ancora mancante. Nessun nuovo commit/push/CI, nessuna UI/S1-009 o altro task.

Notte 20/21 ottobre: S1-007 completato localmente, [baseline](tenant-isolation-baseline.md),
222 test verdi/0 skipped (79/135/8), Release/format/frontend e modello EF PASS.
Autorizzazione più recente: completare 007 e 008, documentare ogni step, commit/push
pertinenti dopo secret scan/gate e verifica CI reale. Inclusione prerequisiti locali
C/D/005/006 da confermare, non lavoro estraneo. Nessuna UI/S1-009, deploy o migrazione tooling.
Questo checkpoint precede lo step parziale S1-008 sopra; Gitleaks 59 file senza leak. Comando di pubblicazione
respinto prima dell'esecuzione per scope prerequisiti più ampio: conferma umana
richiesta, nessun nuovo commit/push/CI. Non aggirare la revisione. Risorse Docker
isolate S1-007 rimosse, volumi utente intatti.
I paragrafi sotto descrivono lo storico, non prevalgono su questa autorizzazione.

20 ottobre, blocco corrente: **S1-006 provisioning interno completato localmente**.
[Baseline](workspace-provisioning-baseline.md). Nuovo registro idempotente nello
schema Tenants, Owner da soggetto verificato con MFA, nessun client TenantId/Role.
Nessun nuovo servizio/NuGet, endpoint o wiring API; nessun commit/push/S1-007+.
Gate: 201 test verdi/0 skipped (79 unit, 115 integration, 7 architecture), Release
0 warning/errori, format e frontend PASS. EF modello/script PASS; Gitleaks 53 file
senza leak, link/whitespace PASS. Risorse Docker di test rimosse, volumi utente intatti.
La nuova migrazione non è stata applicata ai dati utente. Down rifiuta di perdere
registrazioni idempotenti già usate; nessun reset/retention automatica del registro.
Prossimo blocco da autorizzare S1-007, non avviato. Nessuna nuova CI per il locale.

20 ottobre, ripresa successiva: **S1-005 TenantContext interno completato localmente**.
[Baseline](tenant-context-baseline.md). Componenti Application attraverso contratti,
nessun nuovo endpoint/composition API→Tenants, nessuna nuova dipendenza/schema.
Membership/ruoli verificati ogni volta, niente tenant scelto implicitamente;
Owner/Admin richiedono prova MFA. C/D/demo locali preservati, nessun commit/push.
Gate: build Release 0 warning/errori, format e 178 test PASS/0 skipped (64 unit,
107 integration, 7 architecture), frontend PASS, Gitleaks 38 file senza leak.
Scope HTTP/DI e salvataggio selezione nel ticket rinviati esplicitamente a S1-008;
non dichiarare già disponibile una UI/route workspace o isolamento business.
Prossimo task da autorizzare S1-006, non iniziato. SEC-EX-001 invariata.

20 ottobre, ripresa: **S1-004D completato localmente**, [baseline MFA](account-mfa-baseline.md).
Release 0 warning/errori, format e 146 test PASS/0 skipped (39 unit, 100 integration,
7 architecture), incluso MFA Kestrel HTTPS reale. Frontend lint/tipi/build PASS;
Gitleaks 32 file senza leak e link/whitespace verificati.
Nessun nuovo NuGet/schema/provider, frontend invariato.
Nessun commit/push. C e piano demo locali preservati; isolamento/UI e beta ancora no.
Questo aggiornamento prevale sullo stato storico "D non iniziato" sottostante.
Prossimo blocco da autorizzare S1-005 TenantContext. Obbligo MFA Owner/Admin con
ruoli, isolamento e UI ancora da integrare; nessun accesso esterno autorizzato.

20 ottobre: S1-004C implementazione locale, [baseline sessioni](account-session-baseline.md),
ADR-010 composition root, nessun nuovo NuGet/servizio esterno, migrazione DB o push.
Gate locali: Release/format verdi, 127 test/0 skipped (anche Kestrel HTTPS reale),
frontend lint/tipi/build verdi; Gitleaks 26 file senza leak, link/whitespace PASS.
Cookie HTTPS/CSRF, ticket cifrati
Redis e validazione DB per richiesta. Nessun tenant grant o bypass MFA: account con
MFA attiva rifiutati fino a D. UI e D non iniziati. Demo target entro 30 novembre;
nome pubblico Agentilio, registrazione dominio affidata all'utente, non confermata.
RAMAI resta nome tecnico, nessuna rinomina o DNS modificato. Sotto: storico.

20 ottobre, dopo il punto stakeholder: utente richiede riallineamento alla roadmap
e prima demo utile. [Piano demo](demo-roadmap.md) e
[backlog](../tasks/DEMO_BACKLOG.md): accesso/workspace, dataset sintetico,
Today con evidenze, bozza senza invio, feedback. È una verticale proposta, non
funzionalità implementata o beta. Prossimo blocco S1-004C; provider/budget AI,
data presentazione e modalità di accesso da confermare. Nessun servizio esterno,
nuova feature o commit/push in questo aggiornamento. Tooling non migrato;
SEC-EX-001 resta aperta senza proroga. Priorità al prodotto, non altra ricerca lint.

20 ottobre, ripresa 02:18: **370/370 verdi, zero skipped** in locale e nuovi cloni
Windows/Linux. display-name upstream con chiusura vendor motivata nella
[review](frontend-tooling-review.md). Install/lint/audit lab, gate frontend e
backend Release/format/112 test verdi. Pubblicato bcaca48, CI reale
[39347649384](https://github.com/maffeinet/ramai/actions/runs/39347649384) verde;
cloni del commit pubblicato confermano 370/370. CI non esegue il laboratorio.
Questo aggiornamento documentale è successivo al commit verificato;
nessuna migrazione o S1-004C, SEC-EX-001 aperta. Sotto: storico dei gap ora colmati.

20 ottobre, 01:19: Next no-duplicate-head corretto nel laboratorio con sorgente
upstream identica e sette test. Suite estesa 237: 235 pass/2 fail/0 skipped:
restano export memo/forwardRef anonimi per display-name. React-x 5.28.2 testato
in isolamento non risolve i gap, non adottato. Nessuna migrazione/pubblicazione.
Evidenze correnti nella [review](frontend-tooling-review.md); le cifre sotto sono storiche.

20 ottobre, ripresa notturna: [review aggiornata](frontend-tooling-review.md).
Non usare il vecchio risultato 173 verde come approvazione: la copertura estesa
trova due differenze ancora rosse, confermate Windows/Linux; 222 casi locali,
220 pass/2 fail/0 skipped. Pubblicazioni autorizzate solo dopo gate e secret scan
verdi, nessuna migrazione o S1-004C. Riprese orarie e riepilogo delle 09:00 locali
programmati per il 20 ottobre; non promettere attività continua o puntualità garantita.
Prototipo conformità autorizzato dopo l'analisi, 19 ottobre: tooling/frontend-conformance,
173 test verdi, inventario 86/82 regole e audit del proprio lock 0 segnalazioni.
Gap osservati Next/React/JSX recuperati nei casi testati, con bridge MIT e due helper
adattati per ESLint 10. [Uso e limiti](../tooling/frontend-conformance/README.md).
Tooling applicativo NON sostituito; equivalenza esaustiva/Linux/CI e review ancora
da verificare, readyForAdoption=false. SEC-EX-001 aperta, nessun commit/push o S1-004C.

Analisi alternative tooling del 19 ottobre: [rapporto](frontend-tooling-analysis.md).
Prototipo isolato Oxlint + ESLint 10 con Hooks/TS supportati ha audit 0 e gate verdi
sulla copia app, ma perde due controlli diagnostici osservati. Nessuna sostituzione
applicata: prima test di conformità e gap chiusi, poi eventuale migrazione autorizzata.
SEC-EX-001 aperta, nessun cambio allo stack, nessun commit/push o nuovo task.

Riesame vulnerabilità successivo alla pubblicazione B, 19 ottobre: nessuna patch
braces pubblicata, 5 high dev confermate; ESLint 10.13.0/config Next 16.4.2 falliscono
strict-peer-deps in copia isolata. Config Next 16.4.2 con ESLint 9 risolve il lock ma
non le 5 high. Nessuna modifica al frontend, nessuna deroga prorogata o chiusa.
[Evidenze e alternative](security-exception.md). Nessun nuovo commit/push nel riesame.

S1-004B completato il 19 ottobre come flusso interno conferma/reset con token Identity
24h/30min, marker ultimo link, consumo atomico e sender solo in memoria nei test.
[Dettagli](account-recovery-baseline.md). S1-004C/D non iniziati: nessun endpoint,
sessione, MFA o UI. A+B pubblicati nel commit `ca7edf4` dopo i gate e secret scan.
[CI GitHub verde](https://github.com/maffeinet/ramai/actions/runs/39230830575):
112 test senza skipped, build senza warning/errori, format e gate frontend verdi.
Restano 5 high npm dev/ESLint deprecato, SEC-EX-001 non prorogata.
La frase storica "nessun commit/push" del blocco A sotto non prevale sull'autorizzazione B.

S1-004A autorizzato e completato localmente il 19 ottobre: componenti interni account,
validator NFC/denylist, Identity hash/lockout, rate Redis e test. Nessun login/endpoint,
email/reset, MFA o sessione: B/C/D non iniziati. Nessun nuovo commit/push.
Schema e modelli invariati; [dettagli tecnici/limiti](account-security-baseline.md),
[report](sprint1-progress.md). Questo paragrafo è lo storico A prima della pubblicazione
A+B; la run 39230830575 sopra copre ora entrambi i blocchi.

S1-003 completato localmente il 18 ottobre dopo istruzione di prosecuzione: adapter
EF Core/Identity 10.0.12 e provider Npgsql 10.0.3, schemi/history separati Identity/Tenants,
reader e migrazioni testate su PostgreSQL reale temporaneo. Nessuna modifica a dati
o volumi preesistenti. [Procedure e limiti](database-migrations.md),
[report](sprint1-progress.md). Pubblicato il 19 ottobre nel commit `7da7758`,
[pipeline verde](https://github.com/maffeinet/ramai/actions/runs/39210403963):
58 test backend senza skipped e tutti i gate frontend. S1-004/login non avviato.
Audit npm del 19 conferma la stessa catena 5 high dev, 0 critical/0 production:
SEC-EX-001 rimane in vigore solo per sviluppo interno, non prorogata.
I paragrafi storici S1-002 sotto restano riferiti al 17 ottobre.

## Scopo e fonti

Contesto stabile per riprendere il lavoro senza ricostruire la conversazione.
Questo file documenta il progetto: non implementa la funzionalità Business Memory.
Non sostituisce [AGENTS.md](../AGENTS.md), gli [ADR](adr/), il
[backlog](../tasks/SPRINT0_BACKLOG.md) o la [Definition of Done](definition-of-done.md).
In caso di conflitto, segnalare la differenza e chiedere una decisione, senza cambiare
autonomamente stack, scope o confini. Lo stato operativo è in [handoff.md](handoff.md).

## Obiettivo e modello di erogazione

RAMAI è attualmente una Foundation applicativa, non un prodotto già utilizzabile dai
clienti. La rilettura dei PDF 04 e 08 del 16 ottobre conferma l'obiettivo SaaS europeo
multi-azienda e cloud in regione UE, non soltanto un orientamento della conversazione.
ADR-003 conferma la multitenancy; provider cloud concreto, deployment di produzione,
dominio, prezzi e abbonamenti non sono ancora decisi in questo repository.
L'utente ha riconfermato esplicitamente SaaS e fino a 500 utenti attivi mensili per
la prima fase (16 ottobre). Ha chiesto di considerare anche Gmail e altri sistemi.
Modello locale e policy tecnica approvati: account con qualsiasi email verificata, login
indipendente dal futuro collegamento della posta e nessun obbligo di licenza M365.
GitHub conserva il codice ed esegue CI: non ospita il servizio
applicativo in questa configurazione.

## Decisioni architetturali confermate

- MVP come modular monolith, non microservizi (ADR-001).
- Backend ASP.NET Core .NET 10; frontend Next.js App Router, React, TypeScript e npm.
- PostgreSQL datastore primario, pgvector senza vector database separato (ADR-002).
- Redis per cache e short-lived state; infrastruttura locale via Compose (ADR-007).
- Shared-schema multitenancy con TenantId; tenant derivato dall'identità autenticata,
  mai da un parametro client considerato attendibile (ADR-003).
- Nessun accesso alle tabelle interne di altri moduli: application contracts o eventi.
- Operazioni pesanti future asincrone (ADR-004).
- Dominio indipendente dai provider AI (ADR-005); output AI futuri strutturati/validati.
- Microsoft 365 primo connector reale previsto, non ancora implementato (ADR-006).
- Nessun secret in codice, commit o log; nessuna azione irreversibile autonoma.

Tenancy, AI e connector sopra descritti sono vincoli futuri, non capacità già presenti.

## Mappa del repository

| Percorso | Responsabilità |
| --- | --- |
| `src/backend/Ramai.Api` | Composition root, HTTP, health, correlation ID |
| `src/backend/Ramai.Domain` | Baseline dominio condiviso |
| `src/backend/Ramai.Application` | Baseline applicativa condivisa |
| `src/backend/Ramai.Infrastructure` | Configurazione/connettività PostgreSQL e Redis |
| `src/backend/Modules/Ramai.Modules.Identity` | Modello UserAccount globale; autenticazione non implementata |
| `src/backend/Modules/Ramai.Modules.Tenants` | Modelli Tenant/Membership e ruoli, nessuna persistence |
| `src/backend/Modules/Ramai.Modules.Connectors` | Placeholder integrazioni |
| `src/backend/Modules/Ramai.Modules.Memory` | Placeholder Business Memory |
| `src/backend/Modules/Ramai.Modules.Insights` | Placeholder insight |
| `src/backend/Modules/Ramai.Modules.Assistant` | Placeholder assistente |
| `src/backend/Modules/Ramai.Modules.Billing` | Placeholder billing |
| `src/frontend/ramai-web` | Interfaccia Foundation statica; non chiama ancora l'API |
| `tests` | Unit, integration e architecture test |
| `infra/postgres/init` | Abilitazione estensione vector, nessuna tabella applicativa |
| `scripts` | Importazione ambiente locale e relativi test PowerShell |
| `docker-compose.yml` | PostgreSQL/Redis, rete, volumi e health Docker |

## Baseline implementata

S0-001–003: repository, solution backend e frontend placeholder.
S0-004–007: PostgreSQL/pgvector, Redis, Compose e configurazione/secrets.
S0-008–009: `/health` applicativo e logging JSON con correlation ID.
S0-010: 16 test backend (7 unit, 6 integration, 3 architecture).
S0-011: GitHub Actions su push/PR con runner GitHub-hosted e gate backend/frontend.
S0-012: README bootstrap e loader `.env` con 7 test PowerShell locali.

Pubblicazione confermata fino al commit `4c441b3`, CI backend/frontend verde.
Il 16 ottobre l'utente ha approvato chiusura M0 con SEC-EX-001 accettata temporaneamente
solo per sviluppo interno: riesame 30 ottobre o prima secondo le condizioni registrate.
La decisione non è una risoluzione delle vulnerabilità o un via libera commerciale. L'audit npm
del 16 ottobre segnala 5 high nel tooling dev e zero nelle dipendenze production;
dettagli e limiti nel report e nell'handoff, nessuna dipendenza aggiornata.
Nella successiva prova di riduzione del debt è stata aggiunta solo una policy
che nega il postinstall unrs-resolver, verificata con npm 11.17.0 e gate frontend verdi.
Il tentativo rigoroso ESLint 10 fallisce per peer incompatibili: rimangono ESLint 9
fuori supporto e i 5 high dev, senza indebolire i controlli o forzare aggiornamenti.
La policy e i documenti sono pubblicati nel commit `258e603`; anche la
[run GitHub/Linux 38887367228](https://github.com/maffeinet/ramai/actions/runs/38887367228)
è verde, verificata il 16 ottobre. Il debt residuo è stato poi accettato esplicitamente
nei limiti SEC-EX-001, senza coprire nuovi rischi o scenari di produzione.

L'health applicativo verifica applicazione, PostgreSQL e Redis e resta distinto
dall'health Docker. L'API riusa un X-Correlation-ID valido o ne genera uno, lo
restituisce nella response e lo include nello scope del log di richiesta.

Non presenti: autenticazione reale, TenantContext operativo, entità tenant-scoped,
Business Memory, AI, connector, billing o worker applicativi reali. Nessuna tabella
Business Memory creata. Sprint 1 non avviato e non autorizzato da questa documentazione.

## Pianificazione successiva, non implementazione

Le specifiche 04/11 prevedono Identity + Tenant + DB baseline come Sprint 1.
Il [backlog proposto](../tasks/SPRINT1_BACKLOG.md) contiene 12 task e gate umani;
la [SEC-EX-001](security-exception.md) accettata limita il debt noto allo sviluppo
interno con scadenza/riesame. Il backlog resta bozza: accettazione del rischio noto
e chiusura M0 non approvano implicitamente provider, dettagli auth o codice Sprint 1.
Le specifiche 08/12 restano vincolanti per review e gate prima della beta/go-live.
S1-001 completato il 17 ottobre nel perimetro di analisi/approvazione tecnica:
[ADR-009 Identity/access](adr/ADR-009-identity-access.md), Accepted sul perimetro locale
e sui parametri tecnici dal 17 ottobre, non implementato. L'utente ha poi
richiesto identità locale per validare il mercato senza CIAM esterno obbligatorio;
la raccomandazione managed mai approvata è superata per l'MVP. Proposti ASP.NET Core
Identity, PostgreSQL/EF Core e cookie protetti, con autorizzazioni tenant interne.
Non è un nuovo microservizio/token server. Parametri lifecycle email/reset e security
approvati il 17 ottobre, nessun codice o servizio attivato. Managed è storico nell'ADR.
Il numero 008 resta riservato al tema no autonomous write citato dal PDF 04.
Modello di prodotto confermato: single user, multi user/team e mono/multi azienda.
Struttura unica User globale → Membership con ruolo/stato → Tenant; più utenti per azienda
e più aziende per utente, con accesso verificato separatamente. Nessuna esclusione
del TenantId nel caso single-user, nessun accesso cross-azienda automatico.
"Team" nel primo blocco è collaborazione nel workspace, non gruppi/reparti autonomi;
predisposizione multi-azienda approvata, UI del selettore da definire. Nessuna entità
Team o piano billing, nessun codice implementato. Conferma del modello e accettazione
SEC-EX-001 restano decisioni distinte; le policy tecniche sono poi state approvate
esplicitamente il 17 ottobre, senza attestazione normativa o attivazione di servizi.

Registro Sprint 1: [sprint1-progress.md](sprint1-progress.md). S1-002 poi autorizzato
e completato localmente il 17 ottobre: UserAccount, Tenant/Membership, reader contracts
Application, test di invarianti e confini. [Modello/ownership](identity-tenant-model.md).
Moduli ancora senza ProjectReference; nessuna nuova dipendenza. Build/format verdi,
49 test pass/1 infra skipped perché Docker non disponibile, SDK effettivo 10.0.402.
Nessun DB, Identity framework, reader implementato, login o API tenant. L'unicità email
globale resta un vincolo persistente futuro; i test modello non provano isolamento SQL.
Prossimo task S1-003 da autorizzare, non iniziato. Nessun commit/push in questo blocco.

## Indirizzo security/privacy per l'identità locale

L'utente richiede riferimenti NIS2 e GDPR. La [baseline tecnica approvata](identity-security-policy.md)
definisce controlli password, MFA, sessioni, revoca, isolamento e audit più gate privacy/
organizzativi. Nessun claim "compliant" o applicabilità NIS2 dedotto da 500 MAU;
valori numerici sono decisioni tecniche approvate, non requisiti legali automatici. Tutto ancora
da implementare/verificare; SEC-EX-001 non estesa all'autenticazione o ai dati reali.

## Versioni della baseline, non raccomandazioni di aggiornamento

- SDK usato/verificato: 10.0.401; `global.json`: 10.0.100, rollForward latestFeature.
- Node 24.19.0; npm verificato 11.17.0.
- Next.js 16.3.8; React/React DOM 19.3.0; TypeScript 6.0.3.
- ESLint 9.39.5; eslint-config-next 16.3.8.
- Immagini: `pgvector/pgvector:0.8.6-pg17`, `redis:7.2.16-alpine3.21`.

Verificare sempre i manifest prima di cambiare dipendenze; non presumere che queste
siano le versioni più recenti o prive di vulnerabilità.

## Abitudini operative e sicurezza

- Leggere acceptance criteria, mostrare i file previsti, implementare il minimo,
  verificare gate proporzionati e aggiornare documentazione.
- `.env` è locale/ignorato; Compose lo legge, ASP.NET Core non lo importa da solo.
  Usare `scripts/Import-DevEnvironment.ps1` nella shell backend/test; non stampare valori.
- Usare `docker compose config --quiet`, non output completo con credenziali interpolate.
- I volumi sono persistenti: `docker compose down` li conserva. Non usare rimozioni
  dei volumi o reinizializzazioni senza autorizzazione esplicita.
- Un volume PostgreSQL esistente non riesegue automaticamente gli script init e
  non cambia password soltanto cambiando `.env`.
- Nessun aggiornamento forzato npm, modifica degli ADR o avvio Sprint 1 implicito.
- Richiedere autorizzazione per nuovi commit/push; le autorizzazioni precedenti
  riguardavano le rispettive pubblicazioni, non il lavoro futuro.

## Come mantenere questi documenti

Aggiornare questo file quando cambia una decisione confermata; aggiornare handoff
dopo ogni blocco con commit, run, verifiche, limiti e prossimo passo autorizzato.
Datare audit e risultati: un test storico verde non garantisce lo stato runtime attuale.
Non copiare secret, output sensibili o trascrizioni integrali della chat.
