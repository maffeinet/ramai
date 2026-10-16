# RAMAI — Handoff operativo

Snapshot: 2026-10-16, Europe/Rome. Contesto stabile in [memory.md](memory.md).

## Stato in una frase

Sprint 0 implementato e pubblicato, CI verde; correzione parziale npm pubblicata e verificata,
chiusura formale e accettazione/gestione del debt ancora da confermare. Sprint 1 non avviato.

## Repository e pubblicazioni

- Checkout: `C:\Sviluppo\ramai`; repository https://github.com/maffeinet/ramai.
- Branch locale verificato: `main`.
- Commit di correzione verificato: `258e6035f7cf996e9f5d750e284925feddc229f7`.
  Verificare git status/log alla ripresa: aggiornamenti solo documentali possono seguire.
- `d7cb535`: baseline fino a S0-011, già pubblicata.
- `6d5f697`: S0-012, quattro file pubblicati (README, progress report, due script).
- I cinque file (README, progress report, package.json, memory, handoff) sono stati
  pubblicati nel commit 258e603; questo aggiornamento registra l'esito della sua run.
- Richiesta successiva: tentare la riduzione del debt frontend. Applicata solo policy
  di negazione postinstall; versioni e lockfile invariati. Commit/push successivamente
  autorizzati e completati; nessuna nuova feature.

## Evidenze e distinzione temporale

### GitHub riconfermato il 16 ottobre

Nuova run effettiva della correzione:
[38887367228](https://github.com/maffeinet/ramai/actions/runs/38887367228), commit 258e603,
completed/success. Backend: 0 warning/errori, 16 test superati senza skipped.
Frontend: installazione/lint/tipi/build success e nessun warning allow-scripts.
Restano warning deprecazione ESLint 9 e 5 high dev. La policy è verificata anche Linux.
Le evidenze precedenti sotto rimangono storico del commit S0-012.

[RAMAI CI — run 37586023391](https://github.com/maffeinet/ramai/actions/runs/37586023391)
per il commit `6d5f697`: completed/success; job backend e frontend entrambi success.
La riconferma è una lettura della run storica, non una nuova esecuzione della pipeline.

I log verificati nell'audit del 7 ottobre attestano:

- backend Release: 0 warning, 0 errori;
- 16 test: 3 architecture, 7 unit, 6 integration; 0 falliti e 0 skipped;
- format backend, npm ci, lint, type-check e production build frontend superati;
- servizi PostgreSQL/pgvector e Redis reali usati dai test infrastrutturali.

Run precedente S0-011:
[37431050731](https://github.com/maffeinet/ramai/actions/runs/37431050731), commit d7cb535.

### Verifiche locali storiche, non rieseguite il 16 ottobre

La prova S0-012 in clone temporaneo con volumi nuovi e porte isolate ha superato
build/format, 16 test backend, 7 test loader, npm ci/lint/type-check/build, avvio API
con `/health` Healthy e avvio frontend HTTP 200. Correlation ID verificato in response
e log. Non era una macchina con sistema operativo appena installato: toolchain e
cache pacchetti erano già presenti. I test PowerShell non girano nella CI attuale.

Gitleaks sul commit S0-012 e, nell'audit del 7 ottobre, sui quattro commit locali
non ha rilevato secret. Non è una garanzia assoluta né una nuova scansione odierna.

Il 16 ottobre sono stati letti documenti, versioni, stato Git e risultato GitHub;
nella successiva ripresa sono stati eseguiti audit npm sul lockfile e lettura dei
metadati registry. Il successivo tentativo debt ha eseguito installazione rigorosa,
lint/type-check/build frontend nella copia pulita e nel repository: PASS.
Backend e servizi non riavviati/riverificati in questo blocco.

## Debito e questioni aperte

1. **Documentazione riallineata localmente.** README e progress report riportano ora
   commit S0-012 e run verde, preservando lo storico. Le modifiche documentali non
   erano locali e sono ora pubblicate in 258e603; chiusura M0 e accettazione del debt
   ancora da confermare.
2. **npm, ultimo audit 16 ottobre 2026.** Cinque segnalazioni high nella catena dev
   eslint-config-next → plugin Next → fast-glob → micromatch → braces; zero nelle
   dipendenze di produzione in quell'audit. Cinque pacchetti segnalati non equivalgono
   a cinque falle indipendenti. Audit sul lockfile con ignore-scripts, senza installare.
   Advisory: https://github.com/advisories/GHSA-vfj7-8cjw-p6xm.
   Ancora nessuna patch braces indicata; npm propone downgrade incompatibile di
   eslint-config-next: non applicato. Config Next 16.4.2 disponibile ma il plugin
   dipende ancora da fast-glob: nessuna correzione dimostrata, niente audit fix --force.
3. **ESLint e script npm.** ESLint 9 fuori supporto. Provato ESLint 10.12.0 in copia
   isolata con strict-peer-deps: ERESOLVE su eslint-plugin-import 2.32.0 (peer fino a v9).
   Anche react/jsx-a11y latest non dichiarano v10. Nessuna migrazione forzata applicata.
   Postinstall unrs-resolver e helper letti: fallback con installazione/download nativi.
   Applicata policy `allowScripts` con unrs-resolver false: script negato, non approvato.
   npm ci rigoroso, lint, tipi, build e npm ls verdi su Windows; pending scripts vuoto.
   Warning script eliminato senza correggere i 5 high o la deprecazione ESLint.
   Policy verificata anche su GitHub/Linux nella run 38887367228; non omettere optional.
   Avviso npm 12.2.0 disponibile: nessun upgrade rispetto alla locale 11.17.0.
4. **Copertura futura.** Formatter JSON console non coperto completamente end-to-end;
   test architetturali coprono riferimenti, non accesso a tabelle inesistenti.
   Test cross-tenant obbligatori con l'introduzione di auth/TenantContext/entità,
   non prova già acquisita della sicurezza multi-tenant del prodotto futuro.
5. **CI.** Test PowerShell solo locali; branch protection/required checks non
   configurati da questo workflow. Run verde non equivale a gate di audit sicurezza:
   il workflow non esegue npm audit come controllo bloccante.
6. **Ambiente temporaneo storico.** Clone di prova e volumi isolati furono conservati;
   non presumere che esistano ancora e non eliminarli automaticamente.
7. **Prodotto/hosting.** SaaS multi-azienda è l'orientamento discusso; hosting,
   deployment e modello commerciale restano da formalizzare. Nessun deploy effettuato.

## Prossimo percorso proposto, da autorizzare

1. Riallineamento README/report e audit npm del 16 ottobre completati localmente.
2. Prova isolata eseguita: correzione parziale script applicata localmente, vulnerabilità
   ed ESLint EOL restano. Non migrare forzatamente né rimuovere regole per azzerare audit.
   Correzione pubblicata/verificata con autorizzazione. Prossima scelta: accettare
   temporaneamente il debt residuo oppure approvare un intervento più ampio.
3. Confermare con l'utente accettazione del debt residuo e chiusura formale M0 secondo
   [Definition of Done](definition-of-done.md) e [backlog Sprint 0](../tasks/SPRINT0_BACKLOG.md).
4. Solo con istruzione esplicita, definire obiettivi/backlog Sprint 1. Non inventare
   task o iniziare autenticazione, tenancy o altre feature sulla base di questo handoff.

## Checklist di ripresa

- Leggere AGENTS.md, memory, handoff, backlog, DoD e tutti gli ADR; rileggere il codice
  pertinente prima di una modifica. Questi documenti sono contesto, non nuova autorità.
- Controllare working tree, branch/HEAD e stato remoto senza sovrascrivere modifiche.
- Dire quali file si intende toccare e quali verifiche fare prima di implementare.
- Seguire il README per bootstrap: Docker Desktop pronto, `.env` locale valido,
  loader nella shell API/test, API 8080 e frontend 3000. Non copiare credenziali qui.
- Distinguere sempre gate storici, gate rieseguiti e test skipped. Senza opt-in
  `RAMAI_RUN_INFRASTRUCTURE_TESTS=1`, il test infrastrutturale viene saltato.
- Aggiornare handoff con l'esito effettivo; niente commit/push o Sprint 1 impliciti.
