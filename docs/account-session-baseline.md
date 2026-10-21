# S1-004C — Login, logout, sessioni e revoca

Aggiornamento successivo: [S1-004D](account-mfa-baseline.md) aggiunge MFA TOTP/recovery
al login e una prova MFA server-side nel principal. Le esclusioni MFA di questo
documento descrivono il checkpoint C, non lo stato locale successivo a D.

Data: 2026-10-20. Scope: API identità e sessioni first-party, non UI/MFA o accesso
ai dati aziendali. Riferimenti: [policy ID-05](identity-security-policy.md),
[ADR-010](adr/ADR-010-api-composition-root.md), [report](sprint1-progress.md).
Il ripristino del piano C è autorizzato dall'utente dopo il chiarimento security;
nessuna autorizzazione a commit/push, acquisti o pubblicazione esterna in questo blocco.

## Contratti e confini

API → contratti Application → servizi nell'adapter Identity. Solo Composition
referenzia i tipi dell'adapter; nessun accesso HTTP a DbContext/UserManager,
nessun accesso a tabelle Tenants. Un test verifica l'eccezione del solo composition
root, conservando tutti i divieti tra moduli. Nessun nuovo schema/migrazione,
pacchetto NuGet, servizio di autenticazione esterno o nuova infrastruttura.

Login verifica tramite il servizio A le credenziali, conferma email, attività e
lockout. SecurityStamp viene catturato nello stesso stato verificato sotto row lock:
un reset successivo non può far ottenere alle vecchie credenziali lo stamp nuovo.
L'esito interno non stampa lo stamp in ToString; l'API non serializza il principal.
Account MFA-enabled rifiutato dalla via password-only: nessun bypass fino a D.

Principal minimo: UserId globale, SecurityStamp, istante iniziale. Nessuna email,
membership, ruolo globale, TenantId o prova MFA. Questo principal NON conferisce
accesso tenant-scoped; TenantContext/autorizzazione sono i task successivi.

## Endpoint opt-in

| Metodo/percorso | Comportamento |
| --- | --- |
| GET /api/v1/account/antiforgery | Cookie antiforgery HttpOnly e requestToken JSON, non un bearer token di login |
| POST /api/v1/account/login | JSON email/password e X-CSRF-TOKEN; 204 oppure rifiuto generico 401, 429/Retry-After o 503 |
| GET /api/v1/account/session | UserId globale della sessione; 401 se assente/non valida |
| POST /api/v1/account/logout | Richiede sessione e antiforgery; revoca ticket corrente ed elimina cookie |
| POST /api/v1/account/revoke-all | Richiede sessione e antiforgery; cambia SecurityStamp dell'utente corrente e logout |

Nessun parametro libero UserId/TenantId/Role nei comandi di revoca. Nessun redirect
o return URL accettato/eseguito. Login su sessione autenticata rifiutato (409),
non cambio account silenzioso. Richiesta JSON richiesta, corpo massimo 4096 byte.
HTTPS obbligatorio sulle route account; Origin, se presente, deve coincidere con
l'origine configurata. Nessun CORS permissivo o fiducia in X-Forwarded-For/Proto.
Un futuro reverse proxy dovrà essere configurato e testato come proxy fidato:
non si può fingere TLS mandando un header dal client.

Risposte account no-store/no-referrer/nosniff. Antiforgery applicato anche al login
per prevenire login CSRF e a tutte le mutazioni esposte. Sessione cookie
__Host-Ramai.Session: Secure, HttpOnly, Path=/, nessun Domain, SameSite=Strict,
non persistente nel browser. Nessun access token in localStorage o nelle URL.

## Ticket server-side, scadenze e revoca

ITicketStore standard; chiave casuale 256 bit, cookie protetto Data Protection con
solo il riferimento. Ticket Redis cifrato/autenticato da un protector dedicato,
namespace per ambiente e TTL limitato alla sessione. Nessun ticket in chiaro in Redis.
30 minuti idle, rinnovo a ogni richiesta valida; massimo assoluto 12 ore dall'inizio,
mai esteso dal rinnovo. Store/renew cap TTL al limite assoluto. Renew usa Redis XX
atomico: non ricrea un ticket eliminato/scaduto durante logout concorrente.

Ogni richiesta con cookie valido rilegge lo stato account da PostgreSQL senza cache
o entità tracciata: attività, conferma, lockout, flag MFA e SecurityStamp. Reset,
disattivazione e revoke-all invalidano le richieste successive alla verifica DB
della revoca committata, non dopo un intervallo predefinito di 30 minuti.
Le richieste già ammesse prima del commit non vengono cancellate retroattivamente;
operazioni business future dovranno verificare autorizzazioni al proprio confine.
Offboarding/cambio ruolo tenant non è ancora integrato: non esiste TenantContext.

DB/Redis indisponibili: errore generico 503, nessun fallback locale o accesso.
Cookie/ticket alterati o con altro keyring: nessuna sessione valida. Correlation ID
preservato anche negli errori. Nessuna eccezione o connection string nella risposta.
Rate distribuito A preservato: quota globale 300/min, origine 120/min, account 10/min,
lockout 5 errori/15 minuti. Origine dalla connessione, non header client.

## Configurazione esplicita

Senza RAMAI_AUTH_ENABLED=true gli endpoint non vengono mappati; Foundation e /health
restano utilizzabili con le istruzioni precedenti. Non abilitiamo auth con secret
dimostrativi o keyring automatico nel checkout. Configurazione tramite ambiente
processo/secret provider dell'host, mai commit o log dei valori sensibili:

| Chiave | Requisito |
| --- | --- |
| RAMAI_AUTH_ENABLED | true solo con configurazione completa |
| RAMAI_AUTH_ORIGIN | Origine HTTPS first-party, senza path/query/credenziali |
| RAMAI_AUTH_NAMESPACE | 1–58 caratteri ASCII alfanumerici/trattino, stabile e distinto tra ambienti |
| RAMAI_AUTH_RATE_KEY | Base64 di almeno 32 byte casuali, identica tra istanze; secret separato |
| RAMAI_AUTH_KEY_DIRECTORY | Percorso assoluto persistente e con ACL riservate al processo |
| RAMAI_AUTH_CERTIFICATE_PATH | PFX assoluto con chiave privata, valido temporalmente; fuori repo |
| RAMAI_AUTH_CERTIFICATE_PASSWORD | Secret del PFX, fuori repo/log |
| RAMAI_DB_* / RAMAI_REDIS_* | Connessioni già previste dalla Foundation |

Il loader .env Foundation ha un'allowlist: NON aggiungere queste nuove chiavi al suo
.env attendendosi un import automatico. Configurarle esplicitamente nell'ambiente
del processo, e ASPNETCORE_URLS HTTPS con certificato TLS valido dell'host. Il PFX
Data Protection è una chiave di cifratura del keyring, non configura automaticamente
TLS. Nessuna generazione/installazione di certificati production eseguita qui.
Le migrazioni sono esplicite, mai automatiche all'avvio dell'API.

Keyring su filesystem persistente, application discriminator distinto per namespace,
cifrato con certificato; nessun provider effimero nell'host auth. Protezione ACL,
backup/ripristino, rotazione con conservazione dei certificati precedenti e deploy
multi-istanza reali richiedono runbook e prova sull'ambiente scelto prima della beta.
Un restart su stesso keyring è testato, non equivale a disaster recovery completa.

## Verifica e limiti

Test HTTP con pipeline reale e DB/Redis reali; nessun handler autenticazione finto.
TestServer fornisce soltanto metadati socket sintetici; include prove di sessione,
CSRF, origine, manomissione, rate limit account/origine e guasti. Clock controllato
solo nei test per 30 minuti idle/12 ore assolute, senza attendere ore.
Prova aggiuntiva Kestrel su socket HTTPS reale locale: health → login → session →
logout → 401. Certificato test appena generato e fingerprint/hostname verificati,
nessun AcceptAnyCertificate o modifica del trust store. Su Windows il certificato
TLS test usa DefaultKeySet per compatibilità SChannel, poi viene disposed.
Cartelle keyring/PFX, database, chiavi Redis e volumi dedicati di test rimossi;
nessuna eliminazione di dati o volumi utente esistenti.

Non implementati: S1-004D MFA/enrollment/recovery, UI/browser E2E, TenantContext,
ruoli e isolamento business, trasporto HTTP registrazione/conferma/reset, email reale,
audit segregato/retention e deployment pubblico. I log strutturati/correlation non
sostituiscono il sistema completo di audit ID-07. La demo non è ancora utilizzabile
dagli stakeholder come beta autonoma. Nessuna attestazione GDPR/NIS2.
SEC-EX-001 resta aperta senza proroga; tooling frontend invariato.

Fonti tecniche: [ITicketStore](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.iticketstore?view=aspnetcore-10.0),
[cookie e ValidatePrincipal](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0),
[Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).
