# S1-010 — Regressione security e readiness della review

21 ottobre 2026, Europe/Rome. [Piano annunciato](security-regression-plan.md).
Solo test/fixture e documentazione; nessun codice applicativo, schema, endpoint,
NuGet o package npm modificato. Non S1-011/012, AI, deploy o beta.

## Pubblicazione per review — checkpoint successivo

Utente autorizza esplicitamente branch, commit/push e PR del solo S1-010.
Branch `codex/s1-010-security-regression`, commit test
`e28b771351af18648b9e383a04d8b51e755c1596`, 17 file; Gitleaks ripetuto senza leak,
build/format e frontend lint/tipi/19 test riconfermati prima del push.
[PR #1](https://github.com/maffeinet/ramai/pull/1) aperta verso main, auto-merge
disattivato. [CI PR](https://github.com/maffeinet/ramai/actions/runs/39585109772)
e [CI push](https://github.com/maffeinet/ramai/actions/runs/39585048873) **success**,
SHA e log verificati: Ubuntu 24.04 GitHub-hosted, SDK 10.0.401, 300 backend
PASS/0 fail/0 skipped (101/191/8), 19 client/componenti e 11 browser HTTPS PASS.
Build 0 warning/errori, format solution/helper, npm ci/lint/tipi/build e cleanup
servizi/volumi CI PASS. È la verifica Linux del nuovo codice, non un clone locale.
Nessuna review umana registrata, nessun merge: S1-010 non chiuso in DoD.
Questo aggiornamento documentale segue il commit testato e avrà la propria CI
visibile nella PR; non cambia i test né riattribuisce la run a un altro SHA.
Le note sullo stato locale/non pubblicato sotto sono checkpoint precedenti.

## Separazione delle evidenze

**S1-009 pubblicato**: `bbe24f4cb1ca316366623cdf04059959303a20f4`, 38 file,
Gitleaks senza leak prima del push. [CI effettiva](https://github.com/maffeinet/ramai/actions/runs/39580728584)
success, SHA verificato, Ubuntu 24.04 GitHub-hosted: 278 backend/0 skipped,
19 client/componenti, 9 browser HTTPS, build/format/lint/tipi PASS e cleanup PASS.
La pubblicazione è quella autorizzata nella prima fase; main remoto contiene S1-009.

**S1-010**: test pubblicati nella PR #1 e verificati su GitHub/Linux nelle run sopra.
Le prove locali Windows restano distinte. Non usare la run S1-009 come loro evidenza.
Review tecnica automatizzata e test non sostituiscono l'approvazione umana
richiesta dal backlog prima del merge. Stato finale: vedi gate sotto.

## Superficie ed evidenze

L'inventario verifica **14 operazioni API: 12 protette e 2 pubbliche intenzionali**,
metodi HTTP e metadata auth. Nuove route o mutazioni GET fanno fallire l'inventario
e richiedono aggiornamento/review, non diventano implicitamente autorizzate.
Root e `/health` restano diagnostica pubblica Foundation, senza contenuti account.

| Superficie | Controllo | Evidenze eseguibili |
| --- | --- | --- |
| POST account/login | CSRF/Origin/HTTPS, risposta generica, password + MFA quando attiva, rate/lock, niente switch account autenticato | WebSessionTests, MfaTests, AccountSecurityTests, E2E workspace/security |
| GET account/antiforgery | Pubblica intenzionale; emette solo token antiforgery, no-store; non concede sessione | WebSessionTests, matrice CSRF nuova e client tests |
| GET profile, account/session | Solo identità globale della sessione valida, nessun tenant proposto dal client | WorkspaceHttpTests, nuova concorrenza multi-soggetto e matrice revoca |
| GET workspace | Solo tenant selezionato e membership riletta; header/query non sono grant | WorkspaceHttpTests, TenantContextIntegrationTests, nuovo test concorrente e browser |
| GET/PUT workspace/settings | Owner/Admin con MFA; Member in B non eredita Owner in A, body chiuso e CSRF | WorkspaceHttpTests matrice ruoli/input/IDOR, nuovi test concorrenti/CSRF; DB verificato dopo i rifiuti |
| POST workspaces/select | ID è proposta, membership/MFA server-side; diniego non seleziona l'estraneo | WorkspaceHttpTests, nuovi due account e browser A/B |
| POST workspaces | Creator autenticato con MFA, Owner assegnato server-side, quota e idempotenza per creator | WorkspaceProvisioningIntegrationTests, WorkspaceHttpTests, nuova collisione requestId fra account |
| POST account/mfa/enrollment, confirm, recovery-codes | Sessione/CSRF, password/fattore richiesti, replay/lock, materiale protetto, revoca | MfaTests e matrice revoca su tutte e tre le route, E2E reale enrollment/recupero |
| POST account/logout, revoke-all | Auth/CSRF, server ticket/stamp revocati, non sola eliminazione cookie | WebSessionTests, nuova matrice replay cookie revocato, coppia CSRF estranea, browser due schede |
| Path workspace con ID arbitrari non previsti | 404 generico per ID valido estraneo o casuale, nessun accesso implicito | WorkspaceHttpTests Profile_selection_provisioning_settings_and_IDOR_follow_server_context |
| Accesso ORM e scritture | TenantId obbligatorio, query/filter e guard scrittura; niente bypass raw/bulk/tracking | TenantIsolationTests e architecture tests, rieseguiti con PostgreSQL reale |

Le route account non sono operazioni di azienda e non accettano un grant TenantId;
non vengono inventati endpoint CRUD business per aumentare la copertura. L'assenza
di una route non è una prova di isolamento di un modulo futuro non implementato.

## Nuove prove e review eseguita

`WorkspaceSecurityRegressionTests.cs`, partial della fixture HTTP esistente:

- 12 casi di replay del **cookie effettivo** dopo revoke-all, uno per operazione
  protetta: 401 e no-store, nessuna credenziale/tenant nel body. Non solo logout UI.
- Inventario auth/metodi di tutte le 14 operazioni; nessun principal/handler auth finto.
- Coppia token/cookie antiforgery **valida di un altro utente**, con sessione della
  vittima: sei mutazioni (settings/select/create/logout/revoke/enrollment) negate.
  Nessun rename, provisioning o revoca accidentale della vittima.
- Due account reali con MFA, due sessioni su host distinti, due aziende: richieste
  concorrenti con X-Tenant-ID/X-User-ID/query ostili non cambiano il soggetto/scope.
  Letture/rename restano propri, selezione estranea negata, dati PG finali verificati.
- Stesso requestId di provisioning fra creator diversi non espone il receipt/tenant
  del primo utente; il nuovo Owner è il creator verificato.
- Sessione selezionata scaduta dopo 31 e 720 minuti non legge/scrive; nessun dato
  mutato. Il caso 720 da solo scade anche per idle: la prova indipendente del cap
  assoluto sotto attività continua resta WebSessionTests.Absolute_expiry_is_not_extended_by_continuous_activity.
- Canary reali di password/hash, email, cookie/ticket, CSRF, chiave TOTP, HMAC rate,
  password/connection string DB e contenuto body nei log catturati: nessuna presenza.
  Recovery code/token email sono già coperti dalle suite Mfa/AccountRecovery.

`SensitiveAssertions.cs`: verifica assenza senza stampare canary/payload nel
fallimento. Tre test ordinari verificano successo, rilevamento e rifiuto di canary
vuote (mai falso PASS). Applicato ai controlli sensibili di Mfa/WebSession/Workspace,
senza sopprimere assert o abbassare logging. CaptureAccountLogs resta in memoria.

Browser: due nuove prove reali, stesso HTTPS/API/PostgreSQL/Redis del prodotto.
Un account MFA e un terzo workspace dedicati ai soli test, nessun dato reale:

- nome workspace con markup/event handler salvato via API e riletto resta testo
  inerte React, nessun elemento immagine o esecuzione; header tenant estraneo non
  cambia il risultato, selezione estranea negata, risposta privata no-store;
- logout in una scheda: l'azione protetta successiva nell'altra riceve diniego e
  torna al login rimuovendo i dati. Non dichiara cancellazione istantanea di tutto
  il DOM di altre schede senza richieste o push di revoca.

I due nuovi E2E non usano auth mock/intercettazioni. Restano nella suite i casi
precedenti esplicitamente simulati di rete/BFCache. Chromium solo, non tutti i browser.
Reporter esteso alle righe del nuovo file, mai dump di DOM, token, errori con body.

Review sorgente: tenant/ruolo derivati dai contratti verificati, no campi client
usati come grant; RequireAuthorization e CSRF prima delle mutazioni; no trust in
forwarded headers; fail-closed DB/Redis, no cache di membership; ORM operational
distinto da bootstrap. Non emersi bug applicativi da correggere nel perimetro.
Prima compilazione: nullable sulla canary DB, risolta rendendone obbligatoria la
presenza, non disattivando warning. Primo nuovo E2E fallito su confronto letterale
Cache-Control: verificata semanticamente la direttiva no-store in lista, non
disattivato il divieto di cache; gli altri assert restano obbligatori.

## File

Nuovi: questa baseline, piano security, WorkspaceSecurityRegressionTests.cs,
SensitiveAssertions.cs, tests/e2e/security-regression.spec.ts.
Modificati: WorkspaceHttpTests.cs (partial/assert), WebSessionTests.cs, MfaTests.cs
(soli assert sensibili), helper Program.cs (sesto account/terza azienda sintetici),
fixtures.ts (tipo dello stato), safe-reporter.ts (riga errore nuovo file).
README, baseline UI, report, memory, handoff, backlog Sprint 1 aggiornano stato e CI.
Nessun file applicativo backend/frontend modificato da S1-010, né workflow o lockfile.

## Gate

Verifiche locali Windows sull'ultima versione dei test:

| Gate | Esito |
| --- | --- |
| dotnet build Release | PASS, 0 warning/errori |
| format solution e helper | PASS |
| Suite backend completa | **300 PASS/0 fail/0 skipped**: 101 unit, 191 integration, 8 architecture |
| Nuove prove backend | 22 aggiunte: 19 casi HTTP e 3 self-test degli assert, tutti PASS |
| npm ci/lint/type-check/production build | PASS |
| Test client/componenti | 19 PASS, invariati (16 client + 3 rendering statico) |
| Browser Chromium HTTPS | **11 PASS/0 fail/0 skipped**, 2 nuovi |
| PG/Redis/vector | Healthy, PONG, estensione 0.8.6 |
| Audit npm completo/production | 5 high dev/0 production, nessuna nuova segnalazione |
| Gitleaks 8.30.1 | PASS su tutti i 17 file candidati, nessun leak |
| Link Markdown e git diff --check | PASS; sole note CRLF→LF secondo policy |

SDK 10.0.402, Node 24.19.0/npm 11.17.0, Playwright 1.64.1 e versioni applicative
invariati. Eseguito un primo giro completo e poi rieseguito il gate finale dopo
il rafforzamento dell'inventario metodi e della coppia CSRF estranea. Nessuna
selezione di test favorevoli sostituisce la suite completa. Assert ordinari non
infrastrutturali dell'helper sono nell'assembly integration, non nei 101 unit test.

## Acceptance e decisioni residue

Pulizia verificata: zero DB test, zero chiavi Redis, zero temp privati UI e zero
server UI residui. Container/rete/volumi del solo progetto `ramai-s1010-check`
rimossi e assenza verificata; nessun dato/volume utente toccato. Rimangono cache
Chromium e screenshot sintetici ignorati già presenti dal blocco UI, senza secret.

Implementazione e test locali/CI pronti. **Review umana prima
del merge ancora mancante**: S1-010 non è chiuso in DoD.
La pubblicazione S1-009 non autorizza un merge automatico del blocco seguente.
La review umana deve esaminare in particolare:

- [ ] Policy ID-01–07 e confini Identity/Tenants/Composition, minacce e casi negativi.
- [ ] Cookie/CSRF, MFA/recupero/revoca, scope concorrenti e fail-closed.
- [ ] Assert anti-leak e artefatti di test senza segreti; risultati CI dello SHA nuovo.
- [ ] Gap/debt di sviluppo distinti dai blocker per clienti o dati reali.

Non un penetration test, prova di assenza assoluta di vulnerabilità o compliance
legale. Restano canale email operativo/recupero pubblico, audit segregato/retention,
backup/restore/rotazione/ACL dell'hosting scelto, CSP script-src completa e verifica
cross-browser/native BFCache; nessuna beta commerciale approvata da questi test.
Nuove feature dovranno aggiungere le proprie prove di isolamento, non affidarsi
alla sola matrice delle route attuali.

SEC-EX-001 non prorogata: 5 high dev nella catena ESLint, zero production; ESLint9
EOL e warning NO_COLOR/FORCE_COLOR invariati. Riesame entro 30 ottobre/fine Sprint 1/
prima di dati/clienti reali, primo evento; prima della demo di novembre serve review.
Nessuna regola disattivata, audit fix --force o migrazione tooling.

Prossimo passo: review umana della PR S1-010 e decisione esplicita sul merge dopo
i gate. Nessun merge automatico; S1-011 resta da autorizzare e non è stato avviato.
