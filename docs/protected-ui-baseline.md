# S1-009 — UI protetta

21 ottobre 2026, Europe/Rome. S1-009 pubblicato e verificato nella CI sotto;
nessuna autorizzazione beta. [Piano iniziale](protected-ui-plan.md).
Nome pubblico Agentilio, repository/namespace RAMAI invariati.

Aggiornamento successivo: utente autorizza commit/push/verifica CI, poi S1-010.
Pubblicati i soli 38 file S1-009 nel commit
`bbe24f4cb1ca316366623cdf04059959303a20f4` dopo nuova scansione senza leak.
[CI reale 39580728584](https://github.com/maffeinet/ramai/actions/runs/39580728584)
**success**, SHA verificato: GitHub-hosted Ubuntu 24.04, backend 278 PASS/0 skipped,
Release 0 warning/errori, format; frontend npm ci/lint/tipi/build e 19 test PASS;
9 browser HTTPS PASS, helper format e cleanup CI PASS. Stato locale/non pubblicato
nei paragrafi sotto descrive il checkpoint iniziale, superato da questa evidenza.
La CI non comprende i test aggiunti successivamente da S1-010.

## Cosa è disponibile

- `/login`: account già verificato, password e TOTP oppure codice di recupero.
- `/security`: enrollment TOTP con password corrente, conferma, dieci codici
  recupero mostrati una volta; nuovo login dopo la conferma secondo l'API esistente.
  Sostituzione dell'autenticatore usa anche il fattore corrente; recupero con nuovo
  accesso quando richiesto dal server. Nessun QR inviato a terze parti.
- `/workspace`: profilo verificato, selezione esplicita tramite codice workspace,
  nome/ruolo effettivi, creazione idempotente del primo Owner con MFA e settings
  minimi Owner/Admin. Nessuna lista aziende inventata: manca la corrispondente API.
- Logout POST con CSRF. 401 rimanda al login; 403/rete/risposta non valida
  rimuovono il workspace dalla vista. I messaggi non mostrano eccezioni del server.

Non sono una dashboard business o la demo AI. Non sono stati introdotti endpoint
backend applicativi, migrazioni, servizi infrastrutturali o cambi di confini.
Il guard React non è un controllo autorizzativo: ogni dato/azione passa dalle API
S1-008, che rileggono membership/ruolo e impongono MFA per Owner/Admin.

## Sicurezza e configurazione

Browser → Next → API sulla stessa origine browser; Next inoltra solo `/api/v1/*`
all'origine HTTPS esplicita `RAMAI_API_ORIGIN`. Nessun proxy arbitrario controllato
dal client, principal in Next, CORS aperto o fiducia in X-Forwarded-Proto.
L'API riceve HTTPS reale e verifica l'Origin approvata in `RAMAI_AUTH_ORIGIN`.
Configurazione auth completa nella [baseline sessioni](account-session-baseline.md).

`RAMAI_API_ORIGIN` deve essere disponibile **durante next build** (rewrite salvato
nel build) e durante `next dev`; cambiare origine richiede rebuild. Solo origine
HTTPS, senza credenziali/path/query/hash. È una variabile server, non NEXT_PUBLIC.
Per certificati di sviluppo, Node può ricevere `NODE_EXTRA_CA_CERTS` con il solo
certificato/CA locale esplicitamente autorizzato; mai disabilitare la verifica TLS.
Anche l'origine browser deve essere HTTPS, con certificato attendibile per quel
browser. Non basta eseguire l'API HTTPS dietro una pagina HTTP.

Il bootstrap HTTP Foundation resta valido per `/health` e il caricamento della
pagina, **non** per login cookie Secure. Senza proxy configurato il frontend si
compila ma non è connesso all'autenticazione: non interpretare ciò come login pronto.
Non è stata scelta o attivata un'architettura di deployment.

Cookie sessione HttpOnly/Secure/Strict gestito solo da ASP.NET; il client non lo
legge. Token antiforgery recuperato per singola mutazione e tenuto in memoria;
niente localStorage/sessionStorage, password/token/chiavi in URL o log. Password
e codici form cancellati dopo l'invio. Chiavi/recovery solo in memoria della vista,
cancellati su uscita; `pagehide` elimina i segreti e annulla richieste MFA pendenti.
Ripristino di pagina dalla cache richiede nuova verifica del profilo. Nessun polling
periodico che prolunghi indefinitamente l'idle timeout.

Header no-referrer, nosniff, frame deny, CSP anti-framing/object/base/form; richieste
dati `no-store`, nessun dato privato nel prerender statico. La CSP **non è** una
policy completa script-src con nonce: hardening e review esterna restano aperti.
Nessuna attestazione NIS2/GDPR o sicurezza assoluta.

## File creati/modificati

Nel frontend `src/frontend/ramai-web/`:

- modificati `app/page.tsx`, `app/layout.tsx`, `app/globals.css`, `next.config.ts`,
  `package.json` e `package-lock.json`;
- creati `app/login/page.tsx`, `app/workspace/page.tsx`, `app/security/page.tsx`;
- creati `components/login-view.tsx`, `workspace-view.tsx`, `security-view.tsx`,
  `use-session.ts`, `ui.tsx`, `lib/api-client.ts`;
- creati `playwright.config.ts`, `playwright.unit.config.ts`;
- creati `tests/unit/api-client.spec.ts`, `components.spec.tsx`;
- creati `tests/e2e/global-setup.ts`, `fixtures.ts`, `https-server.mjs`,
  `safe-reporter.ts`, `workspace.spec.ts`.

Helper test: `tests/Ramai.UiTestSetup/{Directory.Build.props,Program.cs,Ramai.UiTestSetup.csproj}`.
Console esplicita solo test, non servizio né endpoint conferma account, non nella
solution. Riusa pacchetti/progetti .NET esistenti. Seed/migrazioni soltanto in DB
casuale `ramai_ui_<guid>`; cinque account sintetici, due aziende A/B, namespace
Redis e certificato effimeri. Materiale privato in temp dell'utente, non nel repo.
Cleanup controlla radice/prefissi ed elimina solo risorse proprie; se fallisce
conserva il manifest privato per recupero, non dichiara falso successo.

Workflow `.github/workflows/ci.yml`: test client/componenti nel job frontend;
Chromium e suite HTTPS reale nel job backend con servizi già isolati, helper format
incluso. Stesse action pinned, permessi read-only, nessun nuovo secret o artefatto
sensibile pubblicato. Non equivale al task S1-011 security CI completo.
Documentazione: README, piano/baseline UI, report Sprint 1, memory, handoff,
backlog Sprint 1, roadmap/backlog demo e nota di stato policy aggiornati.

## Versioni e verifiche locali Windows

SDK .NET 10.0.402; Node 24.19.0/npm 11.17.0; Next 16.3.8, React 19.3.0,
TypeScript 6.0.3 invariati. Unica nuova dipendenza dev **@playwright/test 1.64.1**
(playwright e playwright-core transitivi); Chromium 156.0.8078.4, runtime v1248.
Nessun nuovo NuGet. PostgreSQL pgvector 0.8.6-pg17/estensione 0.8.6 e Redis
7.2.16-alpine3.21 invariati.

| Gate | Esito locale |
| --- | --- |
| Backend Release | PASS, 0 warning, 0 errori |
| Backend format e helper format | PASS |
| Backend test reali | 278 PASS, 0 fail, 0 skipped: 101 unit, 169 integration, 8 architecture |
| npm ci, lint, type-check | PASS; warning audit/deprecation sotto |
| Test client/componenti | 19 PASS: 16 client e 3 rendering statico componenti |
| E2E Chromium su build production HTTPS | 9 PASS, 0 fail, 0 skipped |
| Production build | PASS, route /login, /workspace, /security e root |
| Compose, PostgreSQL, Redis, vector | PASS, servizi healthy, PONG, vector 0.8.6 |
| Workflow actionlint 1.7.12 | PASS, pacchetto ufficiale verificato SHA256 |
| Secret scan Gitleaks 8.30.1 | PASS su tutti i 38 file candidati, nessun leak |
| Link Markdown e git diff --check | PASS; sola nota CRLF→LF README secondo policy |

E2E con API/cookie/Identity/MFA/PG/Redis veri: anonimo, login, TOTP, enrollment e
recupero, creazione/selezione, modifica Owner, Member nell'altra azienda, 403 reale,
CSRF negato, revoca reale→401, errore rete con rimozione dati, viewport 390x844,
nessuno storage/cookie JS, input credenziali errato senza redirect esterno.
`pagehide/pageshow` simulati verificano pulizia e ri-verifica; non sono una prova
di tutti i comportamenti nativi BFCache di tutti i browser. Il solo caso rete usa
intercettazione/abort esplicito; non sostituisce i flussi autorizzativi reali.

Prime esecuzioni: due assert ambigui sul ruolo alert (anche il route announcer Next
ha tale ruolo); corretti identificando il messaggio atteso, senza sopprimere test.
Rafforzato inoltre il validatore runtime per rifiutare ruoli array invece di coercirli.
Controllo visivo desktop e mobile su soli workspace sintetici PASS. Screenshot
locali ignorati in `artifacts/ui-preview/`, mai login compilato, chiavi o codici.
Trace/video/screenshot automatici disabilitati; reporter omette DOM/call log che
potrebbero contenere secret, conserva esito e riga fallita. Nessuna registrazione
credenziali negli artefatti o nella console della fixture.

## Ripetere le prove

Dalla root, dopo `npm ci --prefix src/frontend/ramai-web`:

```powershell
npm --prefix src/frontend/ramai-web run test:unit
npm --prefix src/frontend/ramai-web exec -- playwright install chromium
```

Per E2E occorrono PostgreSQL/Redis **dedicati ai test**, variabili RAMAI_DB_HOST,
RAMAI_DB_PORT, RAMAI_DB_USER, RAMAI_DB_PASSWORD, RAMAI_REDIS_HOST/RAMAI_REDIS_PORT
fornite al processo, mai versionate. Il ruolo test deve poter creare/eliminare
database propri. Poi `npm --prefix src/frontend/ramai-web run test:e2e`.
La fixture compila helper/API e Next, crea HTTPS locale/DB/namespace privati,
esegue e chiude i server, elimina DB e temp. Nel browser test il trust è limitato
al pin SPKI del certificato appena generato, non a tutti i certificati non validi.
Node usa solo quella CA aggiuntiva. Non importa certificati nello store personale.
Non eseguire più suite E2E/build concorrenti nella stessa checkout (`.next` condiviso).

Il progetto Compose locale di verifica è `ramai-s1009-check`, porte 45432/46379,
password casuale nel processo/container. Non sono stati modificati dati/volumi
utente. Dopo le prove: zero DB UI/chiavi Redis UI/temp privati/server UI residui;
container/rete/volumi del solo progetto test rimossi e assenza verificata.
Resta soltanto cache browser ignorata `artifacts/playwright-browsers` e anteprime
sintetiche locali, senza credenziali. Il test non lascia un server demo o account persistenti utilizzabili da
stakeholder: il dataset di prodotto e lo script di presentazione restano D2/D5.

## Debito e limiti

Audit riconfermato 21 ottobre: **5 high dev, 0 critical, 0 production**; stessa
catena eslint-config-next → @next/eslint-plugin-next → fast-glob → micromatch → braces,
non cinque vulnerabilità indipendenti. Nessuna nuova segnalazione Playwright.
ESLint 9.39.5 fuori supporto, warning NO_COLOR/FORCE_COLOR del runner non bloccante.
Nessun audit fix --force, peer forzato, regola disattivata o migrazione tooling.
SEC-EX-001 resta interna e senza proroga: riesame entro 30 ottobre/fine Sprint 1/
prima di clienti o dati reali, quale evento arrivi prima. Prima della presentazione
di novembre occorre riesame esplicito, non un'estensione implicita dell'eccezione.

S1-009 non chiude S1-010/011/012, email operative, review security trasversale,
accessibilità formale, test Firefox/WebKit o configurazione di hosting pubblico.
Il recupero password/email pubblico non viene finto con fixture. Nessun AI,
connector, billing, invio, beta esterna o conformità normativa dichiarata.
CI Linux S1-009 verificata nella run 39580728584 sopra. Le nuove prove
[S1-010](security-regression-baseline.md) hanno propria PR/CI e28b771 verificate;
non usare la run S1-009 come evidenza del nuovo codice.

## Demo e prossimo passo

Utente conferma **demo guidata locale con dati sintetici**. La parte tecnica
accesso/workspace è ora implementata/verificata localmente. Per il valore AI resta
la verticale D2–D5: obiettivo condizionato 6–13 novembre per prima prova interna,
prova generale entro 20 novembre e stakeholder entro 30 novembre 2026. Non è una
garanzia: dipende da capacità, scope invariato e decisione tempestiva provider/budget.
L'utente ha poi autorizzato S1-010 e la sua pubblicazione in PR: suite estesa e CI
del nuovo SHA verificate, review umana/merge ancora pendenti. S1-011/012 non iniziati.
