# S1-009 — Piano UI protetta

21 ottobre 2026. Utente autorizza S1-009 e chiede la previsione della prima demo.
Solo UI delle API esistenti, nessuna feature AI/business, nuova migrazione o deploy.
Nessun commit/push S1-009 assunto dall'autorizzazione del blocco precedente.

## File previsti e dipendenze

- src/frontend/ramai-web/app: login, workspace, security; root/layout/CSS aggiornati.
- components: shell, messaggi sanitizzati, form e controller client delle tre viste.
- lib/api-client.ts: fetch same-origin, contratti verificati, CSRF solo in memoria,
  errori normalizzati; mai password/token/sessioni in storage o log.
- next.config.ts: rewrite server-side verso origine API HTTPS esplicita, header
  browser/no-store. Stessa origine browser, senza seconda gestione auth in Next.
- package.json/lock: solo @playwright/test 1.64.1 come nuova dipendenza dev;
  script/config test unit/componenti e browser. Nessun nuovo framework applicativo.
- tests frontend: contratti client/errori, componenti e percorso browser reale.
- tests/Ramai.UiTestSetup: utility console .NET per account/dati/certificato sintetici
  in DB temporaneo, non un nuovo servizio prodotto, non inclusa nella solution.
- fixture E2E e server HTTPS Next solo test: temp privati, pin del certificato
  generato nel browser e CA esplicita in Node, nessun bypass TLS generale.
- README, report, memory/handoff, baseline UI, roadmap/demo backlog; eventuale
  aggiornamento CI limitato a eseguire i test di questo task, non S1-011 completo.

Riutilizzati i pacchetti .NET già presenti tramite riferimenti di progetto del solo
helper test. Nessuna nuova dipendenza infrastrutturale o cambiamento confini moduli.
Test helper non crea endpoint di conferma account, non espone secret né modifica
dati/volumi utente; controlla prefissi/radici prima della pulizia delle proprie risorse.

## UI e sicurezza

Login email/password/TOTP o recovery; enrollment con password corrente e conferma
TOTP, codici recupero mostrati una volta in memoria e poi nuovo login. Account
verificati predisposti tramite fixture/dev autorizzato: nessuna registrazione o
email reale inventata per aggirare i blocchi API mancanti.
Area protetta con nome/ruolo/ID workspace verificati, creazione idempotente,
selezione tramite proposta ID autorizzata dal server, settings minimi e logout POST.
Niente Today fittizio, dashboard o agente simulato. UI non è confine autorizzativo.
401/scadenza svuotano i dati e richiedono login; 403/rete/503 non lasciano dati
aziendali precedenti come se fossero ancora autorizzati. Nessun redirect esterno,
principal dal client, GET mutante, CORS permissivo o token nel bundle/storage/URL.

## Strategia test e gate

Test client/componenti distinti dall'E2E. Browser reale Chromium su UI e API HTTPS,
cookie/Identity/MFA/PostgreSQL/Redis veri: accesso, TOTP/recupero, workspace, logout,
ruoli diversi A/B, rifiuti CSRF/IDOR, 401/403/rete e payload non validi. Simulazioni
di rete nei casi specifici non sostituiscono i casi reali. Trace/video/screenshot
automatici disattivati per non conservare credenziali o secret MFA negli artefatti.
Eventuale screenshot visivo solo area con dati sintetici, mai enrollment/recupero.
Build/format/test backend, npm ci/lint/tipi/build, test UI, audit e secret scan;
documentare limiti e distinguere esecuzioni locali da CI. Non dichiarare beta.

## Previsione demo, non impegno garantito

Demo tecnica accesso/workspace: consegna di S1-009 dopo gate. Demo utile con AI
reale: obiettivo proposto **6–13 novembre**, prova generale entro **20 novembre**,
presentazione entro **30 novembre 2026**. Assunzioni: demo guidata locale, dati
sintetici, solo verticale conversazioni→evidenze→Today→bozza senza invio, capacità
di lavoro disponibile e scelta tempestiva provider/modello/budget. Nessun servizio
AI attivato qui. Prova remota autonoma cambia scope e richiede deployment/review;
L'utente ha confermato il 21 ottobre: **demo guidata locale con dati sintetici**.
Non è autorizzata una prova remota autonoma.
SEC-EX-001 resta soggetta al riesame anticipato già previsto, non prorogata dalle date.

Implementazione e verifiche effettive: [baseline UI](protected-ui-baseline.md).
