# Review laboratorio frontend — stato operativo

Aggiornato: 2026-10-20, Europe/Rome. **370/370 verdi Windows/Linux; migrazione non approvata.**

## Stato corrente — ripresa 02:18

Gap osservati colmati con display-name originale upstream, senza euristiche,
soppressioni o peer forzati. **370/370 test, zero skipped**, in locale e cloni
puliti Windows/Linux: precedenti 237 + 109 fixture upstream + 24 casi wrapper.
Fixture upstream confrontate anche per messageId/severità/posizioni e opzioni;
TS con parser effettivi baseline/candidato, una fixture Flow con Hermes 0.37.0
su entrambi, nessuna conversione dello snippet. Non è la matrice parser storica completa.

14 nuovi sorgenti vendor invariati, stessa provenienza MIT già fissata e hash;
riusati gli adattamenti esistenti ESLint 10. Sei bridge e 40 regole native,
ownership unica verificata. Sette dipendenze esatte aggiunte solo al lab:
array-includes 3.2.0, array.prototype.flatmap 1.3.3, es-iterator-helpers 1.4.0,
jsx-ast-utils 3.3.5, object.fromentries 2.0.8, object.values 1.2.1, hermes-eslint
0.37.0 (solo test Flow). Motivazione dell'ampliamento vendor nella decisione sotto.

Cloni finali: install strict/ignore-scripts, lint e audit lab PASS (0 advisory);
install/lint/types/build e audit production frontend originale PASS. Evidenze:
C:/Users/helpdesk.ISV/AppData/Local/Temp/ramai-clean-conformance-5e698682dc12415db46c00c40cf06ca4/evidence.
HEAD 79996b4 pulito + overlay esplicito lab con manifest SHA256, non un commit
candidato già pubblicato. Linux sulla stessa immagine fissata nelle prove storiche.

Backend Release: 0 warning/errori, format PASS, 112 test PASS/0 skipped (39 unit,
6 architecture, 67 integration), infrastruttura/pgvector reali. .env assente:
password casuale solo nel processo, Compose dedicato ramai-conformance-night-0220,
porte 45432/46379. Rimossi soltanto container/rete/volumi temporanei appena creati
per la prova, nessun dato utente rimosso. Nessuna modifica applicativa o workflow.

Pubblicato nel commit bcaca48d3afcceff06af553889d76b1f9dd726a8 dopo Gitleaks
8.30.1 su tutti i 70 file candidati (nessun leak), whitespace/link e gate verdi.
[CI reale 39347649384](https://github.com/maffeinet/ramai/actions/runs/39347649384)
conclusa success: backend/frontend, 112 test backend/0 skipped, format e build verdi.
La CI attiva NON esegue il laboratorio: la run verifica app/backend, non i 370 test.
Ripetuti inoltre cloni del commit pubblicato bcaca48, non più del vecchio HEAD:
370/370 su Windows/Linux e tutti i gate install/lint/audit lab/frontend verdi,
nessuna diff nei sorgenti laboratorio rispetto al commit. Evidenze finali:
C:/Users/helpdesk.ISV/AppData/Local/Temp/ramai-clean-conformance-89af3766eb6c4995be9bacecaff70f64/evidence.
Questo aggiornamento documentale successivo non è il commit testato dalla run citata.
readyForAdoption=false:
review manutenzione vendor e approvazione separata prima della migrazione,
equivalenza opzioni/autofix non esaustiva. SEC-EX-001 aperta, 5 high dev attive,
nessuna proroga o S1-004C. Le sezioni seguenti sono storico dei gap ora colmati.

## Perimetro

Review dei bridge, copertura aggiuntiva e cloni puliti Windows/Linux. Il 20 ottobre
l'utente ha autorizzato lavoro notturno e pubblicazioni GitHub delle modifiche
pertinenti, mantenendo i quality gate. Non sono autorizzati migrazione del tooling
attivo, S1-004C, sessioni, MFA, UI o altri task. Nessun commit/push di questo blocco
effettuato al momento di questo snapshot, perché il laboratorio ha test falliti.

## Risultati realmente verificati

| Ambiente | Suite laboratorio | Install/lint laboratorio | Audit laboratorio | Frontend originale |
| --- | --- | --- | --- | --- |
| Clone Windows, 19 ottobre | 221 casi: 219 pass, 2 fail, 0 skipped | PASS | 0 segnalazioni | lint/types/build e audit production PASS |
| Clone Linux Debian 12 amd64, 19 ottobre | 221 casi: 219 pass, 2 fail, 0 skipped | PASS | 0 segnalazioni | lint/types/build e audit production PASS |
| Checkout locale, ripresa 20 ottobre | 222 casi: 220 pass, 2 fail, 0 skipped | lint PASS | Non rieseguito in questa ripresa | Invariato |
| Nuovi cloni Windows e Linux, ripresa 01:19 | 237 casi ciascuno: 235 pass, 2 fail, 0 skipped | PASS | 0 segnalazioni | lint/types/build e audit production PASS |

Il caso aggiunto dopo i cloni verifica che l'assenza di report-core.mjs non lasci
un report verde precedente. La revisione finale del bootstrap richiede quindi
una nuova esecuzione in clone, non si attribuiscono i vecchi risultati a codice futuro.

I cloni sono partiti da HEAD 79996b42cbe6eb02be7c37af188b7a2b9ff2761e pulito,
con overlay esplicito dei soli file locali del laboratorio, normalizzati UTF-8 LF
e inventariati con SHA256. Nessun commit del prototipo esisteva: non sono cloni
di una revisione pubblicata del candidato. Nessun node_modules/cache/.env copiato.
Non sono esecuzioni GitHub Actions.

Linux: Node 24.19.0/npm 11.17.0, Docker Linux 29.8.1, immagine ufficiale
node@sha256:a9f5f7c91a432850b2a8a7797adf5eadb6c733ceed61167806cee7ea7fbc29df.
Container non-root, root filesystem read-only, input read-only, risorse limitate,
nessun socket Docker o secret applicativo montato. Il primo tentativo aveva un
mount temporaneo noexec che impediva l'esecuzione dei tool: quel tentativo non
prova la conformità. Dopo exec sul solo /work effimero i gate operativi passano.

## Differenze rilevate prima della ripresa 01:19 (storico)

1. **Perdita di controllo:** react/display-name non segnala
   `export default React.memo(() => <div/>);` nel candidato nativo, mentre la
   baseline segnala errore. La corrispondenza nominale della regola non basta.
2. **Falso positivo rispetto alla baseline:** no-duplicate-head nativo segnala
   Head duplicati in un Document funzionale, dove la regola originale interviene
   soltanto nel contesto della classe derivata dal Document importato.
   Un controllo più restrittivo non viene accettato implicitamente come equivalente.

Entrambe le differenze sono test rossi effettivi, non skipped/soppresse o trasformate
in confronti che accettano la perdita. Le fixture Pages/App Router sono distinte:
head nativo HTML consentito sotto app/, vietato sotto pages/ come nella baseline.

## Ripresa notturna 01:19 — correzione Next e verifica alternativa React

no-duplicate-head è ora servito dal bridge ESLint, non più dal motore nativo.
Sorgente invariata di @next/eslint-plugin-next 16.3.8: 63 righe, nessuna nuova
dipendenza, riusa define-rule già presente; licenza Next inclusa. SHA256
normalizzato 2f3fe9b461c45f144e606ab989b409117af0e163edd2c9858d84cbf29426254c,
verificato contro il pacchetto installato e inventariato in UPSTREAM.json.
Sette nuovi casi verificano alias Document, tre Head/molteplicità, Head singolo,
classe non derivata, funzione, Head annidati e alias Head. Scope originale
conservato anche dove la regola non interviene: non è una modifica di policy.
Ora 41 regole native, cinque entrypoint bridge; la matrice impedisce doppia ownership.

Ulteriori otto test display-name rendono esplicito un secondo caso mancante:
export default React.forwardRef con callback anonima. Suite locale **237 casi,
235 pass/2 fail/0 skipped**: falliscono memo e forwardRef anonimi; il vecchio
fallimento Next è chiuso. Lint laboratorio PASS, audit completo 0 segnalazioni note.

Verificata sul registro npm la versione corrente eslint-plugin-react 7.37.5:
peer ESLint fino a ^9.7, non 10. Nessuna forzatura. Il vendor completo del suo
display-name richiederebbe una chiusura stimata di 23 file/~162 KB contro ~42 KB
React precedenti: non introdotta automaticamente per il costo di manutenzione.

Provato eslint-plugin-react-x 5.28.2 in artifacts/frontend-react-x-probe ignorato,
non nel laboratorio né nell'app: ESLint 10.13.0, typescript-eslint 8.71.2,
TypeScript 6.0.3, install ignore-scripts/strict-peer-deps e audit 0. Integrità npm
sha512-fymzGI84QzcukuHfbr5VG1sh8ZevLozIghYkt3Cmyr/yK+STvWfGf0pjNQNKw/sMUAZwrm53xRBu1HUoevvVGQ==.
Rule no-missing-component-display-name, severità error. Confrontati nove casi
con React 19.3.0 e gli stessi snippet ora conservati nei test del laboratorio:

| Caso | Originale | Nativo | React-x | Somma nativo + React-x |
| --- | --- | --- | --- | --- |
| export default memo anonimo | 1 | 0 | 0 | 0 |
| export default forwardRef anonimo | 1 | 0 | 0 | 0 |
| variabile assegnata a memo anonimo | 1 | 1 | 1 | 2 |
| export arrow anonimo senza wrapper | 1 | 1 | 0 | 1 |

Gli altri cinque casi (memo nominato, memo/forwardRef annidati, funzione nominata,
displayName esplicito, memo non React) producono zero in tutti e tre i motori.
React-x non è quindi una sostituzione né un complemento diretto equivalente per
questo gap; non aggiunto al manifest/lock candidato. Nessun test rimosso o allentato.
La [documentazione React-x](https://eslint-react.xyz/docs/rules/no-missing-component-display-name)
descrive lo scope ristretto ai wrapper; il risultato qui è quello della versione
installata, non una deduzione dalla sola documentazione. Anche il
[sorgente Oxlint consultato](https://github.com/oxc-project/oxc/blob/main/crates/oxc_linter/src/rules/react/display_name.rs)
non basta a certificare la parità della versione binaria 1.88.0: valgono i test.

Nuovi cloni Windows/Linux avviati con snapshot aggiornato, evidenze in
C:/Users/helpdesk.ISV/AppData/Local/Temp/ramai-clean-conformance-4e8681f3668d4f8aacdb2de2670df3bf/evidence.
Verifica conclusa: entrambi i cloni confermano 237 casi, 235 pass/2 fail/0 skipped,
solo memo/forwardRef anonimi. Install/lint/audit laboratorio verdi; frontend originale
install/lint/type-check/production build e audit production verdi. Lo script termina
con errore correttamente per i due test rossi. Confermato anche il bootstrap finale
aggiornato. Nessuna pubblicazione finché la conformità resta rossa; non esiste una
nuova run GitHub del laboratorio. Backend non rieseguito in questo blocco frontend.
Gitleaks 8.30.1 su copia dei 54 file candidati: nessun leak rilevato; output
redatto. git diff --check PASS, nessuna diff in src/ o .github/. Nessun secret
applicativo incluso nelle prove. Questi controlli non rendono verde la conformità.

## Correzioni già nel solo laboratorio

- Protezione della risoluzione fallback Next da alias ciclici/profondità oltre 128
  nodi: errore controllato che fa fallire il lint, non accettazione silenziosa.
  L'originale produceva RangeError su un ciclo. Non è una nuova CVE dichiarata.
  Il budget è una restrizione intenzionale da valutare prima dell'adozione.
- Report senza falso verde su bootstrap mancante, zero test, riepiloghi incompleti,
  fail/skipped/cancelled/todo. Risultati marcati running prima delle importazioni
  del laboratorio e failed in caso di errore gestito.
- Provenienza e hash includono la modifica Next e tutti i file vendor, compresi
  wrapper CommonJS; un file vendor non inventariato fa fallire l'integrità.
- Copertura aggiuntiva di tutte le 42 regole inizialmente native, casi Pages/App
  Router, severità e molteplicità. Questo non certifica ogni opzione o autofix.

## Evidenze e riproduzione

Usare tooling/frontend-conformance/Verify-Clean.ps1 -Windows -Linux con PowerShell
7.4+, Node/npm richiesti e Docker disponibile. Lo script continua a raccogliere
tutti i gate ma termina con errore se uno fallisce; non maschera la suite rossa.
Cloni/caches sono lasciati nelle directory temporanee per verifica; container --rm.

Evidenze precedenti Windows e primo Linux non valido:
C:/Users/helpdesk.ISV/AppData/Local/Temp/ramai-clean-conformance-16793ea87d7a4af39f2bffb1e82a9a00/evidence.
Evidenza Linux valida:
C:/Users/helpdesk.ISV/AppData/Local/Temp/ramai-clean-conformance-cdf3e9f63c0d4fcd859af81f2556fa9b/evidence.
Ogni directory contiene candidate-files.json e report/TAP pertinenti, non credenziali.

## Piano notturno e pubblicazione

### Decisione tecnica di laboratorio, ripresa 02:18

La matrice esplorativa estesa trova perdite anche per wrapper che restituiscono
null/numeri/nessun valore, createElement, import nominati, CommonJS e wrapper
contenuti in chiamate non React. La baseline riconosce il wrapper indipendentemente
dal ritorno JSX; aggiungere due pattern export non preserva questa semantica.
React-x non colma il gap e aggiunge duplicati. Non sono state implementate
euristiche di soppressione/deduplicazione né abbassate le regole.

Motivazione per ampliare la chiusura vendor nel solo prototipo: riusare l'originale
display-name e Components invece di riscrivere il riconoscimento. Chiusura totale
misurata: 23 file/161853 byte upstream, inclusi helper già presenti (~42 KB);
incremento ~120 KB, sei dipendenze dirette (object.values, es-iterator-helpers,
array-includes, object.fromentries, array.prototype.flatmap, jsx-ast-utils).
Niente indice del plugin o peer forzati. Provenienza/licenza/hash e test upstream
obbligatori; questa crescita è un costo di manutenzione esplicito del candidato,
non approvazione della futura migrazione. Prima dell'adozione servirà approvare
un responsabile degli aggiornamenti. Gli esiti vanno registrati dopo la prova;
finché i gate non passano, nessuna pubblicazione.

Riprese orarie fino alle 08:45 del 20 ottobre, più punto alle 09:00 Europe/Rome,
programmate nella chat. Non equivalgono a esecuzione continua o garanzia di puntualità.
Servono PC acceso, app in esecuzione, accesso al repository, rete e permessi/limiti
disponibili. Nessuna nuova automazione deve essere creata dai singoli run.

Prossime azioni: colmare nel prototipo il gap display-name con copertura equivalente,
valutando il costo di manutenzione prima di estendere la chiusura vendor React;
poi ripetere cloni e scansioni, aggiornare report/handoff e pubblicare solo quando
i quality gate sono verdi. Non disattivare display-name o ammorbidire la baseline.
Nessuna modifica al tooling attivo e nessuna chiusura/proroga di SEC-EX-001.
Il suo albero mantiene le 5 high dev note; audit production precedenti zero.
