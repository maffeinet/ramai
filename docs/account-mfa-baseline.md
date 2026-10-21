# S1-004D — MFA TOTP e recupero

Data: 2026-10-20, Europe/Rome. Autorizzazione esplicita: procedere esclusivamente
con S1-004D, seguito da ripresa del lavoro e richiesta di rileggere/documentare.
Implementazione locale; nessun commit/push o autorizzazione alla beta.
Riferimenti: [policy ID-04](identity-security-policy.md),
[sessioni C](account-session-baseline.md), [report](sprint1-progress.md).

## Perimetro e dipendenze

API → contratti Application → adapter Identity, come ADR-010. Nessun accesso al
modulo Tenants, nessun TenantId/ruolo dal client. Nessuna nuova dipendenza NuGet,
servizio, tabella o migrazione: UserTokens e TwoFactorEnabled di Identity esistenti.
TOTP e recovery code generati/verificati con UserManager e provider Authenticator
standard ASP.NET Core Identity; niente algoritmo TOTP applicativo personalizzato.
Il generatore RFC 6238 indipendente esiste soltanto nel client dei test.

Frontend/tooling e CI non modificati. ID-04 è implementato come componente globale
account: l'obbligo Owner/Admin e il rifiuto di privilegi aziendali senza prova MFA
restano da integrare nei blocchi TenantContext/RBAC, prima di una beta.
Nessuna attestazione NIS2/GDPR o resistenza al phishing.

## Contratto HTTP e lifecycle

Tutte le route account mantengono HTTPS, Origin first-party, no-store/no-referrer,
limite body 4096 byte, correlation ID e antiforgery. Mutazioni MFA richiedono sessione
autenticata e **password corrente verificata nuovamente**; non accettano UserId,
TenantId, ruolo o secret scelto dal client. Nessun endpoint GET restituisce secret.

| Metodo/percorso | Input e risultato |
| --- | --- |
| POST /api/v1/account/mfa/enrollment | password; se MFA già attiva anche code oppure recoveryCode. Restituisce sharedKey, issuer Agentilio e expiresInSeconds=600 |
| POST /api/v1/account/mfa/confirm | password + code del nuovo autenticatore. Restituisce dieci recoveryCodes una sola volta e termina la sessione |
| POST /api/v1/account/mfa/recovery-codes | password + code oppure recoveryCode corrente. Sostituisce tutto il set e termina la sessione |
| POST /api/v1/account/login | email/password; se MFA attiva richiede anche code oppure recoveryCode. 204 solo dopo verifica completa |

Dopo un login recovery riuscito, una prova opaca **server-side** permette una sola
operazione BeginEnrollment oppure RegenerateRecoveryCodes entro cinque minuti,
sempre con password nuovamente verificata. È un claim nel ticket Redis cifrato,
non un token esposto nell'API; il digest/stamp/scadenza nello stato cifrato vengono
verificati e consumati sotto lo stesso lock DB. Non è un remember-device o una
sessione ottenibile con password soltanto. Permette di usare anche l'ultimo recovery
code rimasto per ripristinare il dispositivo. Dopo scadenza o consumo occorre un
altro fattore valido; una seconda operazione non può riutilizzare la prova.

`code` è una stringa di esattamente sei cifre ASCII (zeri iniziali preservati).
`recoveryCode` deve corrispondere esattamente a uno dei codici consegnati: nessun
troncamento/normalizzazione implicita. Presentare entrambi è rifiutato. Nessun
remember-device o percorso password-only per account MFA-enabled. Fallimenti login
generici 401, quota/lock MFA 429, guasti dipendenze o materiale cifrato 503.
Gestione MFA con prova invalida: 400; senza sessione 401; CSRF/origine errati 400.

L'enrollment genera una chiave casuale server-side, conservata come pending per
dieci minuti e legata al SecurityStamp corrente. Una nuova richiesta sostituisce
il pending precedente. Password reset/revoca rendono inutilizzabile un pending
legato al vecchio stamp, anche dopo nuovo login. La chiave attiva precedente resta
valida fino alla conferma riuscita della nuova. Conferma errata ripristina la chiave
precedente nella stessa transazione e registra il fallimento, non abilita MFA.

Il secret è necessariamente consegnato al titolare autenticato/ri-autenticato
per importarlo in un'app autenticatore. Non è un bearer token e non è pubblicato
anonimamente: solo POST HTTPS non cacheabile, mai URL, log, analytics o servizio QR
esterno. UI/QR locale e istruzioni browser verranno implementati nel blocco UI.
Recovery codes mostrati solo in emissione: conservarli offline in luogo protetto;
non esiste endpoint per ristampare il set corrente.

## Protezione, replay e concorrenza

Lo store EF standard di Identity non cifra automaticamente i valori UserTokens.
ProtectedIdentityTokenStore aggiunge Data Protection autenticata a AuthenticatorKey,
RecoveryCodes e stato RAMAI.Mfa.v1. Purpose distinti per UserId, provider e slot,
oltre al namespace del keyring C. Gli altri slot (digest monouso B) conservano il
formato precedente. Nessun fallback plaintext per MFA: vecchio materiale non protetto
richiede una procedura controllata, non decifratura permissiva automatica.
Il keyring/certificato persistente resta quello esplicitamente richiesto da C;
backup/ACL/rotazione e restore dell'ambiente reale restano gate operativi.

Ogni operazione MFA acquisisce FOR UPDATE sulla propria riga Users e rilegge utente
e token sotto lock. Consumo recovery, modifica secret, contatori, stamp e marker replay
sono committati insieme. Due istanze concorrenti non possono spendere lo stesso codice.
I codici recovery sono quelli standard Identity, cifrati a riposo e consumati una volta.

Il provider standard accetta step TOTP da 30 secondi con tolleranza di due step per
direzione. RAMAI registra il digest dei TOTP accettati nello stato cifrato per tre
minuti, oltre l'intera finestra di possibile accettazione: lo stesso valore non è
riutilizzabile nella finestra, neppure per enrollment/login/gestione o su altra istanza.
Dopo la conferma iniziale occorre quindi un codice nuovo per il login: la UI dovrà
chiedere di attendere il cambio del codice, non ripresentare quello appena consumato.
Clock di sistema sincronizzato è requisito operativo. TimeProvider è usato per
pending/lock/replay/sessioni; il provider TOTP standard verifica il tempo UTC reale,
non il clock sintetico dei test. I test di scadenza generano codici fuori finestra.

Rate distribuito Redis già previsto: budget globale 300/min e origine 120/min condivisi
con CheckCredentials; budget account MFA distinto, 10/min. Un login MFA usa entrambe
le verifiche e consuma due unità globali/origine; taratura prestazioni prima dell'esterno.
In più: cinque fallimenti MFA consecutivi bloccano le verifiche MFA per 15 minuti,
stato persistente indipendente dal reset del contatore password. La password corretta
non cancella il lock MFA. Successo azzera i fallimenti; non esiste blocco permanente.
Il lock MFA non è un comando di disabilitazione delle sessioni già emesse.

Conferma enrollment, sostituzione recovery e consumo di un recovery code cambiano
SecurityStamp. Le sessioni precedenti sono rifiutate alla successiva verifica DB.
Il login recovery emette una nuova sessione con prova MFA e il nuovo stamp: non
disabilita TwoFactorEnabled e non bypassa il fattore nei login successivi.
Anche l'uso di recovery per autorizzare una rotazione revoca le sessioni precedenti:
BeginEnrollment restituisce anche requiresSignIn=true e termina la sessione.
Occorre un nuovo login password + altro recovery valido prima della conferma del
nuovo autenticatore. Quel login può rilegare al nuovo stamp soltanto un pending
ancora valido e legato allo stamp che il login stesso ha appena verificato; un
pending invalidato da reset/revoca intervenuti non è recuperabile in questo modo.
Non si effettua SignOut seguito da SignIn nella stessa richiesta: l'handler cookie
mantiene la vecchia chiave store e il Renew XX di C non deve resuscitarla.
È una scelta conservativa con più passaggi; la UI dovrà spiegarla esplicitamente.

Se dispositivo e recovery sono entrambi persi, nessun reset MFA via sola email,
password o operatore non verificato. Il recupero assistito è volutamente non esposto;
procedura/controlli umani da approvare prima degli utenti esterni. La rotazione con
password + recovery permette di sostituire un dispositivo perso senza downgrade.

## Verifiche e limiti

Test HTTP con pipeline cookie reale, PostgreSQL/Redis dedicati, keyring protetto
e due host per le corse di consumo. Nessun handler auth simulato. Copertura:
enrollment, riemissione/scadenza, password/CSRF/origine, TOTP fuori finestra/replay,
recovery singolo/concorrente, rigenerazione/rotazione, revoca e assenza secret nei log,
rate/lock separato e keyring errato/materiale cifrato corrotto. Gate e conteggi finali
sono registrati nel report: suite finale **146 pass/0 skipped**, di cui 19 nuovi
casi MFA, con percorso MFA anche su Kestrel HTTPS reale. Build Release 0 warning/
errori, format e gate frontend PASS; Gitleaks sui 32 file locali senza leak.

Restano fuori scope: TenantContext/isolation/RBAC e obbligo MFA per ruolo, UI/browser
E2E, email reale e HTTP self-service completo, assistenza recupero con tutti i fattori
persi, audit segregato/retention, deployment e review security umana. Demo/accesso
sicuro complessivo non pronti per esterni. SEC-EX-001 resta aperta senza proroga.

Fonti: [MFA ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0),
[provider Authenticator](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Identity/Extensions.Core/src/AuthenticatorTokenProvider.cs),
[store Identity e recovery](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Identity/Extensions.Stores/src/UserStoreBase.cs).
