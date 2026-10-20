# Frontend conformance — laboratorio non attivo

Prototipo autorizzato il 19 ottobre 2026: chiudere i gap del candidato
Oxlint + ESLint 10, senza sostituire il tooling RAMAI e senza iniziare S1-004C.
Nessun collegamento a package.json dell'app o alla pipeline GitHub.

Stato review 20 ottobre, ripresa 02:18: **370/370 verdi, nessuno skipped**, anche
nei nuovi cloni Windows/Linux. display-name e no-duplicate-head originali upstream,
con test aggiuntivi. Non adottare automaticamente:
[review corrente](../../docs/frontend-tooling-review.md).

## Esecuzione

Prerequisiti: Node 24.19.0/npm 11.17.0 e dipendenze della baseline frontend
già installate dal suo lockfile. Il comparatore usa ESLint 9 originale come
oracolo; non lo reinstalla e non ne modifica config, sorgenti o dipendenze.
I pacchetti della baseline mantengono il debito SEC-EX-001.

Da questa directory:

```powershell
npm ci --ignore-scripts --strict-peer-deps
npm run lint
npm run report
npm audit --json
```

`npm test` esegue la suite senza generare il riepilogo JSON.
`npm run report` esegue nuovamente tutti i test e produce
`../../artifacts/frontend-conformance/results.json`, ignorato da Git.
Versioni baseline e inventario divergenti, diagnostiche perse, errori di parser,
errori dei processi e integrità vendor non valida fanno fallire i test.
Un report fallito/interrotto non conserva il precedente stato verde.
Nessun download durante i test; npm ci e audit richiedono invece rete.
I file temporanei vengono creati in una directory dedicata e rimossi dopo averne
validato il percorso; l'app non viene usata per scrivere fixture.

## Confini e struttura

- package.json/package-lock.json/.npmrc: dipendenze isolate, peer rigorosi e script
  installazione disabilitati; nessun force o legacy-peer-deps.
- inventory.json: 86 regole TS/TSX e 82 JS/MJS originali, incluse opzioni/severità.
- compatibility.mjs: soli sei entrypoint di regole con i loro helper.
- support.mjs: crea config candidate solo in memoria e file temporanei per Oxlint.
- cases.json/tests/: confronto contro preset reale, casi Next, fixture upstream,
  edge case React/JSX, matrice regole/ignore, hash e isolamento dei bridge.
- eslint.prototype.config.mjs: lint del solo codice di laboratorio, vendor escluso
  per non riscrivere codice/licenze upstream; non è la config futura dell'app.
- report.mjs: riepilogo test e matrice di ownership, `readyForAdoption: false`.
- UPSTREAM.json/vendor/: provenienza, integrità, licenze e adattamenti espliciti.

## Compatibilità e manutenzione

Next no-location-assign-relative-destination viene da @next/eslint-plugin-next
16.3.8; non viene caricato l'indice del plugin né get-root-dirs/fast-glob.
Anche no-duplicate-head viene dalla stessa versione, senza adattamenti o nuove
dipendenze: conserva lo scope di classe derivata da Document e la molteplicità.
React no-deprecated e jsx-uses-react/vars vengono da eslint-plugin-react 7.37.5.
Anche display-name usa l'originale: 14 sorgenti aggiuntivi (~120 KB) preservano
Components invece di euristiche parziali. Costo motivato nella review; sei nuove
dipendenze helper esatte, Hermes 0.37.0 solo per la fixture Flow upstream.
La chiusura di helper necessaria è conservata invece di cambiare solo il peer
range del vecchio plugin o forzarlo su ESLint 10.

Sono adattati due helper React: context.filename al posto dell'API rimossa
getFilename; fallback JSDoc limitato ai casi ClassDeclaration/ClassExpression,
derivato dal metodo ESLint 9.39.5. Quest'ultimo conserva riconoscimento dei commenti
adiacenti, export e class expression. Licenze MIT React/Next/ESLint incluse.
UPSTREAM.json conserva hash originali e adattati separati; non maschera le modifiche
come byte identici all'upstream. Snapshot di quattro file fixture React a commit fisso.

L'adattatore di fixture esegue i dati dei test upstream con Espree e i parser
attuali tramite la nostra suite; **non** esegue la matrice originale di parser
Babel/Flow/vecchie versioni TS, né certifica tutti i suggerimenti/autofix.
Confronta diagnostiche, severità e posizioni per i gap; il preset ibrido nativo
confronta regola/severità/molteplicità, non messaggi o autofix identici fra motori.

Mantenere una chiusura vendor implica responsabilità di revisione aggiornamenti,
licenze e regressioni: referente nominativo da assegnare prima dell'adozione.
Rimuovere i bridge solo quando un'alternativa upstream supera la suite e la review.
Un audit npm senza segnalazioni riguarda il lock del laboratorio, **non** certifica
le sorgenti vendor e **non** bonifica l'oracolo esterno o il progetto attivo.
I test differenziali devono girare solo su fixture fidate, senza secret/clienti.

## Criterio di arresto

Nessuna migrazione automatica. I gap osservati possono risultare chiusi nei casi
testati senza che tutte le 86 implementazioni native siano semanticamente equivalenti.
Prima di adottare: review dei bridge e della responsabilità di manutenzione,
copertura estesa delle regole native/App Router, scope/ignore, suggerimenti rilevanti,
clone pulito e prova Windows/Linux CI, audit aggiornato e autorizzazione separata.
SEC-EX-001 rimane aperta e non prorogata. Nessuna attestazione di sicurezza assoluta.

Analisi e report: [frontend-tooling-analysis.md](../../docs/frontend-tooling-analysis.md).
