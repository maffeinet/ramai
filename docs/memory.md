# RAMAI — Memoria del progetto

Aggiornato: 2026-10-18, Europe/Rome.

## Ultimo aggiornamento operativo

S1-003 completato localmente il 18 ottobre dopo istruzione di prosecuzione: adapter
EF Core/Identity 10.0.12 e provider Npgsql 10.0.3, schemi/history separati Identity/Tenants,
reader e migrazioni testate su PostgreSQL reale temporaneo. Nessuna modifica a dati
o volumi preesistenti. [Procedure e limiti](database-migrations.md),
[report](sprint1-progress.md). S1-004/login non avviato; nessun commit/push.
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
