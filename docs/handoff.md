# RAMAI — Handoff operativo

Snapshot: 2026-10-19, Europe/Rome. Contesto stabile in [memory.md](memory.md).

## Stato in una frase

Sprint 0/M0 chiuso con SEC-EX-001 accettata temporaneamente per sviluppo interno;
baseline pubblicata e CI verde. Debito non risolto, riesame entro 30 ottobre o prima.
S1-001 documentale completato; S1-002 modelli/contratti completato localmente il 17 ottobre;
S1-003 persistence/migrazioni completato localmente il 18 ottobre dopo richiesta di
prosecuzione e avvio Docker. S1-004/login e task successivi non iniziati.
Il 19 ottobre l'utente ha autorizzato verifica, commit/push e controllo GitHub del
lavoro già svolto. Pubblicazione in corso; esito e riferimenti nel report Sprint 1.
Nessuna autorizzazione a iniziare S1-004 in questo blocco.

## Ultimo blocco — S1-003

Due adapter sotto `src/backend/Persistence/`, DbContext/migrazioni/snapshot separati
Identity/Tenants; ASP.NET Identity standard come singolo store account, membership
con PK TenantId/UserId e vincoli PostgreSQL. Reader contracts implementati, non
registrati nell'API. Nessun auto-migrate, endpoint, login o nuovo servizio.
[Procedure/ownership/recovery](database-migrations.md) e [report](sprint1-progress.md).
Test reali eseguiti su Compose temporaneo con password casuale; dati/volumi esistenti
non modificati. Risorse temporanee rimosse. Nessun `.env` creato.
Gate finali locali: build 0 warning/errori, 58 test superati/0 skipped, format e
snapshot EF coerenti, nessun advisory NuGet o leak Gitleaks rilevato nelle scansioni.
Nessuna nuova run GitHub; debt npm preesistente non risolto né riauditato qui.
Il prossimo blocco è S1-004, da autorizzare separatamente e suddividere secondo policy
prima del codice; non saltare cookie/CSRF/revoca/email/MFA per accelerare la tabella.
Le sezioni storiche successive descrivono gli esiti alla rispettiva data, non prevalgono
su questo snapshot. SEC-EX-001 resta temporanea e non autorizza beta o dati cliente.

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

1. **Documentazione riallineata e pubblicata.** README e progress report riportano ora
   commit S0-012 e run verde, preservando lo storico. Le modifiche documentali prima
   locali sono ora pubblicate in 258e603; chiusura M0 e accettazione del debt
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
   cloud EU sono confermati dalla rilettura delle specifiche 04/08. Provider concreto,
   deployment e modello commerciale restano da definire nel progetto. Nessun deploy effettuato.

## Pianificazione preparata il 16 ottobre

Richiesta interpretata come preparazione documentale della strategia di sviluppo
controllato, non firma dell'eccezione o autorizzazione a implementare Sprint 1.

- [SEC-EX-001](security-exception.md): stato PROPOSTA, approvatore non assegnato;
  solo sviluppo interno, non beta esterna/commercializzazione. Riesame proposto al
  30 ottobre o fine Sprint 1/prima di clienti reali se antecedente. Nessuna proroga implicita.
- [Sprint 1 backlog](../tasks/SPRINT1_BACKLOG.md): 12 task proposti Identity/Tenants/DB,
  auth/sessioni, contesto e isolamento, ruoli, UI minima, test e gate CI.
- Primo task proposto S1-001: decisione modello auth e threat model, soltanto documenti;
  prima del codice servono conferma M0/decisione rischio, scope e ADR approvati.
- Fonti lette: PDF 04/08/11/12. Confermata la sequenza Identity + Tenant + DB baseline;
  conservare layout/moduli attuali nonostante le varianti nei diagrammi PDF.
- Decisioni mancanti: auth locale versus IdP/protocollo, BFF/cookie/CSRF e lifecycle,
  membership/tenant attivo, ownership schema e contratti tra Identity e Tenants.
  Non assumere scelte implicite; nessuna dipendenza o schema modificato.
- Le nuove bozze, memory/handoff e il progress report sono modifiche solo documentali
  locali. Nessun commit/push, nuova run, build, audit npm o feature in questa sessione.

## Prossimo percorso proposto, da autorizzare

### Aggiornamento corrente — S1-002 completato localmente

Utente: "Procedi esclusivamente con S1-002, mostrando prima i file previsti e la
strategia dei test". Preflight completato e scope rispettato; modifiche pregresse preservate.
Implementati UserAccount globale, Tenant/Membership/ruoli/status, bootstrap Owner,
invarianti in memoria e contratti reader/DTO in Application senza credenziali.
Moduli senza nuovi riferimenti, nessun pacchetto o DB. Aggiunti 32 casi unit e
2 architecture, mantenuti tutti i test precedenti e il divieto di dipendenze tra moduli.
Documentazione: [identity-tenant-model.md](identity-tenant-model.md) e
[sprint1-progress.md](sprint1-progress.md), backlog e memory aggiornati.

SDK effettivo 10.0.402, build Release 0 warning/errori, 49 test pass/0 fail/1 skipped,
format verde. Skipped il test reale PostgreSQL/Redis/pgvector: Docker Desktop non
disponibile; nessun servizio avviato/volume alterato, nessuna nuova run GitHub.
Non equiparare i test in memoria all'isolamento HTTP/SQL futuro. Vincoli globali
email e concorrenza restano da realizzare nel DB; reader non implementati/registrati.
ConfirmEmail e assegnazione Owner non validano ancora token o identità HTTP.

File di codice limitati a modelli, contracts e test; nuovo documento ownership,
report/backlog/memory/handoff aggiornati. Nessun commit/push. Prossimo task S1-003
solo dopo richiesta; non aggiungere persistence o auth come completamento implicito.
Le sezioni storiche che indicano S1-002 non avviato sono superate da questo aggiornamento.

### Decisione corrente — parametri tecnici S1-001 approvati il 17 ottobre

Risposta utente: "ok parametri tecnici S1-001 approvati". ADR-009 Accepted per il
modello locale corrente e baseline ID-01–07 approvata, controlli non implementati.
S1-001 chiuso come task di analisi/decisione, non come prova che l'autenticazione funzioni.
Registrato in [sprint1-progress.md](sprint1-progress.md). Le sezioni storiche seguenti
che descrivono parametri ancora da approvare sono superate da questo aggiornamento.

Riassunto parametri: password 15–128, lockout 5/15 minuti, conferma email 24 ore,
reset 30 minuti; idle 30 minuti/assoluta 12 ore; antiforgery/revoca e fail-closed;
MFA TOTP Owner/Admin prima beta. Niente CIAM, token server o team/reparti aggiunti.
Impostazioni del framework, benchmark, session store/keyring, rate limit aggiuntivi
e test da realizzare nei task pertinenti, senza dichiararli già verificati.

Non approvati come fatti: conformità GDPR/NIS2, classificazione NIS2, provider email,
hosting, prezzi, nuove risorse, release o intero backlog. Review privacy/organizzativa
e applicabilità ACN ancora necessarie prima della beta. SEC-EX-001 invariata fino al riesame.

Prossimo blocco di codice: esclusivamente S1-002 modello/contratti con file e test da
mostrare prima; niente migrazioni S1-003 o login S1-004 impliciti. La sola approvazione
dei parametri non avvia questi task. Nessun codice, test runtime, commit o push nel blocco.

### Policy locale e indirizzo NIS2/GDPR — aggiornamento corrente

L'utente propone NIS2 e GDPR come policy. Registrato come obiettivo di sicurezza/privacy,
non attestazione di compliance. Nuovo [identity-security-policy.md](identity-security-policy.md)
con controlli ID-01–07, valori tecnici proposti e gate organizzativi pre-beta.
Password minimo 15/max supportato >=64 (proposta 128), blocklist e niente rotazione
arbitraria da riferimento NIST, non da presunto obbligo GDPR/NIS2. MFA Owner/Admin
prima della beta, revoca/sessioni, email/reset e test reali pianificati. Nessun codice.

Applicabilità NIS2 e misure ACN da validare con referente competente; 500 MAU non la
determinano. Fonti normative UE consultate, portale ACN non accessibile (403): nessuna
pretesa di aver verificato ogni misura italiana. Identità locale non richiede Entra.
Privacy: ruolo per finalità, retention, diritti, fornitori/accordi e incidenti da
progettare/revisionare; identificativi pseudonimi non dichiarati anonimi.

File di questo blocco: baseline nuova, ADR-009, backlog, memory e handoff. Solo
documenti, controllo link/whitespace; nessun codice, pacchetto, test runtime o commit/push.
Prossimo passo: approvare policy tecnica e risolvere decisioni residue S1-001 prima
dei task di codice; nessuna eccezione al gate beta/commercializzazione.

### Decisione più recente — M0 chiuso e debt accettato temporaneamente

Risposta umana esplicita: "si procediamo accettando il debito fino al riesame",
16 ottobre, alla richiesta di chiusura M0 con eccezione solo sviluppo interno.
SEC-EX-001 ora ACCETTATA TEMPORANEAMENTE; scadenza/riesame 30 ottobre o prima se fine
Sprint 1/clienti o dati reali, nessuna proroga automatica. Nessun rilascio commerciale
autorizzato. Referente operativo nominativo da assegnare per il riesame; nessun monitor
automatico creato. M0 è chiuso sulla base delle evidenze e CI già pubblicate.

Run riconfermata: 38887663889, commit 4c441b3, backend/frontend success. Nessuna nuova
run/build/test/audit oggi per questa modifica documentale. Eccezione, README, report,
memory/handoff, backlog e ADR aggiornati solo localmente, nessun commit/push.
Le sezioni storiche sotto che indicano SEC-EX-001/M0 aperti sono superate qui.
Resta S1-001: completare/revisionare policy security locali; S1-002+ non avviati.

### Revisione auth — direzione corrente locale

Modello proposto e poi confermato dall'utente: single/multi user, collaborazione nello
stesso workspace e predisposizione mono/multi azienda. ADR-009/backlog registrano
User–Membership–Tenant, ruolo per membership, senza single-user che salta tenancy
o Owner globale. Gruppi/reparti rinviati; nessuna entità Team. Scope UI multi-workspace
ancora da definire. Conferma di prodotto, non firma del rischio o approvazione di tutto l'ADR.
Tenere separate cardinalità utenti/aziende e piani commerciali; niente billing o
aggregazione dati cross-azienda impliciti. Solo quattro documenti aggiornati, nessun codice.

L'utente chiede di partire senza Entra External ID e gestire gli account in RAMAI
per validare il mercato prima di assumere costi CIAM. ADR-009 ora propone ASP.NET
Core Identity + PostgreSQL/EF Core e cookie first-party nel monolite, non una nuova
applicazione o sicurezza da zero. La proposta managed seguente è solo storico.

Email di qualsiasi dominio verificata; social login e connector separati. Dev/test:
sender in memoria, dati sintetici, mai token in console/API pubbliche. Prima di utenti
esterni serve un canale email reale per conferma/reset, con provider/infrastruttura
da approvare se nuovi. Nessun costo attivato. Perimetro cookie-only non dichiarato OIDC:
approvare lifecycle e security model prima del codice. SEC-EX-001/M0 restano aperti.

File del blocco: ADR-009, backlog, memory e handoff, solo documenti; link/whitespace
verificati. Nessun pacchetto, migration, account, test runtime, commit/push. Prossimo
passo: concludere decisioni S1-001 sul modello locale, non riproporre CIAM come obbligo SaaS.

Ultimo blocco: conferma del modello registrata nei quattro documenti; nessun codice
avviato. Prima di implementare S1-002 restano approvazione chiusura M0/SEC-EX-001 e
review dei dettagli security S1-001. Non considerare un generico "procediamo" come
accettazione implicita di vulnerabilità high o autorizzazione a nuove risorse esterne.

### Storico S1-001 — analisi managed iniziale, superata per l'MVP

- Utente conferma SaaS e fino a 500 utenti attivi mensili nella prima fase; non numero
  aziende. Domanda aperta: accesso anche Gmail/altri sistemi. Proposta: qualsiasi
  email verificata, social login opzionale separato dal consenso dei connector.
- Nuovo [ADR-009 Proposed](adr/ADR-009-identity-access.md): managed CIAM OIDC e API/BFF,
  membership/ruoli/tenant in RAMAI. Entra External ID candidato principale, Auth0 alternativa;
  nessun provider o piano scelto/attivato. Riservato ADR-008 al tema citato dal PDF 04.
- Include confronto locale/gestito, sessioni/cookie/CSRF/revoca, mapping issuer+subject,
  onboarding/ruoli proposti, threat model e test richiesti, costi da stimare e fonti ufficiali.
- Importante: blocco account IdP non revoca automaticamente cookie RAMAI già emessi;
  meccanismo concreto e ritardo massimo restano da validare. Directory IdP non è TenantId.
- Per 500 MAU core Entra nel free tier pubblicato, ma nessun preventivo totale o budget
  approvato; feature aggiuntive, email, MFA/supporto e data residency da verificare.
- Solo quattro file modificati in questo blocco: ADR nuovo, backlog, memory, handoff.
  Nessun codice, pacchetto, migration, account, risorsa cloud, test runtime o commit/push.
- S1-001 non Done: mancano approvazione umana del security model e decisioni concrete.
  La richiesta di analisi non firma SEC-EX-001 né chiude M0.

1. Riallineamento README/report e audit npm del 16 ottobre completati localmente.
2. Prova isolata eseguita: correzione parziale script applicata e pubblicata, vulnerabilità
   ed ESLint EOL restano. Non migrare forzatamente né rimuovere regole per azzerare audit.
   Correzione pubblicata/verificata con autorizzazione. Prossima scelta: accettare
   temporaneamente il debt residuo oppure approvare un intervento più ampio.
3. SEC-EX-001 accettata e M0 chiuso: rispettare condizioni e scadenza, assegnare il
   referente operativo e registrare il riesame; nessuna estensione implicita a clienti reali.
4. S1-001 approvato e S1-002 implementato localmente. Prossimo blocco da autorizzare:
   S1-003 persistence/migrazioni con file, rischi e test PostgreSQL da proporre prima;
   verificare Docker e vincoli globali. Nessun CIAM o login S1-004 implicito.
   Non iniziare autenticazione, tenancy o altre feature sulla base di questa bozza;
   scelte security richiedono review e approvazione umana.

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
