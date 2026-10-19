# ADR-009 — Identità locale per RAMAI SaaS

Status: **Accepted per il modello locale e i parametri tecnici S1-001 — non implementato**.
Data analisi: 2026-10-16, Europe/Rome. Task: S1-001 (solo analisi documentale).
Approvazione tecnica: utente del progetto, 2026-10-17: "ok parametri tecnici S1-001 approvati".
Ambito: proposta locale corrente e baseline ID-01–07. Il confronto managed archiviato
non è approvato; validazioni normative, scelte operative future e implementazione restano separate.

Il numero 008 non è utilizzato qui: il documento architetturale 04 lo cita per
"No autonomous write actions", anche se quel file ADR non esiste ancora nel repository.
Non viene creata o modificata implicitamente quella decisione.

## Direzione corrente: identità locale, non CIAM obbligatorio

L'utente chiede di partire senza Entra External ID o altro CIAM per validare il mercato
prima di assumere costi e dipendenze aggiuntive. SaaS e fino a 500 MAU iniziali restano
confermati. La precedente proposta managed, mai approvata, è superata per l'MVP;
è conservata sotto soltanto come confronto storico, non come decisione corrente.

**Proposta attuale: ASP.NET Core Identity nel modulo Identity del monolite,
PostgreSQL tramite EF Core, autenticazione web a cookie protetti.** Tenant, membership
e ruoli restano applicativi RAMAI. Nessun microservizio identità separato, protocollo
JWT/password hashing creato da zero o token server general-purpose.

ASP.NET Core Identity fornisce componenti standard per utenti, hash password,
conferme/reset e lockout, ma configurazione, sessioni e sicurezza restano nostre
responsabilità ([documentazione](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)).
Non equivale a Entra o a un authorization server OIDC. Il primo client è il web
first-party con cookie; bearer token proprietari Identity non vengono adottati come
surrogato di OIDC/OAuth. Le specifiche citano sia locale sia OIDC: il perimetro
cookie-only del primo client web è compreso nell'approvazione tecnica corrente;
non viene dichiarato implementato OIDC o un server di federazione.

### Single user, multi user, team e mono/multi azienda — indirizzo confermato

Input utente: valutare single user, multi user e team, con mono o multi azienda.
Non sono interpretati come sei applicazioni o sei diversi schemi database.
Sono due dimensioni distinte: persone per azienda e aziende accessibili all'utente.
L'utente ha approvato la raccomandazione: collaborazione nella stessa azienda, account
personali e predisposizione al multi-azienda. Questa conferma riguarda il modello di
prodotto, non tutti i dettagli di sessione/sicurezza o l'eccezione npm.

| Scenario | Utenti per azienda | Aziende accessibili allo stesso account |
| --- | --- | --- |
| Solo mono-azienda | 1 | 1 |
| Collaborazione mono-azienda | Più utenti con credenziali individuali | 1 |
| Solo multi-azienda | Anche 1 per ciascuna azienda | Più aziende autorizzate |
| Collaborazione multi-azienda | Più utenti per ciascuna azienda | Più aziende autorizzate |

Un account User globale, un Tenant per workspace aziendale e una Membership per
coppia UserId/TenantId. Ogni membership ha ruolo/stato propri: Owner in A non implica
Owner o accesso in B. Il creator solo è comunque membro Owner del proprio tenant;
non esiste un percorso single-user senza TenantId che poi richiede una migrazione.
Le aziende condividono l'infrastruttura, non i dati applicativi. Il cliente mono-azienda
non implica un'installazione single-tenant separata della piattaforma SaaS.

Membership multi-azienda prevista nel modello; scope UI del selettore ancora da definire.
Il backend verifica sempre
che l'azienda richiesta sia consentita e deriva il contesto dall'accesso autenticato,
mai da un TenantId arbitrario fidato. Testare anche cambi workspace e richieste
concorrenti, per non leggere/scrivere nell'azienda sbagliata. Nessuna vista aggregata
cross-azienda, condivisione dati, gerarchia holding o amministratore globale impliciti.

"Team" significa per il primo blocco collaborazione di più persone nello stesso
workspace. Gruppi/reparti con membri, ACL o permessi propri sono rinviati: non creare
TeamId o tabelle aggiuntive per questo concetto. Mai condividere password tra membri.

Eventuali piani commerciali Solo/Team/Multi-azienda potranno applicare limiti a utenti
e workspace senza cambiare il modello. Prezzi, entitlement e billing restano fuori
questo task e non sono decisi dal nome dello scenario. Nessuna cardinalità massima
o restrizione commerciale è implicitamente approvata.

### Primo flusso proposto e lifecycle

1. Registrazione con qualsiasi email e password, non solo indirizzi Microsoft;
   risposte generiche, rate limit e policy password/lockout motivate, non semplici
   valori demo copiati dai tutorial. Evitare blocchi permanenti facilmente abusabili.
2. Conferma email con token standard Identity prima dell'accesso tenant-scoped;
   nessun account auto-confermato per aggirare l'assenza del canale email.
3. Login tramite UserManager/SignInManager; password mai persistite in chiaro,
   stampate o restituite. Un UserId immutabile globale, email normalizzata/univoca
   come attributo login, non identità immutabile o prova di membership.
4. Sessione web protetta; provisioning idempotente workspace e Owner server-side.
   Nessun join automatico per dominio email, alias Gmail o TenantId proposto dal client.
5. Logout e reset/cambio password con revoca delle sessioni pertinenti verificata
   nel backend, non semplice cancellazione del cookie sul browser.

Per dev/test: sender finto in memoria e account sintetici; token recuperabili dai
test, mai console, repository o endpoint pubblico che restituisce link di reset.
Per utenti esterni: scegliere canale email reale per conferma/reset, anche uno già
disponibile se idoneo, con consegna, bounce, rate limit e secret valutati. Nessun
fornitore acquistato o approvato ora; eventuale nuova infrastruttura richiede ADR.
Reset token a scadenza limitata e invalidazione dopo uso/modifica credenziali da
dimostrare con test; nessun token nei log o nelle risposte API.

### Sessioni, tenancy e sicurezza proposte

- Next.js e API sotto stessa origine HTTPS da rendere riproducibile nel bootstrap;
  niente seconda gestione sessioni Next.js o access token in localStorage.
- Cookie host-only HttpOnly/Secure, SameSite appropriato, antiforgery per mutazioni
  e logout. CORS non sostituisce CSRF; nessuna mutazione via GET
  ([riferimento](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)).
- Ticket server-side opaco/revocabile, Redis già previsto se approvato; il cookie
  Identity non diventa automaticamente un ticket store Redis: adapter/configurazione
  e keyring Data Protection persistente e protetto sono lavoro esplicito.
- Timeout approvato: 30 minuti idle, 12 ore assolute. Revoca
  logout/offboarding/reset/cambio ruolo verificata; SecurityStamp da solo non prova
  immediatezza se controllato a intervalli. Definire latenza massima e test.
- Account/membership attivi verificati nelle operazioni protette; indisponibilità
  DB/session store fail-closed. Nessun cookie di ruoli fidato indefinitamente.
- Identity possiede utenti/credenziali, Tenants possiede Tenant/Membership con
  TenantId obbligatorio; interazione solo via contracts. Filtri query e protezioni
  write A/B restano obbligatori. Dati predisposti a più membership, uno workspace
  attivo per sessione proposto; cambio validato server-side.
- Owner/Admin/Member per membership; profilo e workspace accessibili ai membri
  autorizzati, settings non sensibili a Owner/Admin. Inviti, trasferimento Owner,
  gestione avanzata ruoli e operazioni privilegiate future restano fuori verticale.
- MFA/recovery amministratori prima della beta esterna come nelle specifiche,
  metodo e test da pianificare; non eliminati per scegliere identità locale.

Test minimi futuri: registrazione/conferma/login/workspace/logout, password errata,
enumeration/rate limit, CSRF/open redirect, reset scaduto/riusato, revoca/account
disabilitato, cookie manomesso, assenza secret nei log e cross-tenant PostgreSQL.
Principal finti nei test non sostituiscono prove del login/sessione realmente implementati.

### Costi ed evoluzione

Nessun canone CIAM obbligatorio. Restano hosting, DB/backup, email, monitoraggio e
tempo operativo/security: identità locale non significa costo zero o capacità garantita
per 500 MAU senza misure. Non viene attivato alcun servizio con questa analisi.

Login social/enterprise può essere introdotto dopo, con account-link verificato e
UserId/membership stabili. Migrare a un provider futuro può richiedere reset o altra
procedura: non promettere migrazione automatica/gratuita. Non costruire ora un adapter
generico senza consumer. Entra/Auth0 restano alternative, non obblighi del SaaS.

Login locale, login social e consenso alla posta sono separati. Nessun connector
attivato dal login: Microsoft resta primo secondo ADR-006, Gmail/altri seguono il piano.

### Stato e decisioni prima del codice

L'utente indica NIS2 e GDPR come riferimenti per le policy di identità locale.
La [baseline approvata](../identity-security-policy.md) traduce l'indirizzo in controlli
ID-01–07 e parametri tecnici approvati esplicitamente il 17 ottobre. Non è certificazione di
conformità o approvazione dei futuri fornitori. Applicabilità NIS2, privacy
e misure ACN specifiche da validare; identità locale resta possibile senza CIAM.

Direzione locale, modello collaborativo e parametri security approvati dall'utente.
**S1-001 Done nel solo perimetro documentale**, approvazione 17 ottobre; dettagli
implementativi, confini persistence concreti e prove nei rispettivi task successivi.
SEC-EX-001 e chiusura M0 sono state precedentemente approvate
esplicitamente il 16 ottobre, solo per sviluppo interno e fino al riesame registrato:
la decisione non approva integralmente questo security model. Nessun codice,
migration, pacchetto, account o costo autorizzato dalla bozza.

Registro decisioni del blocco: risposta utente "ok procediamo come hai suggerito tu"
alla proposta single/multi user, collaborazione senza gruppi e predisposizione
multi-azienda. Conferma registrata il 16 ottobre, limitata a questi aspetti.
Successiva approvazione del 17 ottobre: parametri tecnici S1-001 approvati, senza
estendere l'approvazione alle alternative managed o a una certificazione normativa.

<details>
<summary>Analisi managed iniziale — superata per l'MVP, solo confronto storico</summary>

## Contesto e limiti dell'analisi

L'utente ha confermato il modello SaaS. RAMAI deve servire aziende diverse sullo
stesso prodotto, senza condividere i loro dati; non è un'installazione per ciascun cliente.
Restano validi modular monolith, .NET 10, Next.js e shared-schema TenantId (ADR-003).

SaaS non implica uno specifico cloud o provider di identità. Login a RAMAI, consenso
Microsoft Mail/Calendar, azienda RAMAI e directory del provider sono concetti distinti.
Nessun account, servizio a pagamento, pacchetto, schema, login o connector attivato.
Eccezione SEC-EX-001 e chiusura M0 restano decisioni separate non ancora approvate.

Input utente del 16 ottobre: SaaS confermato; prima fase **fino a 500 utenti attivi
mensili**. L'utente chiede di valutare anche Gmail e altri sistemi, non soltanto M365.
Raccomandazione: account RAMAI con qualsiasi email verificata, senza vincolo M365;
social login eventuale separato dai connector. Questa è ancora una proposta, non
approvazione di singoli pulsanti/provider. Budget, obbligo SSO aziendale, numero aziende
e onboarding non sono ancora comunicati. 500 utenti non significa 500 aziende.

## Confronto delle strategie

| Aspetto | CIAM gestito via OIDC | Identità locale con componenti standard |
| --- | --- | --- |
| Credenziali | Gestite dal provider; RAMAI non conserva password | RAMAI gestisce hash, recupero, verifica email e lifecycle |
| Operazioni | Parte del lavoro MFA/anti-abuse può essere delegato; piano da verificare | Team responsabile di configurazione, aggiornamenti, invio email e supporto account |
| Costo | Servizio, MAU, feature e supporto; evitare stime basate solo sul free tier | Infrastruttura più tempo operativo/security; non è costo zero |
| Disponibilità | Dipendenza esterna per login; sessioni e revoca vanno progettate | Disponibilità sotto gestione RAMAI, inclusi failure e recovery |
| Indipendenza | OIDC riduce accoppiamento; migrazione utenti può comunque essere onerosa | Maggiore controllo, ma protocolli/SSO richiedono una soluzione dedicata |
| Autorizzazioni aziendali | Sempre responsabilità RAMAI | Sempre responsabilità RAMAI |

ASP.NET Core Identity è una base valida per account locali, ma non va confuso con
Microsoft Entra o con un authorization server OIDC. I bearer token delle Identity API
sono proprietari, non un sostituto automatico di OIDC/OAuth standard [F2, F3].
Una scelta locale richiederebbe un disegno alternativo completo di password/reset,
recupero MFA, email, sessioni e protocolli; non basta aggiungere una tabella utenti.

## Raccomandazione proposta

**CIAM gestito con OIDC e login ospitato, API ASP.NET Core come confidential client/BFF;
TenantId, membership e ruoli applicativi conservati e verificati da RAMAI.**

È una valutazione progettuale per concentrare il lavoro sul SaaS, non una garanzia
che il servizio esterno renda RAMAI sicuro o conforme. Il provider non sostituisce
test cross-tenant, autorizzazione server-side, gestione sessioni e risposta incidenti.

### Candidati, non selezione definitiva

1. **Microsoft Entra External ID, external tenant CIAM**, primo candidato per la
   verifica di fattibilità, non approvazione dell'acquisto. È distinto dal workforce
   tenant dei dipendenti e supporta applicazioni rivolte a clienti business [F4].
   Non confondere il suo identificativo directory con il TenantId interno RAMAI.
2. **Auth0**, alternativa da comparare se flussi, federation, UX o condizioni Entra
   non soddisfano i requisiti. La disponibilità di Organizations dipende dal piano [F7].
   Non è necessario usare Organizations come archivio principale delle aziende RAMAI.

Entrambi richiedono verifica pratica di .NET 10, login/logout, recupero, MFA,
federazione Microsoft se desiderata, callback locali HTTPS e revoca.
Microsoft-first per il connector non obbliga a scegliere Microsoft per il login.

## Due diligence commerciale prima della scelta

La pagina Microsoft consultata indica offerta core gratuita fino a 50.000 MAU;
feature premium e SMS sono separati. Non significa "intera autenticazione gratis"
o conferma di un preventivo [F5]. La pagina Auth0 distingue piani, MAU, Organizations
e connessioni enterprise: valutare l'offerta B2B adatta, non il solo prezzo iniziale [F8].

Preparare scenari 100/1.000/10.000 MAU con numero aziende/utenti per azienda,
MFA, SSO enterprise, dominio personalizzato, ambienti dev/prod, supporto ed email.
Per la prima fase usare lo scenario comunicato di 500 MAU; gli altri sono solo
sensibilità di crescita. Non ci sono budget o preventivi approvati, né costo totale
mensile stimato. Il free tier core Entra comprende numericamente questo volume,
ma ciò non dimostra gratuità di MFA, email, supporto o dell'intero servizio RAMAI.
Verificare condizioni correnti, DPA/subprocessor, geografia dati/log/backup,
accessi di supporto, export/migrazione e cancellazione account prima dell'adozione.
Una selezione geografica europea non è da sola prova di conformità o di ogni
trattamento confinato in UE; la documentazione distingue componenti e geografie [F6].

## Architettura proposta senza nuovo microservizio

1. Browser usa Next.js; API/BFF e web pubblicati dietro uno stesso origin HTTPS
   con routing esplicito. /auth e /api/v1 arrivano all'host ASP.NET Core.
2. Il backend avvia Authorization Code + PKCE con state/nonce e redirect esatti;
   valida issuer, audience, firma, scadenze e parametri con handler standard [F1].
3. Dopo callback, il modulo Identity associa (issuer, subject) a UserId interno.
   L'email è un attributo verificabile, non la chiave immutabile dell'identità e non
   una prova di membership. Niente auto-link di account solo per email coincidente.
4. Il backend crea una sessione RAMAI; il browser riceve solo cookie di sessione
   protetto HttpOnly/Secure, senza access/refresh token in localStorage o URL.
5. Il modulo Tenants espone tramite application contract membership e autorizzazioni.
   Il backend risolve il tenant attivo e valida accesso su ogni operazione protetta.
6. Logout invalida la sessione RAMAI server-side e gestisce il logout IdP supportato.

BFF è un ruolo nell'API già esistente, non un servizio nuovo. Evitare doppia gestione
token/sessioni in Next.js e ASP.NET Core. Nessuna scelta di reverse proxy o hosting
attivata da questo ADR. Il bootstrap futuro dovrà rendere riproducibile il routing
HTTPS locale; i due origin HTTP della Foundation non sono il modello auth approvato.

## Sessioni e controlli proposti

- Sessione opaca con stato/revoca server-side (Redis già previsto), legata a UserId.
  Keyring Data Protection condiviso, cifrato/protetto e persistente da progettare
  esplicitamente; mai chiavi o token nel client bundle o nel repository.
- Cookie dedicato host-only; SameSite e cookie temporanei del protocollo impostati
  secondo callback/provider, non un unico valore applicato indiscriminatamente.
- CSRF sulle mutazioni cookie-authenticated e sul logout; controllo Origin quando
  appropriato; CORS non considerato sostituto dell'antiforgery. Nessuna mutazione via GET.
- Proposta da approvare: idle timeout 30 minuti e durata assoluta 12 ore. Scadenza
  assoluta non prolungabile indefinitamente tramite sliding expiration.
- Verifica server-side di account locale e membership attiva nelle richieste protette;
  per la prima verticale preferire check autorevole a cache permessi senza revoca.
- Revoca locale al logout, offboarding e cambi ruolo. Disabilitare l'utente solo
  nell'IdP potrebbe non terminare una sessione RAMAI già attiva: prima dell'approvazione
  definire evento/backchannel o procedura di revoca locale e ritardo massimo ammesso.
- Redis/session store indisponibile: rifiutare operazioni protette, non fallback anonimo
  con privilegi o sessioni inventate. Non chiedere al provider un nuovo login su ogni API.
- Token IdP persistiti solo se indispensabili, server-side con protezione appropriata.
  Nessun refresh token/offline_access richiesto per future Mail API ancora fuori scope.
- Rate limit su login e provisioning, limiti anti-abuse anche se il provider ha i propri.
  Audit sintetico di login/logout/revoca/accesso negato, senza token o PII nei messaggi.
- MFA per Owner/Admin proposto prima della beta esterna; verificare enforcement,
  recupero e costo effettivi nel provider prima di prometterli ai clienti.

## Utenti, aziende e ruoli: proposta applicativa

- Un User RAMAI globale, collegato a ExternalIdentity (issuer, subject) nel modulo
  Identity. Nessun TenantId artificiale su un'identità realmente globale.
- Tenant e Membership (TenantId, UserId, Role, Status) nel modulo Tenants; unicità
  della coppia TenantId/UserId. Ogni record scoped mantiene TenantId obbligatorio.
- Modello dati predisposto a più membership per utente; una sola azienda attiva per
  sessione. UI multi-workspace e selezione esatta da confermare per Sprint 1.
- Cambio workspace come mutazione protetta: il client propone una destinazione,
  il backend verifica membership e aggiorna la sessione. Non fidarsi di header/body
  TenantId senza questa verifica; autenticazione valida non implica accesso a ogni tenant.
- Primo workspace creato esplicitamente dall'utente autenticato secondo onboarding
  approvato; primo Owner assegnato server-side. Nessun join automatico per dominio email.
- Membership invitate/ruoli altrui solo tramite operazioni autorizzate future;
  gestione avanzata/inviti email restano fuori Sprint 1 finché non approvati.
- Niente tabelle lette direttamente tra Identity e Tenants; passaggio da contracts.
  Query filter e validazione scritture restano obbligatori: OIDC non isola il database.

| Operazione minima proposta | Owner | Admin | Member |
| --- | --- | --- | --- |
| Leggere proprio profilo/workspace autorizzato | Sì | Sì | Sì |
| Aggiornare settings azienda non security-sensitive | Sì | Sì | No |
| Cambiare Owner/ruoli/credenziali di altri | Non implementata in questa verticale | Non implementata | No |
| Billing, delete tenant, connector privilegiati | Futuro, policy Owner dedicata | No | No |

Membership/routing scelti dal browser non sono fonti di autorizzazione. Ruolo Owner
RAMAI non equivale a amministratore della directory Microsoft o Auth0.

## Login RAMAI separato dal connector Microsoft 365

Autenticarsi risponde a "chi sei?". Collegare Mail/Calendar risponde a "quali dati
esterni autorizzi RAMAI a leggere?". Un account può accedere a RAMAI senza concedere
Graph Mail/Calendar; la futura connessione richiede consenso, scope, token storage,
revoca e test propri (ADR-006). Login Microsoft non comporta importazione email.
Non richiedere licenza Microsoft 365 a tutti i clienti per effetto di una scelta tecnica.
Proposta onboarding: email verificata indipendente dal dominio, login gestito dal
CIAM; valutare pulsanti Microsoft/Google separatamente se richiesti. "Accedi con Google"
non equivale a "collega Gmail". Connector Gmail e altri sistemi non sono implementati
o anticipati da questo ADR: prima Microsoft 365, poi le integrazioni del backlog futuro.

## Threat model minimo e test richiesti

| Minaccia | Contromisura/criterio da dimostrare |
| --- | --- |
| Callback/replay/issuer contraffatto | Handler OIDC, nonce/state/PKCE e redirect allowlist; negative test reali del protocollo |
| Email uguale o cambiata usata per takeover | Chiave issuer+subject, account-link solo dopo verifica esplicita |
| CSRF/login CSRF/open redirect | Antiforgery e correlation OIDC, return URL locali, test di origine ostile |
| Furto/fissazione sessione | Cookie protetto, rotazione, ticket server-side e timeout |
| Sessione sopravvive a revoca IdP/ruolo | Revoca locale e comportamento provider verificati; niente claim permessi eterni |
| IDOR/cross-tenant con ID valido | TenantContext/membership server-side, filtri e protezioni write; PostgreSQL A/B in CI |
| Autopromozione Owner o provisioning duplicato | Ruoli derivati server-side, transazione/compensazione approvata e idempotenza |
| Provider/Redis indisponibile | Login fallisce chiaramente, operazioni protette fail-closed, niente bypass |
| Secret in browser/log/test | Redazione, test sentinella e scansione; credenziali solo server-side |

Test integration con autenticazione simulata sono utili ma non dimostrano callback,
MFA o logout del provider. Prima della chiusura S1-001 serve una checklist di prova
del provider scelto; l'eventuale PoC eseguibile richiede autorizzazione distinta.

## Decisioni da approvare e condizioni di implementazione

1. CIAM gestito versus locale; provider e piano dopo due diligence e budget.
2. Approvare proposta login con qualsiasi email verificata, non limitato a M365;
   volume iniziale 500 MAU confermato. Numero aziende e budget operativo ancora da definire.
3. BFF/cookie stessa origine, sessione server-side, timeout e modello revoca proposti.
4. User globale, membership multiple/tenant attivo, matrice ruoli e onboarding proposti.
5. Meccanismo concreto di blocco account IdP → revoca locale, MFA/recovery e sviluppo HTTPS.
6. Layout migrations/contracts e pacchetti in task successivi, senza scegliere adesso
   nuove dipendenze infrastrutturali. SEC-EX-001/M0 approvati prima del codice.

Stato S1-001: analisi predisposta, **acceptance non ancora soddisfatta** finché
decisioni e review security umana non sono registrate. Nessuna implementazione S1-002+.

## Fonti ufficiali consultate il 16 ottobre 2026

- F1: [ASP.NET Core OIDC/confidential client, PKCE e BFF](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0).
- F2: [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).
- F3: [Identity API cookies e token proprietari](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0).
- F4: [Entra External ID per clienti/business](https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam).
- F5: [Pricing Entra External ID](https://azure.microsoft.com/en-us/pricing/details/microsoft-entra-external-id/).
- F6: [Data residency Entra](https://learn.microsoft.com/en-us/entra/fundamentals/data-residency).
- F7: [Auth0 Organizations, disponibilità per piano](https://auth0.com/docs/manage-users/organizations/configure-organizations/create-organizations).
- F8: [Pricing Auth0](https://auth0.com/pricing).

Prezzi/feature sono informazioni temporali, non offerta contrattuale o verifica legale.
Nessuna risorsa attivata e nessuna approvazione automatica derivano da queste fonti.

</details>
