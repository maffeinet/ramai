# S1-008 — Piano API/RBAC e checkpoint interno

20 ottobre 2026, ripresa notturna. Task autorizzato; nessuna UI/S1-009.

Aggiornamento 21 ottobre: sospesi approvati esplicitamente dall'utente, ADR-010
esteso e HTTP implementato. [Baseline corrente](workspace-api-baseline.md),
278 test verdi/0 skipped e gate locali PASS. Questo piano conserva il checkpoint
iniziale: le indicazioni di attendere sotto sono storiche, non il blocco corrente.

## File previsti prima dell'implementazione

Primo step indipendente dal wiring host:

- Application/Contracts/Tenants/WorkspaceOperations.cs: DTO minimizzati, esiti,
  porta di persistence con TenantContext verificato, servizio applicativo pubblico.
- Application/Tenancy/WorkspaceOperationsService.cs: nuova risoluzione per ogni
  operazione, RBAC e validazione del solo nome workspace.
- Persistence/Ramai.Modules.Tenants.Persistence/WorkspaceDataStore.cs: operazioni
  sul TenantDataContext S1-007, senza ricorso al contesto globale bootstrap.
- tests/Ramai.UnitTests/WorkspaceOperationsTests.cs e
  tests/Ramai.IntegrationTests/WorkspaceOperationsIntegrationTests.cs.
- Report, memory, handoff e questo piano per esiti effettivi.

Percorsi Application/Persistence relativi a src/backend. Nessun NuGet nuovo,
migrazione, dato/volume utente, frontend o cambio confini moduli.

Step HTTP successivo: handler /api/v1, contratti input chiusi, ProblemDetails con
correlation ID, protezioni HTTPS/origine/CSRF e test cookie reali. Servirà composizione
host Tenants, finora esclusa da ADR-010 limitato a Identity. **Non aggiungere ora un
riferimento API→Tenants** senza decisione umana sull'eccezione host-only; non
aggirare il grafo con reflection, service locator o spostamento in Infrastructure.
L'autorizzazione al task non viene registrata come approvazione già ottenuta di
questa estensione architetturale. La proposta è: host referenzia l'adapter Tenants
solo in Composition per DI; handler esclusivamente contratti Application. Nessuna
comunicazione diretta o lettura tabelle fra moduli. Test architetturali da estendere
con la sola eccezione approvata, non rimuovere.

## Contratti e policy proposte per il trasporto

| Percorso proposto | Autorizzazione |
| --- | --- |
| GET /api/v1/profile | Sessione account valida, solo UserId/idoneità minimi |
| POST /api/v1/workspaces | Provisioning S1-006, MFA e idempotenza, CSRF |
| POST /api/v1/workspaces/select | Proposta non fidata verificata dal resolver, CSRF; conserva inizio assoluto |
| GET /api/v1/workspace | Qualsiasi membership corrente ammessa |
| GET /api/v1/workspace/settings | Owner/Admin con MFA, solo nome non sensibile |
| PUT /api/v1/workspace/settings | Owner/Admin con MFA, solo nome, CSRF |

Non introdurre elenco globale workspace o lookup cross-tenant per comodità UI.
Policy HTTP da confermare: 401 assenza/sessione non valida, 403 mancanza membership,
MFA o ruolo, 409 selezione mancante; se si espongono identificatori risorsa, usare
404 indistinguibile per ID non corrente/inesistente senza lookup esterno al contesto.
Non inviare principal, stamp, claim di recupero, password o stringhe connessione.
Nessun input libero UserId/Role/TenantId per mutazioni: selezione workspace è una
proposta esplicita separata, mai autorizzazione. JSON sconosciuto rifiutato, non
ignorato; nome 1–256 UTF-16 dopo trim, niente controlli o caratteri sostitutivi.

## Strategia test

Unit: tutti i ruoli, MFA Owner/Admin, assenza/selezione/revoca, nessun accesso
alla porta quando negato, input invalidi, eccezioni/cancellazione senza fallback.
Integration interne: resolver/sessione/account reali, PostgreSQL/Redis isolati,
Owner A/Member B/Admin con MFA, rinomina autorizzata A senza alterare B, ruolo
riletto e membership revocata dopo una richiesta precedente. Nessuna autenticazione
finta dichiarata come prova del cookie. Successivamente HTTP integration: 401/403,
CSRF, origine/HTTPS, mass assignment/IDOR e indisponibilità con log minimizzati.

Build/format/suite completa/frontend/EF/secret scan; risultati interni non equivalgono
a test HTTP o completamento S1-008. Pubblicazione separatamente bloccata in attesa
conferma umana dei prerequisiti C/D/005/006; nessun tentativo di aggiramento.

## Checkpoint eseguito nella ripresa 23:07

Creati i sei file elencati nel primo step; nessun nuovo riferimento host o endpoint.
22 unit e 6 integration reali nuovi verdi; suite completa **250/250, zero skipped**
(101 unit, 141 integration, 8 architecture). Build Release 0 warning/errori,
format verify e frontend invariato lint/type-check/build PASS; entrambi i modelli EF
senza cambiamenti. PostgreSQL/Redis temporanei healthy, vector 0.8.6/PONG.
Questo non soddisfa ancora gli acceptance HTTP S1-008. Attendere decisioni sopra,
senza estendere automaticamente ADR-010 o reiterare pubblicazione respinta.
Gitleaks 65 file senza leak e link locali di 17 documenti/whitespace PASS; risorse
Docker temporanee rimosse con verifica dell'assenza, senza toccare volumi utente.
