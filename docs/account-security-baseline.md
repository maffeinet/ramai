# S1-004A — Account, password e anti-brute-force

Data: 2026-10-19. Componenti interni completati localmente, non login utilizzabile.
Scope autorizzato: ID-01 e primi controlli ID-02 della [policy](identity-security-policy.md).
Nessuna sessione, cookie, endpoint, conferma/reset email, MFA, UI o autorizzazione tenant.
S1-004 complessivo rimane aperto. Nessun commit/push o nuova run GitHub in questo task.

## Componenti e integrazione futura

Tutto il codice operativo è in `Ramai.Modules.Identity.Persistence/AccountSecurity`:
UserManager/IdentityUserContext standard sullo stesso store account di S1-003.
Nessun nuovo schema/migrazione, ruolo globale o accesso a tabelle Tenants.

`AddRamaiAccountSecurity` configura IdentityCore/store, password validator e hasher,
servizio scoped e rate limiter Redis. **Non chiamato da Program.cs**: l'API Foundation
rimane invariata. Non aggiunge SignInManager, cookie scheme, token provider, email
sender o middleware di sessione. I test usano questo composition point direttamente.
La nuova PackageReference StackExchange.Redis **3.3.1** riusa la versione già nella
soluzione; Identity/EF **10.0.12** e Npgsql/provider **10.0.3** invariati.
Non è una nuova infrastruttura rispetto a Redis/ADR-009 già approvati.

RegisterAsync riceve email/password e contesto server, delega CreateAsync allo store
standard; account attivo ma EmailConfirmed=false. Richieste duplicate valide e nuove
restituiscono lo stesso Accepted senza UserId; password non valida restituisce un
rifiuto generico indipendente dall'esistenza dell'account. Nessun account seed reale.

CheckCredentialsAsync è un controllo **interno**, non un login: Accepted/UserId solo
per password corretta, account attivo, email già confermata e lockout non attivo.
Non produce principal, cookie, token, completamento MFA o permessi di tenant.
La conferma nei test è solo preparazione di fixture sintetiche; non esiste un metodo
pubblico per saltare ID-03. ID-03/04/05 vanno implementati nei blocchi successivi.

## Password e hash

- 15–128 valori scalari Unicode dopo normalizzazione NFC; non byte, unità UTF-16 o
  grafemi visivi. UTF-16 malformato rifiutato. Nessun trim, case folding nell'hash,
  troncamento, requisito di simboli/maiuscole o rotazione a calendario.
- Passphrase, spazi e caratteri supplementari consentiti. La normalizzazione NFC è
  applicata coerentemente in validazione/hash/verifica tramite un adapter; crittografia,
  salt e verifica restano nel PasswordHasher standard, non riscritti.
- IdentityV3 PBKDF2-HMAC-SHA512, salt 128 bit e subkey 256 bit del framework; costo
  esplicito **220.000 iterazioni**. Header/formato, salt diverso, password errata,
  equivalenza NFC e rehash da 100.000 verificati. Hash corrotto in Base64 rifiutato.
- Microbenchmark locale, 7 campioni dopo warmup: mediana hash **71,0 ms**, verifica
  **71,6 ms** (SDK 10.0.402/runtime 10.0.13). Misura indicativa su questa macchina,
  non capacità per 500 MAU, benchmark cloud o garanzia sotto carico.

Fonti tecniche consultate il 19 ottobre:
[implementazione Identity 10.0.12](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/PasswordHasher.cs),
[default costo 100.000](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/PasswordHasherOptions.cs),
[riferimento OWASP per costo SHA512 e misurazione](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
Il costo va rimisurato sull'hosting scelto prima della beta, senza ridurlo automaticamente
per superare un test di performance.

## Denylist offline e provenienza

Corpus pubblico SecLists/NCSC, **99.839 voci non vuote** alla revisione
`1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56`, MIT. Fonte e licenza:
[corpus fissato](https://github.com/danielmiessler/SecLists/blob/1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56/Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt),
[licenza fissata](https://github.com/danielmiessler/SecLists/blob/1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56/LICENSE).
Avviso MIT completo conservato in Data/SecLists-LICENSE.txt e copiato negli output/publish.

`scripts/Update-PasswordBlocklist.ps1` scarica SOLO queste risorse pubbliche, verifica
checksum prima delle scritture e genera in modo deterministico il dataset derivato:

- corpus sorgente SHA256 `C2E5696882C603B76BB67A47EE970897E5A76FC4C3F5547ABE3D0CA340C576E0`;
- **329 digest unici**, relativi alle voci NFC di 15–128 scalari compatibili con la policy;
  quelle più corte sono già rifiutate per lunghezza, non password dimenticate dal filtro;
- confronto denylist case-insensitive, ma hash delle credenziali sempre case-sensitive;
- dataset SHA256 `52732C0E7165FBEC2A1371ED1A9E01B5232D11F84D7E91C06853F0C0A531C8DC`,
  verificato nei test; line ending LF fissato per .sha256;
- rifiuto aggiuntivo di ripetizioni di un unico scalare, riferimenti a RAMAI e local-part
  email (se almeno 4 caratteri) contenuti nella password, come valori attesi/contestuali.

I digest SHA256 servono SOLO a consultare il corpus pubblico, **non** ad archiviare
credenziali. Nessuna password utente inviata a servizi esterni o scritta dal generatore.
Il servizio runtime non usa la rete per consultare la lista. Assenza/corruzione del
dataset genera errore, non fallback senza controllo. Aggiornamento della revisione
richiede review, checksum e test nuovi, non download automatico all'avvio.

Copertura limitata a questo corpus comune/compromesso noto: non è un controllo su tutti
i breach, non una prova di password sicura. Prima della beta occorre riesaminare corpus,
aggiornamento e policy contestuale; nessun servizio di reputazione/password acquistato.

## Lockout, rate limit e concorrenza

Standard Identity: **5 errori consecutivi → 15 minuti**, consentito per nuovi utenti.
Account con LockoutEnabled=false rifiutati fail-closed, non esentati dalla protezione;
eventuali vecchi account richiedono abilitazione controllata, non bypass implicito.
Anche tentativi con password troppo corta contano come fallimenti. Verifica corretta
azzera il contatore; scadenza testata con LockoutEnd sintetico nel passato, senza
attendere 15 minuti o inventare uno sblocco pubblico. Nessun blocco permanente.

Un lock PostgreSQL FOR UPDATE sulla sola riga Identity serializza verifica/rehash e
incrementi fra scope/istanze. Query parametrizzata; transazione e store standard;
nessun lock globale utenti o flag Redis sostitutivo del contatore autorevole DB.
Refresh della riga già tracciata evita contatori/stato obsoleti nello scope.
Il servizio deve essere scoped, non condiviso contemporaneamente fra richieste.

Rate policy interna, **finestra fissa TTL 60 secondi**, distinta Register/CheckCredentials:

| Dimensione | Budget per finestra |
| --- | --- |
| Operazione globale, condivisa fra istanze | 300 |
| Origine server, condivisa fra account | 120 |
| Account normalizzato, condiviso fra origini | 10 |

Lua atomico sul Redis già previsto, nessuna coda o fallback in memoria. La quota
globale è valutata per prima, limitando anche la cardinalità delle nuove chiavi.
Fixed window consente burst a cavallo della scadenza: non dichiarato sliding window.
Le quote non si azzerano dopo una password valida: indipendenti dal contatore Identity.
Origine più permissiva dell'account per ridurre effetti NAT; resta una soglia iniziale
da tarare con traffico legittimo, non una garanzia contro DoS o assenza di falsi positivi.

`AccountRequestContext` richiede IP della connessione verificata e correlation ID
validato. IPv4-mapped IPv6 canonicalizzato. Il futuro adapter HTTP NON deve fidarsi di
IP/header/client arbitrari: proxy fidati e policy endpoint/trasporto vanno integrati
e testati quando esisteranno endpoint. Qui nessun controller è protetto o creato.

La registrazione richiede chiave HMAC casuale di almeno 32 byte fornita dal chiamante,
uguale fra istanze, custodita fuori repo/log e separata dalle password utente. Nei test
è generata in memoria. Nessun default, `.env` nuovo o configurazione host implicita;
gestione/rotazione del secret e namespace stabili sono prerequisiti dell'integrazione.
Chiavi Redis di account/origine pseudonimizzate con HMAC, non email/IP in chiaro.
La rotazione cambia partizioni e richiede pianificazione, non improvvisazione in deploy.

## Esiti, log e guasti

Account assente, password errata, account disattivato, bloccato o non confermato hanno
medesimo rifiuto pubblico, senza UserId. Throttled ha messaggio generico e RetryAfter
60 secondi, anche per account inesistente. Redis/DB indisponibili → Unavailable,
mai accesso o rate limit aggirato; eccezioni transitorie Npgsql wrappate gestite.

Hash dummy allo stesso costo per assenti/bloccati/disabilitati riduce il percorso
veloce, ma query/lock/counter diversi restano osservabili: **non constant time**.
Confronto timing sintetico raccolto nei test, non prova statistica anti-enumerazione.
Restano da verificare HTTP, traffico reale e timing sotto carico prima della beta.

Eventi strutturati allowlist operation/outcome con correlation ID nello scope;
nessuna email/IP/password/hash, connessione o eccezione del servizio nei messaggi.
SensitiveDataLogging EF esplicitamente disabilitato. Test catturano anche log framework
e scope; pipeline log host JSON/timestamp/environment resta quella della Foundation.
Audit segregato, retention e governance ID-07 non sono sostituiti da questi eventi.

## Verifica e limiti di rilascio

Test ordinari: policy Unicode, dataset, hash/rehash, benchmark e contesto server.
Test opt-in con PostgreSQL/Redis reali: creazione/duplicati, email non confermata,
progressione/scadenza/reset lockout, concorrenza, throttle condiviso, TTL e limiti
globali/origine/account, guasti DB/Redis, esiti/log e confronto timing.
Solo database `ramai_s1test_<GUID>` e namespace Redis propri del test, mai dati cliente,
FLUSHDB o reset volumi esistenti. Risultati nel [report](sprint1-progress.md).

Questo blocco non completa S1-004, non autorizza la beta e non è attestazione GDPR/NIS2.
SEC-EX-001 non copre auth o questi limiti e non viene estesa. Nessun task successivo iniziato.
