# S1-006 — Provisioning workspace e primo Owner

Aggiornamento 21 ottobre: provisioning esposto via API protetta nel solo
[S1-008](workspace-api-baseline.md), con MFA, CSRF e quota Redis distribuita.
Il perimetro interno descritto sotto conserva lo storico S1-006; nessuna UI/beta.

Data: 2026-10-20, Europe/Rome. Scope autorizzato: solo S1-006, componenti interni.
Nessuna nuova API/UI, attivazione esterna, commit/push o S1-007+.
Riferimenti: [TenantContext](tenant-context-baseline.md),
[policy](identity-security-policy.md), [migrazioni](database-migrations.md).

## Admission e proprietà

WorkspaceProvisioningService accetta un principal già autenticato dal cookie
handler, nome e requestId non vuoto. Non accetta TenantId di destinazione, UserId
del creator o Role scelti dal client. Controlla forma/scadenza della sessione con
gli stessi controlli S1-005, validator Identity e IUserAccountReader: account
attivo/confermato e corrispondenza esatta dell'identità. Per creare un Owner è
obbligatoria la prova MFA del principal verificato. Nessun tenant selezionato è
necessario per creare il primo workspace, né un ruolo in un'azienda esistente.

Nome: trim, non vuoto, massimo 256 unità UTF-16, niente controlli interni o carattere
di sostituzione. Il nome non è una chiave business globale: due comandi distinti
possono creare aziende con lo stesso nome. Nessuna unicità da dominio email, piano
commerciale, quota utenti/workspace o iscrizione automatica a tenant esistenti.
Gli input sono minimizzati; un nome non valido non crea una registrazione idempotente.

Il writer genera un TenantId casuale server-side e invoca Tenant.Create con il
soggetto verificato: aggregato con esattamente una membership iniziale Owner.
L'output contiene solo esito e TenantId, non è un'autorizzazione o un ticket di
sessione. Non modifica il principal o seleziona implicitamente il nuovo workspace;
il futuro trasporto dovrà usare S1-005 e conservare la selezione nel ticket protetto.

## Idempotenza e atomicità

Nuova tabella **tenants.WorkspaceProvisioningRequests**, posseduta da Tenants:

- TenantId obbligatorio e FK soltanto alla propria tabella Tenants, con indice unico;
- chiave primaria ActorUserId/RequestId, entrambi non vuoti;
- RequestName registra il payload ammesso per confrontare i retry;
- nessuna FK/query diretta a identity.Users o altro modulo.

RequestId è una chiave di retry, non un TenantId o un secret. La stessa coppia
soggetto/requestId e stesso nome dopo trim restituisce Replayed con lo stesso
TenantId. Payload diverso: Conflict senza modifica o TenantId nel risultato.
Stessa chiave usata da un altro soggetto non recupera il workspace precedente:
la coppia actor/request è distinta. Usare l'ID di un tenant altrui come requestId
non ne assegna la proprietà: si genera comunque un nuovo TenantId.

Tenant, membership Owner e registrazione idempotente vengono salvati nello stesso
DbContext/transazione Tenants. Non si effettuano scritture Identity, quindi non
serve una transazione distribuita o accesso a tabelle di altro modulo. Un errore
prima del commit annulla le tre righe. IDbContextFactory fornisce un contesto isolato
per ogni tentativo: il writer non cancella il tracker del chiamante e non riusa
transazioni fallite. Nessun nuovo NuGet o servizio infrastrutturale.

Le richieste concorrenti sono arbitrate dalla PK nel DB. Il perdente esegue rollback
del proprio Tenant/Owner provvisori, poi rilegge la registrazione con un contesto
nuovo. Si gestisce soltanto la violazione della PK idempotente prevista; gli altri
errori propagano, senza fingere successo. Nel replay viene verificata anche la
membership Owner attiva corrispondente. Nessuna promozione o riattivazione implicita.

Ogni retry passa di nuovo dai controlli account/MFA: una sessione revocata non può
ottenere successo solo perché una precedente operazione aveva creato il workspace.
Una perdita di connessione durante commit può rendere l'esito incerto: ritentare
con la **stessa** chiave, non generarne una nuova. La registrazione persistente
riconcilia il risultato. Non è promesso exactly-once universale su chiamate diverse.

La verifica Identity è admission via contratto, non un lock cross-module sul suo DB.
Revoche committate prima della verifica vengono osservate; un'operazione già ammessa
può concludersi se l'account cambia durante il commit Tenants, come nella policy
delle richieste in corso. Successive operazioni restano negate dal validator/context.
Offboarding, cancellazione globale e recupero operativo non sono implementati qui.

## Migrazione e recovery

Migrazione Tenants: **20261020123949_WorkspaceProvisioning**, con designer e snapshot.
Up crea solo la tabella/indice/FK/check; conserva tenant/membership preesistenti,
schema Identity, Foundation e pgvector. Nessuna esecuzione sui volumi utente, né
auto-migrate all'avvio. Si applica con la procedura esplicita del documento migrazioni,
RAMAI_MIGRATION_CONNECTION protetta e script revisionato prima dell'uso reale.

Down consente il rollback solo se la tabella registrazioni è vuota. Se contiene
successi solleva 55000 e mantiene dati/schema/history nella transazione: cancellare
la memoria dei retry non è una recovery sicura. Non svuotarla per forzare il rollback.
Prima di un rollback reale con dati serve una procedura revisionata e backup
verificato; roll-forward correttivo preferibile al reset del registro.
Retention/delete di queste righe seguono il futuro lifecycle tenant, non un TTL
automatico che renderebbe nuovamente eseguibili i vecchi comandi.

## Logging, test e stato operativo

Writer: logging standard strutturato di esito, TenantId/UserId pseudonimi e scope
CorrelationId. Niente nome workspace, requestId, email, password o principal.
Questo non sostituisce audit segregato/retention; il futuro endpoint deve mantenere
HTTPS/CSRF, quote anti-abuso e correlation ID validato come gli altri comandi.

Unit: soggetto derivato dalla sessione, MFA obbligatoria, account assente/inattivo/
non confermato/revocato/mismatch, input invalidi, chiave vuota e dipendenza fallita.
Integration: login/password/MFA/recovery reali e PostgreSQL/Redis isolati; successo,
retry/conflict, dodici tentativi concorrenti, errore forzato dopo SaveChanges ma
prima di commit (zero righe residue), tentativo di takeover con ID altrui, chiavi
per attore, retry dopo revoca e rollback migrazione bloccato con registrazioni.
Il test delle chiavi per attore a livello writer non pretende di essere un test
di admission HTTP: il port interno riceve solo un soggetto già verificato.

Gate finali e file nel [report](sprint1-progress.md): **201 pass/0 skipped**, build
Release 0 warning/errori, format e frontend PASS, EF modello/script PASS, Gitleaks
53 file senza leak. Risorse test isolate rimosse, nessuna migrazione su dati utente.
Nessun wiring API→Tenants,
endpoint o browser E2E aggiunto: integrazione host e CSRF appartengono a S1-008,
con revisione del composition root già segnalata. Demo/accesso esterni non pronti.
SEC-EX-001 e warning npm invariati, nessuna proroga o migrazione tooling.
Prossimo task da autorizzare: S1-007, isolamento letture/scritture tenant-scoped.
