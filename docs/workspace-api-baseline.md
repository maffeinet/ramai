# S1-008 — API workspace e autorizzazioni

21 ottobre 2026. Autorizzazione: "ok procedi con tutto quanto è in sospeso",
dopo le richieste esplicite su estensione ADR-010 e pubblicazione dei prerequisiti.
Scope esclusivamente S1-008 e pubblicazione C/D/005/006/007/008 già verificati;
nessuna UI/S1-009, AI, connector, billing, deploy o nuova infrastruttura.

## Composizione e prerequisiti

[ADR-010](adr/ADR-010-api-composition-root.md) esteso all'adapter Tenants soltanto
in Composition. Handler usano contratti Application, non EF, UserManager o tabelle.
Contesto globale Tenants limitato a migrazioni/bootstrap/lookup/provisioning;
WorkspaceDataStore usa il contesto operativo filtrato S1-007.
Nessun NuGet o schema nuovo in S1-008. La migrazione di idempotenza S1-006 rimane
prerequisito: applicare esplicitamente entrambe le serie di migrazioni seguendo
[procedure](database-migrations.md); nessun auto-migrate nell'API.

API disponibili solo con l'opt-in RAMAI_AUTH_ENABLED e la configurazione HTTPS,
keyring/certificato, HMAC e Redis già richiesta dalla
[baseline sessioni](account-session-baseline.md). Auth disattivata: nessun endpoint
account/workspace registrato. Nessun canale email reale o account demo persistente.

## Contratti v1

| Metodo e route | Input JSON chiuso | Regola |
| --- | --- | --- |
| GET /api/v1/profile | Nessuno | Sessione valida; solo UserId, emailConfirmed e mfaVerified |
| POST /api/v1/workspaces | name, requestId UUID | Account verificato, MFA, CSRF; Owner server-side e idempotenza S1-006 |
| POST /api/v1/workspaces/select | workspaceId UUID | Proposta non fidata, membership/ruolo riletti; MFA Owner/Admin; CSRF |
| GET /api/v1/workspace | Nessuno | Membership corrente ammessa |
| GET /api/v1/workspace/settings | Nessuno | Owner/Admin con MFA |
| PUT /api/v1/workspace/settings | name | Owner/Admin con MFA e CSRF |

Workspace/settings rispondono con workspaceId, name, role (nome enum), non entità
EF o principal. L'unico setting modificabile è il nome: 1–256 UTF-16 dopo trim,
senza controlli/carattere sostitutivo; nessun campo Role/UserId/TenantId modificabile.
JSON sconosciuto/duplicato (anche differenze di maiuscole), nullo o non oggetto
rifiutato; body massimo 4096 byte anche senza Content-Length, profondità JSON 8.

Creazione nuova: 201 con workspaceId; replay stessa chiave/soggetto/nome: 200
stesso ID; richiesta incompatibile: 409. Creare non seleziona implicitamente.
Selezione ammessa: 204, principal salvato tramite cookie handler con proprietà e
inizio assoluto della sessione conservati; nessun nuovo login, ruolo nel cookie
o principal serializzato. Un tentativo di selezione negato non cambia selezione.
Una sola selezione per sessione/browser: non promettere workspace separati per tab.

Header/query TenantId non cambiano il contesto. Non esistono route di lettura o
scrittura per ID arbitrario, né elenco globale: /workspace/{id} è 404 per qualsiasi
ID, anche esistente, senza lookup cross-tenant. Per selezione, azienda estranea e
inesistente danno entrambe 403. Sessione valida non è autorizzazione aziendale.

## Trasporto, errori e logging

Cookie/HTTPS/origine/CSRF C/D applicati anche alle nuove route. CSRF tramite
/api/v1/account/antiforgery e header X-CSRF-TOKEN su tutte le mutazioni; niente
mutazioni GET o fiducia in CORS. Cache-Control no-store, Referrer-Policy no-referrer,
X-Content-Type-Options nosniff. Tutti gli errori delle route workspace/profile usano
ProblemDetails con status, titolo generico e correlationId coerente con response/log.
Nessun dettaglio eccezione, nome workspace, password, cookie o connessione nel log.
Eventi sintetici Operation/Outcome, più scope strutturato già esistente.

| Stato | Significato |
| --- | --- |
| 400 | JSON/CSRF/origine/HTTPS/input non ammessi |
| 401 | Sessione assente, scaduta, revocata o account non ammesso |
| 403 | Membership, ruolo o MFA insufficienti |
| 404 | Risorsa scoped assente o route per ID arbitrario non esposta |
| 409 | Selezione necessaria oppure conflitto idempotenza, secondo endpoint |
| 413 / 415 | Body troppo grande / media type non JSON |
| 429 | Quota provisioning esaurita, Retry-After 60 |
| 503 | Dipendenza DB/Redis non disponibile, fail-closed |

Quota provisioning Redis atomica, condivisa tra host: massimo 300/min globale,
60/min origine socket e 10/min UserId, finestre di 60 secondi dal primo evento.
UserId/origine HMAC nei nomi delle chiavi con chiave server già prevista;
namespace separato dal login. Anche replay e tentativi negati dopo input valido
consumano quota. È una baseline anti-abuse interna da tarare sull'ambiente target,
non una quota commerciale, protezione DDoS completa o capacità per 500 MAU provata.

## Prove e limiti

28 test HTTP nuovi con cookie/account/MFA veri, PostgreSQL/Redis temporanei:
profilo/selezione/provisioning/replay, matrice Owner/Admin/Member, ruolo diverso A/B,
IDOR/selezione estranea, contesto ignorante di header/query, input/CSRF/origine/HTTPS,
revoca/role reload, 429 tra host, ProblemDetails/log sanitizzati, preservazione inizio
assoluto nel ticket Redis reale e percorso Kestrel HTTPS. Prima esecuzione fallita
per IUserAccountReader non registrato nell'host: aggiunta registrazione del reader
esistente, nessuna soppressione o mock dell'autenticazione. Test verificati dopo fix.
I 28 test applicativi interni precedenti restano parte della suite, non sostituiti.

Risultati finali, scan e pubblicazione effettiva nel [report](sprint1-progress.md).
La CI storica non verifica queste modifiche finché non viene pubblicato il commit.
Nessuna attivazione su dati utente, UI, email reale, deployment o beta esterna.
Sospensione tenant globale, inviti, cambio ruoli/Owner, delete, quote commerciali,
ETag/conflitti di modifiche lecite al nome e audit segregato/retention sono fuori
questa verticale. Revoche sono verificate al confine di ogni nuova operazione,
non annullano retroattivamente operazioni già ammesse. Restano review umana security,
operatività keyring/email, privacy e SEC-EX-001 prima di qualsiasi accesso esterno.
