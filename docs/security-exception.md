# SEC-EX-001 — Eccezione temporanea tooling frontend

Data proposta: 2026-10-16, Europe/Rome.
Stato: **ACCETTATA TEMPORANEAMENTE — SOLO SVILUPPO INTERNO**.
Approvazione: utente del progetto RAMAI, nella conversazione, il 2026-10-16.
Evidenza: risposta "si procediamo accettando il debito fino al riesame" alla richiesta
esplicita di chiudere Sprint 0 accettando SEC-EX-001 fino al riesame del 30 ottobre.
Motivazione già discussa: proseguire lo sviluppo controllato senza forzare dipendenze
incompatibili, mantenendo il rischio noto visibile e separato dalla release commerciale.
Responsabilità del riesame: responsabile progetto; referente operativo nominativo da
assegnare prima della scadenza. Non si inventano nome, firma digitale o identità legale.

La precedente sola richiesta di strategia non era accettazione; ora è registrata
la successiva approvazione esplicita. Questa eccezione non autorizza release,
nuovi task applicativi o deroghe generiche ad AGENTS.md.

## Ambito esatto

Solo sviluppo interno RAMAI, tooling frontend del lockfile attuale:

- advisory [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm),
  braces <=3.0.3, stack exhaustion/denial of service su pattern profondamente annidati;
- propagazione delle 5 high npm lungo eslint-config-next → @next/eslint-plugin-next
  → fast-glob → micromatch → braces; non cinque falle indipendenti;
- ESLint 9.39.5 fuori supporto; migrazione v10 non compatibile con i peer verificati.

Ultimo audit npm: riesame del 19 ottobre successivo alla pubblicazione A+B,
5 high dev, 0 critical, 0 segnalazioni production; stessa catena/advisory del
16 ottobre, senza modifica del lockfile. Compatibilità peer riesaminata sotto.
Questo dato non certifica assenza di vulnerabilità o di esposizione del tooling.
Inventario e prove in [sprint0-progress.md](sprint0-progress.md).

Esclusi dalla deroga: nuove vulnerabilità, dipendenze runtime vulnerabili, leak,
autenticazione/autorizzazione/isolamento tenant, altri componenti e versioni non valutate.

## Motivazione e alternative provate

Nessuna patch braces indicata nell'ultimo controllo. npm propone downgrade major
incompatibile della configurazione Next. Prova ESLint 10.12.0 con strict-peer-deps
in copia isolata: ERESOLVE, non forzato. Aggiornare solo la configurazione Next non
è stato dimostrato sufficiente. Non rimossi plugin o regole per azzerare l'audit.

L'eccezione accettata consente di proseguire lo sviluppo interno senza dichiarare il prodotto
pronto alla vendita. Cambiare framework non è giustificato dalle evidenze raccolte.

## Riesame tecnico dopo S1-004B — 2026-10-19

Richiesta utente: correggere le vulnerabilità. Nessuna chiusura o proroga implicita
dell'eccezione: l'esito delle prove è **bloccato da dipendenze upstream**.

- Registry npm: braces latest 3.0.3; elenco delle versioni pubblicate senza patch.
  L'[advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) indica ancora
  "Patched versions: None". Non sono cinque vulnerabilità indipendenti.
- @next/eslint-plugin-next stabile 16.4.2 mantiene fast-glob 3.3.1 e la stessa catena.
- Prova in copia temporanea con package/lock/.npmrc del progetto:
  `npm install --package-lock-only --ignore-scripts --strict-peer-deps --save-dev --save-exact eslint@10.13.0 eslint-config-next@16.4.2`:
  **ERESOLVE**, peer di eslint-plugin-import 2.32.0 limitato a ESLint fino a 9.
  Anche i peer correnti di eslint-plugin-react 7.37.5 e jsx-a11y 6.10.2 escludono 10.
- Seconda copia temporanea: aggiornare soltanto eslint-config-next a 16.4.2 con
  ESLint 9 risolve il lock con strict-peer-deps, ma `npm audit --json` restituisce
  ancora **5 high** sulla stessa catena. Non è una correzione; nessun cambio trasferito.
- Le prove isolate modificano solo lock/manifest temporanei; non attestano che la
  configurazione nuova sia eseguibile. Nessun force, legacy-peer-deps, override,
  downgrade Next o rimozione di regole applicati al repository.

Manifest, lockfile e configurazione lint RAMAI restano invariati. Una sostituzione
del tooling o un fork con patch richiederebbero proposta separata, verifica della
copertura lint equivalente e manutenzione esplicita: non sono implementati qui.
SEC-EX-001 resta aperta alle condizioni e scadenze già approvate, non rinnovata.

Verifiche locali ripetute sulla baseline invariata: npm ci --strict-peer-deps,
lint, type-check e production build superati; npm audit --omit=dev restituisce
0 segnalazioni. Audit completo resta non verde (5 high); ESLint 9 resta deprecato.
Nessuna nuova esecuzione GitHub, nessun commit/push di questo riesame.

## Esposizione e rischio residuo

Analisi alternativa successiva al riesame: [confronto tooling](frontend-tooling-analysis.md).
La prima prova Oxlint 1.88.0 + ESLint 10.13.0/TS/Hooks aveva audit 0 ma due controlli
persi. Il successivo prototipo autorizzato in tooling/frontend-conformance recupera
i gap osservati e i riferimenti JSX: 173 test verdi, mapping 86/82 regole e audit
del proprio lock senza segnalazioni. Sorgenti di compatibilità MIT con provenienza
e due adattamenti ESLint 10 espliciti, non certificati dal solo npm audit.
Nessuna migrazione applicata, nessuna chiusura/proroga di SEC-EX-001. La baseline
originale usata come oracolo rimane vulnerabile. Prima di adottare: review, copertura
semantica aggiuntiva, manutenzione esplicita e verifiche clone pulito/Linux/CI.

Il problema è classificato nel tooling dev, ma build agent e workstation restano
esposti se elaborano input ostili. PR e contenuti esterni non diventano attendibili
perché processati da un linter. Non è stata dimostrata l'assenza di percorsi sfruttabili
in CI; non assegnare una probabilità numerica non misurata.

Non accettare pattern glob o configurazione lint arbitraria da richieste applicative.
Eseguire prove con dati sintetici, non clienti reali; non caricare codice non revisionato
su runner con credenziali di produzione. Ogni distribuzione richiede verifica del
pacchetto effettivo: devDependencies non equivale automaticamente a "mai distribuito".

## Controlli attuali verificati

- Versioni bloccate, npm ci, nessun force/legacy-peer-deps o audit fix --force.
- GitHub-hosted Ubuntu, permessi contents:read, credenziali checkout non persistenti.
- Servizi/test CI con credenziale DB effimera; nessun deploy/segreto produzione nel workflow.
- Postinstall unrs-resolver negato esplicitamente, non approvato globalmente.
- Gate lint/type-check/build/test mantenuti; Gitleaks già usato nelle pubblicazioni.
- [Run funzionale 38887367228](https://github.com/maffeinet/ramai/actions/runs/38887367228)
  e [run successiva 38887663889](https://github.com/maffeinet/ramai/actions/runs/38887663889)
  verdi per commit 258e603 e 4c441b3 rispettivamente.

Controlli NON ancora automatici: dependency/secret scan bloccanti, branch protection,
review obbligatoria e scadenza automatica dell'eccezione. Nessun monitor creato.

## Durata accettata e condizioni di revoca

Riesame/scadenza: **30 ottobre 2026**, oppure fine Sprint 1, oppure prima
di qualsiasi prova con clienti/dati reali, qualunque evento arrivi prima.
Nessuna proroga automatica; l'approvazione corrente non si trasferisce a nuovi rischi.
Al riesame l'esito dovrà essere risoluzione, nuova accettazione esplicita motivata
o stop dell'esposizione interessata, non silenziosa prosecuzione della deroga scaduta.

Riesame immediato se: nuova patch compatibile, nuova advisory/exploit pertinente,
nuova esposizione runtime, uso in staging con clienti, cambiamento del lockfile o
coinvolgimento di credenziali/dati reali. Scadenza o variazioni sostanziali richiedono
nuova decisione, non riuso implicito della proposta.

## Azioni proposte nello Sprint 1

- Assegnare referente operativo nominativo per il riesame già approvato e registrare l'esito.
- Introdurre scan che falliscano su nuove vulnerabilità rilevanti; distinguere questa
  specifica eccezione da altre high, senza sopprimere intere categorie.
- Valutare migrazione compatibile quando i plugin la supportano; testare in isolamento.
- Verificare artefatto produzione e dipendenze effettive prima di qualsiasi rilascio.

## Gate di uscita

Ripresa 02:18: laboratorio 370/370 verde anche in nuovi cloni Windows/Linux,
install/lint/audit lab PASS con display-name upstream. NON chiude SEC-EX-001:
lock/tooling attivi invariati, migrazione non approvata. Costo vendor e referente
manutenzione da valutare prima dell'adozione. [Review](frontend-tooling-review.md).
Il paragrafo sotto è storico.

Review notturna del 20 ottobre: il candidato resta non equivalente per display-name
su export memo/forwardRef anonimi (237 casi, 235 pass/2 fail locali). Corretto solo
il falso positivo no-duplicate-head nel laboratorio, nessuna migrazione o bonifica
del lock attivo. React-x provato in isolamento non risolve i casi mancanti e non
è stato aggiunto. SEC-EX-001 resta aperta, senza proroga.

Chiudere SEC-EX-001 quando dependency tree corretto, gate frontend verdi, audit
aggiornato e verifica CI acquisita. Prima della beta esterna servono revisione security
e decisione esplicita sul rischio residuo; questa eccezione NON copre la commercializzazione.
Restano i gate di sicurezza, privacy e release delle specifiche 08 e 12.

## Registro decisioni

| Data | Decisione | Approvatore | Evidenza |
| --- | --- | --- | --- |
| 2026-10-16 | Proposta preparata; rischio non ancora accettato | Non assegnato | Audit e prove nel progress report |
| 2026-10-16 | Accettazione temporanea per sviluppo interno e chiusura M0, entro le condizioni sopra | Utente del progetto RAMAI nella conversazione | "si procediamo accettando il debito fino al riesame" |
