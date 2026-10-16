# RAMAI — Memoria del progetto

Aggiornato: 2026-10-16, Europe/Rome.

## Scopo e fonti

Contesto stabile per riprendere il lavoro senza ricostruire la conversazione.
Questo file documenta il progetto: non implementa la funzionalità Business Memory.
Non sostituisce [AGENTS.md](../AGENTS.md), gli [ADR](adr/), il
[backlog](../tasks/SPRINT0_BACKLOG.md) o la [Definition of Done](definition-of-done.md).
In caso di conflitto, segnalare la differenza e chiedere una decisione, senza cambiare
autonomamente stack, scope o confini. Lo stato operativo è in [handoff.md](handoff.md).

## Obiettivo e modello di erogazione

RAMAI è attualmente una Foundation applicativa, non un prodotto già utilizzabile dai
clienti. Nella conversazione è emerso l'orientamento verso un SaaS multi-azienda,
accessibile dal browser. ADR-003 conferma la multitenancy, non una scelta di hosting.
Provider cloud, deployment di produzione, dominio, prezzi e abbonamenti non sono
ancora formalizzati. GitHub conserva il codice ed esegue CI: non ospita il servizio
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
| `src/backend/Modules/Ramai.Modules.Identity` | Placeholder identità |
| `src/backend/Modules/Ramai.Modules.Tenants` | Placeholder aziende/tenant |
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

Pubblicazione confermata fino al commit `6d5f697`, CI backend/frontend verde.
Il 16 ottobre README e progress report sono stati riallineati localmente; rimane
la decisione umana di accettazione del debt e chiusura formale M0. L'audit npm
del 16 ottobre segnala 5 high nel tooling dev e zero nelle dipendenze production;
dettagli e limiti nel report e nell'handoff, nessuna dipendenza aggiornata.
Nella successiva prova di riduzione del debt è stata aggiunta solo una policy locale
che nega il postinstall unrs-resolver, verificata con npm 11.17.0 e gate frontend verdi.
Il tentativo rigoroso ESLint 10 fallisce per peer incompatibili: rimangono ESLint 9
fuori supporto e i 5 high dev, senza indebolire i controlli o forzare aggiornamenti.

L'health applicativo verifica applicazione, PostgreSQL e Redis e resta distinto
dall'health Docker. L'API riusa un X-Correlation-ID valido o ne genera uno, lo
restituisce nella response e lo include nello scope del log di richiesta.

Non presenti: autenticazione reale, TenantContext operativo, entità tenant-scoped,
Business Memory, AI, connector, billing o worker applicativi reali. Nessuna tabella
Business Memory creata. Sprint 1 non avviato e non autorizzato da questa documentazione.

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
