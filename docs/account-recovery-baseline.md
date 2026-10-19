# S1-004B — Conferma email e recupero password

Data: 2026-10-19. Componenti interni sullo stesso store Identity, non endpoint o UI.
Riferimenti: [policy ID-03](identity-security-policy.md),
[baseline A](account-security-baseline.md), [report](sprint1-progress.md).
Il blocco autorizza soltanto conferma/reset e relativi test; sessioni/MFA non implementate.
L'utente ha autorizzato la pubblicazione A+B dopo il completamento dei gate locali.

## Provider standard e confini

DataProtectorTokenProvider Identity, con due provider separati:

| Operazione | Provider/protector | Durata |
| --- | --- | --- |
| Conferma email | Ramai.EmailConfirmation.v1 | 24 ore |
| Reset password | Ramai.PasswordReset.v1 | 30 minuti |

UserManager genera/verifica i token e aggiorna email/password/SecurityStamp con le
API standard. Nessun protocollo, cifrario o token password-reset costruito da zero.
I due provider Data Protection non generano codici MFA. Non vengono chiamati
AddDefaultTokenProviders, AddSignInManager o registrazioni cookie/sessioni.
Fonte di riferimento del formato e dei controlli:
[Identity DataProtectorTokenProvider 10.0.12](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Core/src/DataProtectorTokenProvider.cs).

AddRamaiAccountRecovery richiede esplicitamente IDataProtectionProvider, sender e
origine HTTPS, dopo AddRamaiAccountSecurity. Nessun keyring o sender predefinito,
nessuna directory chiavi creata. Program.cs e API rimangono invariati.
Nuova sola FrameworkReference Microsoft.AspNetCore.App nel proprio adapter;
nessun nuovo pacchetto NuGet o infrastruttura esterna.

Nei test provider effimero condiviso fra scope e sender in memoria **solo nel progetto
test**, senza console, file email, API inbox o contatto con SMTP. Prima di utenti reali
occorrono keyring persistente/protetto con application isolation e recovery verificate,
gestione rotazione e trasporto email affidabile approvati separatamente. Chiavi effimere
non sono un'opzione production: restart/ripartenza con altro keyring invalida i link.

## Emissione, sostituzione e consumo

- Account attivi con lockout abilitato; conferma soltanto se email non confermata;
  reset soltanto per email già confermata e password presente. Mai auto-conferma.
- Richiesta per account assente, inattivo o non idoneo: stesso Accepted, senza UserId,
  token o prova di invio nella risposta. Un nuovo account non riceve implicitamente
  una mail: il futuro flusso applicativo dovrà comporre esplicitamente la richiesta.
- Per ogni utente/scopo è valido solo **l'ultimo link emesso**. Nella UserTokens già
  esistente si salva SHA256 del token standard opaco, non il token o il link.
  LoginProvider Ramai.AccountRecovery.v1, Name EmailConfirmation/PasswordReset.
  Il digest è un marker di revoca, non un formato di token alternativo.
- Il provider standard verifica scopo, utente, scadenza e SecurityStamp; il servizio
  verifica anche il marker corrente. Due scopi hanno marker/protector separati.
- Emissione e consumo sotto lock della propria riga Users e transazione PostgreSQL;
  marker e modifica account committati atomicamente. Nessun accesso cross-module.
- Reset riuscito cambia hash/SecurityStamp, consuma il marker e impedisce replay;
  cambia password con policy A ancora applicata. Nuove credenziali errate/non conformi
  non consumano il link valido. Conferma riuscita consuma il marker e replay rifiutato.
- Password cambiata tramite il framework invalida i token precedenti tramite stamp.
  Reset non riattiva account, non conferma automaticamente email e non azzera lockout:
  l'eventuale blocco temporaneo A rimane fino alla scadenza. Nessuna sessione emessa.
- Aggiornare SecurityStamp **non prova revoca immediata di sessioni**: adapter/check
  server-side e relativo test restano S1-004C, non simulati qui.

I marker obsoleti/scaduti non conferiscono accesso: validazione crittografica e tempo
restano necessari. La nuova emissione sovrascrive il marker; consumo riuscito lo elimina.
Non è stato introdotto un job di retention, né un outbox/worker o schema nuovo.

## Invio e indisponibilità

Il sender viene invocato dopo il commit, senza tenere il row lock durante l'invio.
Fallimento sender: log sintetico, mai eccezione/link/email nei messaggi, stessa risposta
Accepted di account assente. Il marker resta corrente; si può richiedere un nuovo link
entro le quote. Nessun rollback che ripristini un vecchio link o retry email automatico.

Non esiste atomicità DB/email o consegna garantita: trasporto reale, monitoraggio di
consegna/bounce e recupero da guasti sono gate pre-beta. Un eventuale outbox/nuovo
servizio richiede piano/approvazione, non viene aggiunto implicitamente.
Guasti DB/Redis restituiscono Unavailable senza dettagli, mai conferma/reset riuscito.

## Quote distribuite e risposte

Il limiter A resta compatibile con le quote originali Register/CheckCredentials.
Nuove partizioni Redis HMAC per operazioni recovery, Lua atomico:

| Operazioni | Globale | Origine server | Account |
| --- | --- | --- | --- |
| Richiesta conferma o reset, ciascuna separata | 60/min | 20/min | 3/15 minuti |
| Consumo conferma o reset, ciascuno separato | 120/min | 60/min | 5/15 minuti |

Per richiesta account key = email normalizzata; per consumo = UserId globale canonico,
mai TenantId. Quote applicate anche a identità inesistenti. RetryAfter recovery fisso
conservativo 900 secondi, indipendente da esistenza/account o dimensione saturata;
non promette che il limite globale/origine duri effettivamente 15 minuti.
Le quote non sostituiscono body limit, proxy fidati, antiforgery e mapping HTTP futuri.

## Link, redirect e minimizzazione

Origine HTTPS server configurata: no credenziali/path/query/fragment. Destinazioni
fisse /account/confirm-email e /account/reset-password, soltanto come dati del messaggio:
**quelle pagine/route non sono create da B**. Query con UserId, code Base64Url del token
Identity e returnPath locale; nessuna email. Code/URL bearer sono dati sensibili.

ReturnPath accetta solo path root-relative canonico (max 256), caratteri ASCII
alfanumerici, slash, trattino, underscore e punto; rifiuta // iniziale, backslash,
escape %, query, fragment, controlli, schemi e segmenti `.` o `..`. Nessun redirect eseguito
dal servizio. Il futuro handler dovrà riapplicare la stessa validazione al ritorno,
senza fidarsi di query ripresentate dal browser.

Code input limitato a 8192 caratteri Base64Url, UTF-8 valido; niente decode arbitrari
o dettagli di parsing nelle risposte. AccountRecoveryMessage non è record e ToString
non contiene recipient/link. Nessun token torna nel risultato del servizio.
Eventi operation/outcome con correlation ID; log delle query protetti dal già attivo
SensitiveDataLogging=false. Test catturano messaggi, eccezioni e scope anche framework.

Prima di esporre HTTP: evitare token in access log, analytics e referrer, usare pagine
senza tracking e policy referrer appropriate. B non dichiara risolta la configurazione
di proxy/CDN/email reali ancora non scelti. Timing/DoS e soglie da tarare sul target;
risposta generica non è una prova di eliminazione di tutti i canali temporali.

## Prove

Suite reale PostgreSQL/Redis: durata separata 24h/30min, conferma, reset/policy,
purpose/utente/keyring diversi, token alterati, replay, scambio credenziali, reissue
concorrente e due reset concorrenti con esattamente un vincitore, log minimizzati,
sender guasto, DB/Redis indisponibili e rate generico per account esistente/assente.

Le prove di scadenza preparano **solo nei test** payload standard protetti con timestamp
nel passato: 23h valido/25h scaduto per conferma e 29min valido/31min scaduto per reset.
Non modificano scadenze di produzione, non aspettano ore e non sostituiscono il
provider di produzione con un token validator finto. Provider/marker usati normalmente
nei flussi ordinari restano quelli reali. Test link/redirect eseguibili anche senza DB.
Risultati locali e GitHub sono distinti nel report; nessun dato reale usato.
