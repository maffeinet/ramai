# S1-003 — Persistence e migrazioni

Aggiornamento 20 ottobre S1-006: [provisioning](workspace-provisioning-baseline.md)
aggiunge la migrazione 20261020123949_WorkspaceProvisioning e la tabella Tenants
WorkspaceProvisioningRequests. Down rifiuta la perdita di registrazioni già usate;
le ownership/separazioni sottostanti restano invariate. Nessuna migrazione sui dati
utente applicata automaticamente.

Implementazione locale: 2026-10-18. Scope limitato alla baseline database;
nessun login, sessione, endpoint o provisioning pubblico. Approvazione di prosecuzione
dopo S1-002 e conferma Docker disponibile. Riferimenti: [ADR-009](adr/ADR-009-identity-access.md),
[modello](identity-tenant-model.md), [report](sprint1-progress.md).

## Layout e ownership

I sette progetti in `Modules/` rimangono modelli indipendenti dal framework.
Due adapter sotto `src/backend/Persistence/` implementano la persistenza degli stessi
moduli nel monolite, non nuovi bounded context, servizi o datastore.

| Adapter | Dipendenze RAMAI consentite | Tabelle/schema |
| --- | --- | --- |
| Ramai.Modules.Identity.Persistence | Application (reader contract) | identity.Users, UserClaims, UserLogins, UserTokens |
| Ramai.Modules.Tenants.Persistence | Application e proprio modello Tenants | tenants.Tenants, Memberships |

Ogni contesto ha `__EFMigrationsHistory` nel proprio schema. I layer condivisi e
l'API non acquisiscono riferimenti agli adapter in questo task. Nessuna registrazione
DI, migrazione automatica all'avvio o modifica a `/health`. I reader sono implementati
e testati, non esposti come API o usati per autorizzare un client arbitrario.

ASP.NET Core Identity `IdentityUserContext<AccountUser, Guid>` è l'unica source of
truth persistente per account e credenziali. `UserAccount` resta modello puro, senza
una seconda tabella. La stessa chiave globale UserId corrisponde a Users.Id.
Le tabelle accessorie sono quelle standard del framework, senza seed, ruoli globali,
provider social configurati o token emessi. PasswordHash nullable non abilita login.
S1-004 dovrà configurare UserManager/normalizzazione/policy e mantenere questa fonte
unica, senza duplicare account o archiviare password artigianali.

Tenants mappa direttamente l'aggregato. Il costruttore privato per materializzazione
non crea un Owner fittizio; `Tenant.Create` conserva il bootstrap Owner. La proprietà
Memberships espone IReadOnlyList con wrapper runtime non modificabile e backing field
privato per EF; i test di protezione originali restano attivi.

## Vincoli e limiti

- Users: PK globale UUID esplicito/non vuoto; email e chiave normalizzata obbligatorie,
  massimo 256; indice globale univoco NormalizedEmail; username univoco standard.
- Tenants: PK TenantId non vuoto, nome non vuoto massimo 256.
- Memberships: PK composta TenantId/UserId, valida anche per righe inattive; UUID non
  vuoti, FK verso il proprio Tenant con delete Restrict, indice UserId; enum ruolo/stato
  verificati da CHECK e al massimo un Owner attivo per tenant con indice parziale.
- Creazione esattamente di un Owner garantita dall'aggregato e dalla singola SaveChanges
  transazionale; SQL arbitrario potrebbe creare un tenant senza Owner o cancellarlo.
  Non si dichiara una garanzia DB di "almeno un Owner" o di autorizzazione.
- Nessuna FK/query verso identity.Users nel contesto Tenants. Esistenza/idoneità
  dell'utente devono essere controllate dal servizio tramite IUserAccountReader prima
  del provisioning S1-006; integrità cross-module e offboarding non sono magicamente
  garantiti da questa baseline. I test di store usano anche ID sintetici senza account.
- Nessun TenantContext/query filter/write guard ancora: S1-005/007. I reader filtrano
  esplicitamente coppia tenant/utente e stato; questo non dimostra IDOR HTTP risolto.
- Vincoli univoci testati anche con connessioni concorrenti; gestione errori/retry
  e transazione del provisioning idempotente restano in S1-006.

## Versioni e motivazione

SDK locale 10.0.402, net10.0. EF Core e relativo Design/CLI **10.0.12**;
Microsoft.AspNetCore.Identity.EntityFrameworkCore **10.0.12**, provider
Npgsql.EntityFrameworkCore.PostgreSQL **10.0.3**, Npgsql risolto **10.0.3**.
Identity usa gli store standard, il provider usa PostgreSQL già approvato: nessuna
nuova infrastruttura. Design è PrivateAssets=all; CLI è fissata nel manifest locale
`.config/dotnet-tools.json`, non aggiorna il vecchio tool globale installato.
NuGet: [Identity](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore/10.0.12),
[provider](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3).
Il restore tool segnala disponibile CLI 10.0.13: qui il pin 10.0.12 mantiene allineati
tool e pacchetti già scelti; non è un'affermazione che siano le ultime patch disponibili.

Immagini verificate: `pgvector/pgvector:0.8.6-pg17`, `redis:7.2.16-alpine3.21`;
Docker Engine locale 29.8.1. Compose root invariato.

## Comandi riproducibili e protezione dati

Da root, prima di qualunque database update: backup verificato, ambiente corretto,
review dello script e credenziali di migrazione appropriate. Non usare il runtime
con privilegi di amministrazione solo per fare migrazioni automatiche.

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/backend/Persistence/Ramai.Modules.Identity.Persistence
dotnet ef migrations has-pending-model-changes --project src/backend/Persistence/Ramai.Modules.Tenants.Persistence --context TenantsStoreContext
dotnet ef migrations script --idempotent --project src/backend/Persistence/Ramai.Modules.Identity.Persistence
dotnet ef migrations script --idempotent --project src/backend/Persistence/Ramai.Modules.Tenants.Persistence --context TenantsStoreContext
```

Per applicare, impostare `RAMAI_MIGRATION_CONNECTION` solo nell'ambiente del processo
da fonte locale protetta: non incollare valori nella cronologia, output, report o Git.
Le factory hanno un fallback senza password per scaffolding/script, NON una connessione
di sviluppo già configurata. Non usare `--connection` con credenziali visibili.
Con la variabile impostata correttamente, ordine esplicito Identity poi Tenants:

```powershell
dotnet ef database update --project src/backend/Persistence/Ramai.Modules.Identity.Persistence
dotnet ef database update --project src/backend/Persistence/Ramai.Modules.Tenants.Persistence --context TenantsStoreContext
Remove-Item Env:RAMAI_MIGRATION_CONNECTION
```

InitialIdentity esegue `CREATE EXTENSION IF NOT EXISTS vector` anche su DB vuoto;
richiede immagine compatibile e privilegi per creare l'estensione. Su Foundation già
inizializzata non la ricrea. Nessuna tabella Business Memory o colonna embedding.
Le Down rimuovono SOLO le tabelle di proprietà del modulo, mai vector/schema pubblico.
**Una Down cancella i dati delle tabelle rimosse**: non è un backup e non è una procedura
autorizzata per sviluppo esistente o produzione. Nessun downgrade automatico.

Non è stata applicata alcuna migrazione ai volumi/database preesistenti. Tutte le prove
sono avvenute su progetto Compose temporaneo, porte assegnate dinamicamente, password
casuale in memoria. Rimosse soltanto le risorse temporanee create per questa verifica.
Nessun `.env` locale creato o sovrascritto.

## Test e recovery verificati

Test ordinari: metadati dei due modelli, nessuna FK/tabella cross-module, script/history
separate e mantenimento dei confini architetturali originali.

Test opt-in `RAMAI_RUN_INFRASTRUCTURE_TESTS=1`, stesse RAMAI_DB_* / RAMAI_REDIS_* della
baseline: richiedono un PostgreSQL **di test** con privilegi CREATEDB/estensione;
creano e rimuovono solo DB `ramai_s1test_<GUID>` generati dalla singola invocazione,
mai il database configurato di sviluppo. In CI il servizio temporaneo già ha tali privilegi.

1. DB vuoto: due migrazioni, pgvector, round-trip aggregato e reader, ruoli A/B.
2. Foundation sintetica: tabella/sentinella preesistente e vector conservati dopo
   upgrade ripetuto, Down delle migrazioni e riapplicazione solo nel DB usa-e-getta.
3. PK account, NormalizedEmail duplicata e Guid.Empty rifiutati da PostgreSQL.
4. Membership duplicata anche inattiva, tenant inesistente, ID vuoto, enum invalidi,
   secondo Owner/inattivazione Owner rifiutati; rollback transazione senza righe residue.
5. Connessioni concorrenti: solo un account con stessa chiave email e una membership
   con stessa coppia persistono.

Questo è recovery **delle migrazioni iniziali su fixture sintetica**, non restore da
backup, PITR, disaster recovery o prova dei target RPO/RTO di produzione. La Down in
questa prova parte senza dati account da conservare; la sentinella Foundation rimane.
La strategia operativa backup/restore e il disaccordo RPO dei PDF restano pre-go-live.
La pipeline esistente scopre i test opt-in con infrastruttura già attiva; nessuna nuova
run GitHub svolta senza commit/push autorizzati.
