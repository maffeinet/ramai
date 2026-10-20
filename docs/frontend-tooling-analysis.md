# RAMAI — Alternative al tooling frontend vulnerabile

Data: 2026-10-19, Europe/Rome. Stato: **prototipo di conformità implementato, non migrazione**.
Richiesta: conservare controlli equivalenti senza cambiare Next.js o stack applicativo.
Nessuna modifica a manifest, lockfile, configurazione lint, codice applicativo o workflow RAMAI.
Le quattro modifiche documentali del riesame precedente sono conservate.

Aggiornamento 20 ottobre: questa analisi conserva lo storico del prototipo iniziale.
Ultima ripresa 02:18: 370/370 verdi anche nei nuovi cloni Windows/Linux con
display-name originale e chiusura vendor motivata; sei dipendenze helper più
Hermes solo test. Stato pubblicazione e limiti nella review, migrazione non approvata.
La [review corrente](frontend-tooling-review.md) prevale sui risultati precedenti:
lo stato intermedio era 237 test locali, 235 pass/2 fail, display-name non equivalente. Next
no-duplicate-head recuperato con bridge upstream senza nuove dipendenze.
React-x 5.28.2 non risolve gli export anonimi provati; non aggiunto al candidato.

## Esito e raccomandazione

Una strada concreta è **Oxlint nativo + ESLint 10 con plugin Hooks e TypeScript
supportati**, completata da controlli compatibili per i gap rimasti. Il prototipo
si installa senza forzature, ha audit senza segnalazioni note e compila la copia
dell'app con le versioni applicative attuali. La prima analisi aveva osservato due
controlli diagnostici persi. Il successivo [laboratorio di conformità](../tooling/frontend-conformance/README.md)
li recupera nei casi testati, con 173 test verdi e mapping di tutte le 86 regole.
Questo non dimostra equivalenza esaustiva di ogni implementazione nativa/autofix.
Non sostituire il tooling attuale sulla sola base dell'audit o del mapping verde.

Raccomandazione: revisionare il prototipo e verificarlo in clone pulito/Linux prima
di autorizzare separatamente una migrazione, con criterio "nessun controllo perso".
Se il criterio non viene soddisfatto, non trasferire il prototipo nel progetto.
Nessun software può essere dichiarato sicuro in assoluto sulla base di npm audit.
SEC-EX-001 resta aperta e non prorogata; nessuna readiness commerciale implicita.

## Baseline effettiva

Riferimento applicativo pubblicato: ca7edf4; ultimo commit documentale 79996b4.
Ambiente locale delle prove: Windows, Node 24.19.0, npm 11.17.0.
Applicazione invariata: Next.js 16.3.8, React/React DOM 19.3.0, TypeScript 6.0.3.
Tooling attuale: ESLint 9.39.5, eslint-config-next 16.3.8.

Inventario ottenuto con ESLint.calculateConfigForFile, non dal solo elenco npm:

| Gruppo | Regole attive TS/TSX | Corrispondenze nominali Oxlint 1.88.0 |
| --- | ---: | ---: |
| React | 17 | 14 |
| React Hooks/Compiler | 16 | 14 |
| Next.js | 22 | 21 |
| Import | 1 | 1 |
| Accessibilità JSX | 6 | 6 |
| ESLint core | 4 | 4 |
| TypeScript ESLint | 20 | 20 |
| Totale | 86 | 80 |

Stesso conteggio su app/page.tsx, app/layout.tsx e next.config.ts; sulla configurazione
MJS attuale sono 82 (senza i quattro core del preset TypeScript). Opzioni, severità,
parser, scope dei file e ignore fanno parte del contratto, non solo i nomi.
Le tre corrispondenze TypeScript no-array-constructor/no-unused-expressions/
no-unused-vars in Oxlint passano dai rispettivi nomi core; non è prova generale
di equivalenza. Gli scope jsx_a11y del registro CLI sono normalizzati a jsx-a11y.

L'audit attuale del progetto resta 5 high dev sulla catena
eslint-config-next → @next/eslint-plugin-next → fast-glob → micromatch → braces;
audit production del riesame precedente: 0 segnalazioni. Braces latest è ancora
3.0.3; l'[advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) non indica patch.

Ispezione della sorgente installata: il plugin Next usa fast-glob nella risoluzione
di settings.next.rootDir; nella configurazione RAMAI tale parametro non è impostato,
e getRootDirs restituisce context.cwd senza chiamare il glob. Questo restringe
il percorso individuato, ma non dimostra assenza di tutti gli altri percorsi,
non annulla l'advisory e non chiude l'eccezione. Nessun input glob applicativo aggiunto.
La [discussione upstream](https://github.com/micromatch/braces/issues/70) contiene
anche una contestazione del maintainer: non la trattiamo come patch o ritiro dell'advisory.

## Alternative confrontate

| Alternativa | Evidenza | Giudizio |
| --- | --- | --- |
| Solo aggiornamenti Next config/ESLint | Riesame precedente: config 16.4.2 conserva 5 high; ESLint 10 con preset Next fallisce peer | Non risolve |
| Biome 2.5.15 da solo | Migratore: 41 regole migrate direttamente, 47/86 dichiarate coperte includendo opzionali; gap concreti nelle prove | Non equivalente |
| Oxlint 1.88.0 da solo | 80/86 corrispondenze nominali, due errori di esempio non segnalati e altri gap da valutare | Non equivalente |
| Oxlint + ESLint 10 selettivo | Audit 0, strict peer e gate locali verdi sul prototipo; Hooks/TS ufficiali mantenuti, due gap diagnostici noti | Candidato preferito, non ancora adottabile |
| Fork/patch conservativa Next/braces | Può conservare i plugin/regole ma introduce manutenzione e non risolve da sola ESLint 9 fuori supporto | Riserva, non testata/implementata |
| Lint attuale in ambiente effimero confinato | Conserva i controlli, limita impatto operativo; pacchetti vulnerabili e debito restano | Mitigazione temporanea, non correzione |

Oxlint ha [plugin nativi](https://oxc.rs/docs/guide/usage/linter/plugins) ma le sue
implementazioni non sono automaticamente identiche ai plugin originali. Non si usa
il supporto [JS plugin, ancora alpha](https://oxc.rs/docs/guide/usage/linter/js-plugins.html)
come scorciatoia per aggirare i peer. Biome documenta il proprio
[migratore](https://biomejs.dev/installation/migrate-eslint-prettier/): copertura e
opzioni devono comunque essere verificate sul progetto concreto.

## Prove differenziali: 17 casi sintetici

Configurazioni generate solo in directory temporanea. Baseline originale con
versione React fissata a 19.3.0 nelle sole prove per evitare auto-detection fuori
dal progetto. Candidati configurati esplicitamente, non con generici preset default.
Le categorie implicite Oxlint sono disattivate e sono riabilitate le regole mappate.
Biome: migrazione delle 86 regole con recommended=false e formatter/assist disabilitati.

| Caso | Baseline | Oxlint | Biome | Ibrido |
| --- | --- | --- | --- | --- |
| Componente valido | Nessuna diagnostica | Idem | Idem | Idem |
| JSX senza key | Rilevato | Rilevato | Rilevato | Rilevato |
| Hook condizionale | Rilevato | Rilevato | Rilevato | Rilevato |
| Dipendenza useEffect mancante | Rilevato | Rilevato | Rilevato | Rilevato |
| next/image senza alt | Rilevato | Rilevato | Non rilevato | Rilevato |
| ARIA non valido | Rilevato | Rilevato | Rilevato | Rilevato |
| Script sincrono | Rilevato | Rilevato | Rilevato | Rilevato |
| next/script inline senza id | Rilevato | Rilevato | Rilevato | Rilevato |
| window.location.assign relativo | Rilevato | Non rilevato | Non rilevato | Non rilevato |
| componentWillMount deprecato | Rilevato | Non rilevato | Non rilevato | Non rilevato |
| any esplicito | Rilevato | Rilevato | Rilevato | Rilevato |
| Variabile inutilizzata | Rilevato | Rilevato | Rilevato | Rilevato |
| Valori enum duplicati | Rilevato | Rilevato | Rilevato | Rilevato |
| Componente locale usato solo nel JSX | Nessuna diagnostica | Idem | Idem | Idem |
| Oggetto locale chiamato window, non globale | Nessuna diagnostica | Idem | Idem | Idem |
| Mutazione delle props | Rilevato | Rilevato | Non rilevato | Rilevato |
| Date.now durante render | Rilevato | Rilevato | Non rilevato | Rilevato |

Su 14 casi negativi: baseline 14 rilevati, Oxlint/ibrido 12, Biome 9.
Tre casi positivi senza falsi allarmi nei quattro assetti. "Rilevato" comprende
warning ed errori secondo severità originale; non equivale sempre a exit code non zero.
Il caso Image/alt evidenzia una differenza di opzioni/mapping della migrazione
provata, non prova che Biome non possa configurare quel controllo.
17 casi non coprono tutte le opzioni o tutte le 86 regole: nessuna certificazione
di equivalenza, né benchmark prestazionale o attestazione security.

## Prototipo ibrido: cosa è stato realmente eseguito

Dipendenze dev esatte del candidato: oxlint 1.88.0, eslint 10.13.0,
typescript-eslint 8.71.2, eslint-plugin-react-hooks 7.1.1, globals 16.4.0;
@types/node 26.6.4, @types/react e @types/react-dom 19.3.0 e TypeScript invariati.
ESLint mantiene le 20 regole TS, 16 Hooks/Compiler e 4 core originali;
Oxlint mantiene le 42 corrispondenze React/Next/a11y/import.
Non è installato il preset eslint-config-next né il plugin Next JavaScript.

- npm install e poi npm ci: --ignore-scripts --strict-peer-deps, PASS senza force.
- npm ls: nessun ESLint 9 o peer invalido; solo ESLint 10.13.0.
- npm audit --json del lock candidato: 0 segnalazioni, anche incluse dev.
- npm ls braces fast-glob micromatch eslint-config-next eslint-plugin-react --all:
  albero vuoto. Questa è rimozione della catena nel candidato, non nella baseline.
- Lint della copia app/ e next.config.ts: PASS; type-check: PASS.
- Production build Next.js: PASS; Next/React/TypeScript non cambiati.
- Warning build: Next inferisce la root dal lock del laboratorio padre, perché
  il prototipo è annidato in un'altra directory npm. Nessuna modifica Next per
  nasconderlo; da ripetere in clone isolato senza lock antenati prima dell'adozione.
- Prove Windows locali; nessuna esecuzione Linux/GitHub, test HTTP o backend nuova.
  Il build del candidato non prova equivalenza del lint.

SHA256 lock del candidato provato:
BE81EA067F6BA2F52FD8ED5FA8CF603FBE9DE68368D93BE8CBCED7D5B520F7FD.
Prototipo, inventario e risultati JSON restano artefatti locali temporanei in
C:/Users/helpdesk.ISV/AppData/Local/Temp/ramai-lint-study-20261019-1425;
non sono dipendenze o gate del repository e non vanno usati come bootstrap definitivo.

## Gap individuati dalla prima analisi

Lo stato successivo al prototipo è riportato sotto; queste erano le condizioni iniziali.

1. Conservare no-location-assign-relative-destination: nuovo supporto upstream
   compatibile, oppure regola locale versionata con provenienza/licenza e test
   equivalenti alla sorgente Next. Non sostituirla con una ricerca testuale:
   la regola distingue globali, shadowing, accessi computed e valori statici.
2. Conservare react/no-deprecated: plugin mantenuto con copertura dimostrata,
   oppure adattamento locale revisionato. Non bastano tre lifecycle vietati:
   la regola copre anche API React/DOM legacy, import e destructuring.
3. JSX uses-react/uses-vars non hanno regole Oxlint omonime: verificare il contratto
   del runtime JSX automatico, i riferimenti JSX e i casi validi/invalidi rispetto
   a no-unused-vars. Il solo test positivo presente non chiude l'intera equivalenza.
   [ESLint 10 traccia i riferimenti JSX](https://eslint.org/docs/latest/use/migrate-to-10.0.0),
   ma non usare questo dato per dichiarare identici tutti i comportamenti.
4. Il prototipo ibrido conserva config e gating mediante il plugin Hooks originale;
   da soli i nativi Oxlint non espongono tali due regole. È anche il motivo per
   mantenere il [plugin Hooks ufficiale](https://react.dev/reference/eslint-plugin-react-hooks)
   invece di affidarsi alle analisi Compiler native sperimentali.

Nella sola fase di analisi non era stato implementato alcun bridge o fork.
La manutenzione di regole locali richiede responsabile, licenze, versioni sorgente
bloccate, suite di conformità e criterio di rimozione quando arriva supporto upstream.
Se il costo di questa manutenzione non è accettato, non procedere alla migrazione.

## Piano proposto, da autorizzare separatamente

1. Salvare inventario completo delle regole/opzioni/severità e mapping versionato.
   Ampliare i test differenziali, compresi JS/MJS, TS/TSX, App Router, ignore,
   import/alias, JSX e casi di falso positivo. Nessuna disattivazione silenziosa.
2. Progettare e revisionare i due controlli diagnostici mancanti e la gestione JSX;
   trasferire test upstream pertinenti rispettando licenze e adattandoli a ESLint 10.
3. Solo dopo copertura verde: aggiornare package.json, package-lock.json,
   eslint.config.mjs, aggiungere configurazione Oxlint e test di conformità.
   Nessuna nuova feature, backend, framework, formatter o libreria runtime.
4. Rendere il test di conformità un gate non opzionale; eseguire entrambi i linter,
   tsc e build. Mantenere almeno le severità attuali; una policy warning-as-error
   sarebbe una decisione esplicita ulteriore. Scan dell'intero albero con soglia
   high, senza soppressioni generiche e senza ereditare SEC-EX-001 su nuovi rischi.
5. Verificare npm ci da clone pulito, Windows e runner Linux GitHub, dipendenze
   senza la catena, audit aggiornato e produzione. Prima del push serve nuova
   autorizzazione: nessun commit o push in questa analisi.
6. Solo con evidenze equivalenza/quality gate/CI complete, chiudere formalmente
   SEC-EX-001; se un controllo resta scoperto, fermare l'adozione e riportarlo.

### Strada conservativa se non si approva l'ibrido

Conservare tutti i controlli ed eseguirli, entro la deroga già approvata, in un
job/container effimero senza secret/dati clienti, privilegi ridotti, workspace
limitato e risorse/timeout vincolati; separare acquisizione delle dipendenze dalla
fase lint e limitare la rete di quest'ultima. Review dei file di configurazione
e nessun glob utente. GitHub-hosted con contents:read aiuta, ma non prova già
tutto questo confinamento: hardening aggiuntivo da implementare/testare a parte.

Non è una bonifica delle dipendenze, non risolve ESLint 9 fuori supporto e non
autorizza beta/clienti o proroghe. Un fork di braces o del plugin Next resta
un'ulteriore opzione solo con revisione e test: rinominare il pacchetto per far
sparire l'audit non è una correzione. Nessun audit fix --force o downgrade Next.

## File e limiti della sola analisi

Creato questo documento; aggiornati security-exception.md, sprint1-progress.md,
memory.md e handoff.md, preservando il riesame precedente. Nessuna modifica
applicativa, nessuna nuova infrastruttura, S1-004C e task successivi non iniziati.
Le prove isolate dimostrano fattibilità, non equivalenza completa né sicurezza
assoluta. L'albero reale RAMAI conserva il debito finché non si approva e verifica
la migrazione. Backend non rieseguito perché invariato. Nessuna nuova CI o pubblicazione.

## Prototipo di conformità autorizzato — 2026-10-19

L'utente ha autorizzato esclusivamente prototipo e chiusura dei gap, senza sostituire
il tooling e senza iniziare S1-004C. Implementato in tooling/frontend-conformance,
con package/lock/config propri e senza integrazione nella CI o negli script dell'app.
[Uso, file, vincoli e manutenzione](../tooling/frontend-conformance/README.md).

- Bridge di quattro regole: Next no-location-assign-relative-destination e React
  no-deprecated, jsx-uses-react, jsx-uses-vars, con sola chiusura di helper necessaria.
- MIT React/Next/ESLint incluse; versioni, integrità npm e SHA256 dei file in
  UPSTREAM.json. Hash originali e adattati conservati separatamente.
- Due incompatibilità ulteriori trovate dai test: rimozione getFilename e
  getJSDocComment in ESLint 10. Adattati solo i due helper coinvolti; fallback JSDoc
  ristretto alle classi, con casi export/class expression/commenti non pertinenti.
- Nessun cambio del peer range del vecchio plugin, force, override o nuovo plugin
  completo Next/React; candidato senza catena braces nel proprio lock.
- Inventario congelato: 86 regole TS/TSX, 82 JS/MJS, incluse opzioni/severità;
  ogni regola ha esattamente un motore assegnato, nessuna disattivazione silenziosa.

### Risultati locali del prototipo

**173 test pass, 0 fail, 0 skipped**:

- 27 confronti dei preset completi, inclusi i 17 casi dell'analisi e più estensioni;
- 34 casi Next (19 negativi, 15 positivi), con globali, shadowing, computed access,
  alias, riassegnazioni, template, protocolli e spread;
- 88 fixture React upstream a commit bloccato, eseguite tramite adattatore locale;
- 18 edge case React/JSDoc/versioni/import/destructuring;
- 5 controlli inventario/ownership/integrità/albero/ignore e 1 prova di isolamento.

I due casi persi nell'analisi ora sono rilevati con le stesse severità originali;
le regole di compatibilità mantengono anche messaggi/posizioni nei test dedicati.
Per i motori differenti il confronto del preset controlla regola/severità/molteplicità,
non messaggi identici o autofix. Le fixture upstream non equivalgono all'intera
matrice storica Babel/Flow/TS né alla convalida di ogni suggerimento.

npm install e npm ci --ignore-scripts --strict-peer-deps PASS; lint del codice del
laboratorio con zero warning ammessi PASS; npm audit --json del lock del laboratorio
0 segnalazioni note. Il comparatore usa la baseline originale come oracolo esterno:
quel dependency tree rimane vulnerabile e non è coperto dall'audit del nuovo lock.
Una prova separata dimostra che i bridge eseguono anche senza caricare la baseline
o gli indici dei vecchi plugin. Vendor e adattamenti richiedono comunque review,
non sono certificati dal solo audit npm.

Frontend originale: lint, type-check e production build ripetuti PASS;
audit production 0 segnalazioni. Manifest/lock/config originali invariati, quindi
le 5 high dev e il warning ESLint 9 restano. Nessuna nuova verifica backend,
Linux, clone pulito o GitHub in questo blocco; nessun commit/push.

Il report generato in artifacts/frontend-conformance/results.json dichiara
esplicitamente readyForAdoption=false. Report/handoff/memory ed eccezione aggiornati;
SEC-EX-001 non chiusa né prorogata. Prima di adottare: review dei bridge e owner di
manutenzione, copertura aggiuntiva delle regole native, clone pulito e Linux/CI,
audit aggiornato e nuova autorizzazione. Nessuna migrazione automatica o S1-004C.

Verifiche finali: Gitleaks 8.30.1 sui 45 file candidati (40 del laboratorio e 5
documentali) senza leak; link locali e controllo whitespace dei documenti PASS.
Diff di src/, workflow, gitignore e gitattributes assente. Audit completo della
baseline riconfermato: stessa advisory, 5 high/0 critical, senza nuove segnalazioni.
