# RAMAI — Roadmap prima demo stakeholder

Data: 2026-10-20; aggiornamento 2026-10-21, Europe/Rome.
Indirizzo utente: riallineare il progetto al valore per le PMI e sviluppare una
prima demo utile agli stakeholder. Questo aggiornamento pianifica la verticale;
non dichiara implementate le funzionalità mancanti o approvati servizi esterni.

## Obiettivo dimostrabile

Un titolare entra nel proprio workspace e vede quali conversazioni richiedono
attenzione, capisce il motivo leggendo le fonti e prepara una bozza di follow-up.
La bozza resta modificabile/copiabile e non viene inviata da RAMAI.

Percorso: login → azienda → Today → priorità → messaggi sorgente → bozza → feedback.
Prima un assistente focalizzato su questo compito, non una piattaforma multi-agente
generalista. Modular monolith, stack, tenancy e confini dei moduli invariati.

La specifica di prodotto 02 privilegia email, Today, evidenze e follow-up (US-11,
US-12, US-16, US-18, US-20, US-21, US-25); il documento 06 richiede output AI
strutturati, validazione, provenance e controllo costi. La demo ne implementerà
un sottoinsieme esplicito: non completa tutte le user story o l'MVP.

## Modalità iniziale e limiti

**Confermato dall'utente il 21 ottobre:** demo guidata locale da parte del team,
con dati interamente sintetici, non beta pubblica o accesso autonomo clienti.
Un'eventuale prova remota richiede nuova decisione, deployment e ulteriori gate,
senza bypass impliciti.

- Due aziende fittizie per dimostrare separazione dei dati.
- Dataset piccolo e riproducibile di conversazioni, con casi positivi e negativi.
- Nessuna casella Microsoft/Gmail reale, OAuth, invio email o dati di clienti.
- Analisi AI reale per dichiarare completata la demo AI; fixture e mock solo nei
  test, sempre identificati. Nessuna risposta preregistrata presentata come generata.
- Provider, modello, modalità di gestione credenziali e tetto di spesa richiedono
  decisione esplicita prima di attivare chiamate esterne. Nessun servizio acquistato.
- Un mockup anticipato può validare il flusso visivo ma va etichettato prototipo,
  non demo funzionale completata.

L'uso di input sintetici NON autorizza l'esposizione pubblica o l'indebolimento di
auth/tenant isolation. MFA, email operativa, privacy e release review restano gate
prima della beta secondo la policy approvata; non dichiarati completati dalla demo.

## Stato di partenza verificato

Foundation, diagnostica, logging e CI presenti. Identity/Tenants S1-004C/D e
005–008 pubblicati, CI reale 278 test verdi/0 skipped. S1-009 UI protetta ora
implementata localmente: 278 backend, 19 client/componenti e 9 E2E HTTPS verdi,
non ancora pubblicata o verificata in CI. [Baseline](protected-ui-baseline.md).
AI/Today/fonti/bozze non implementati. D1 ha il percorso tecnico locale, non la
review trasversale S1-010 o la readiness per clienti. Laboratorio tooling fuori
percorso critico; i conteggi test non misurano una percentuale di prodotto.

## Milestone orientate al risultato

| Milestone | Risultato visibile | Accettazione minima |
| --- | --- | --- |
| D1 — Accesso e workspace | Login, area aziendale protetta, logout | Sessioni/CSRF/revoca testati; letture e scritture A/B isolate; percorso browser reale |
| D2 — Sorgenti demo | Conversazioni consultabili nell'azienda corretta | Import sintetico idempotente; source ID stabili; nessun connector reale |
| D3 — Prima utilità AI | Richieste/impegni rilevati con fonte | Output strutturati validati, evidenze appartenenti al tenant, niente fatti inventati; errori/costi gestiti |
| D4 — Today e follow-up | Priorità spiegate, dettaglio e bozza modificabile | Ranking spiegabile, data ambigua segnalata, nessun invio; feedback minimo persistente |
| D5 — Prova stakeholder | Percorso riproducibile su clone pulito | E2E e CI verdi, script presentazione, dataset ripristinabile, limiti e feedback registrati |

D1 riusa il backlog Sprint 1: S1-004C/D → S1-005/006/007 → API/UI minime
S1-008/009, con test per ogni blocco. I blocchi sono implementati, S1-009 solo
locale; non marca completato l'intero Sprint 1: review security, CI e bootstrap
finali mantengono i loro gate.
D2–D4 sono una verticale successiva dedicata, non un cambio retroattivo dello
scope Sprint 1. Task dettagliati in [DEMO_BACKLOG](../tasks/DEMO_BACKLOG.md).

## Sceneggiatura di accettazione

1. Accedere come utente di Alfa Demo e aprire Today.
2. Vedere una richiesta di preventivo in attesa, con motivo e messaggi sorgente.
3. Vedere un impegno scaduto; non vedere un falso allarme per un thread già risposto.
4. Aprire una bozza basata sulle sole informazioni disponibili, modificarla/copiarla.
5. Marcare utile/non utile e vedere lo stato persistito.
6. Provare l'accesso a una risorsa valida di Beta Demo: negato lato server.
7. Simulare provider indisponibile: errore comprensibile, nessun risultato inventato.

Dataset proposto: 4 scenari (richiesta senza risposta, impegno scaduto, thread
risolto, scadenza ambigua), con più messaggi per scenario e aspettative versionate.
Le valutazioni di qualità e latenza sono misurate, non percentuali inventate.

## Ownership tecnica

- Identity/Tenants: credenziali, sessioni, membership e contesto autorizzato.
- Memory: sole sorgenti normalizzate/evidenze necessarie alla demo, tenant-scoped;
  niente knowledge graph/RAG generale obbligatorio per questa verticale.
- Insights: risultati proposti, scoring spiegabile, stati e feedback minimi.
- Assistant: sintesi/bozza e orchestrazione applicativa tramite contratti;
  provider adapter dietro astrazione, non dipendenza del dominio dal fornitore.
- Connectors e Billing: restano fuori dalla prima demo, salvo decisione separata.
- API/frontend: contratti e percorso utente; mai accesso diretto a tabelle altrui.

## Debito e disciplina di consegna

Nessun ulteriore ampliamento del laboratorio frontend nel percorso critico.
SEC-EX-001 rimane aperta e visibile, senza proroga: solo sviluppo interno entro
30 ottobre/fine Sprint 1/prima di clienti o dati reali, quale evento arrivi prima.
Prima di presentare o distribuire artefatti fuori da tale perimetro serve review
esplicita: la demo non è una scappatoia all'eccezione. Migrazione tooling solo con
scope/decisione dedicati e senza sottrarre implicitamente task alla verticale.

Ogni consegna riporta: cosa si può provare, comando/URL, test effettivi, limiti e
prossimo blocco. Nessun task dichiarato finito sulla base della sola documentazione.
Nessun commit/push o deployment automaticamente autorizzato da questo piano.

## Calendario e decisioni aperte

Target indicato dall'utente: entro il 30 novembre 2026, prova generale proposta
nella settimana precedente. Non è una garanzia di consegna o data di beta.
Previsione proposta il 21 ottobre: prima prova AI interna **6–13 novembre**,
generale entro **20 novembre**, stakeholder entro **30 novembre**. Condizionata
a capacità di lavoro, scope email/evidenze/Today/bozza senza invio invariato,
chiusura gate e scelta tempestiva provider/modello/budget. La demo tecnica
accesso/workspace è già verificata localmente in S1-009, non è ancora quella AI.
Nome pubblico scelto: Agentilio; RAMAI resta il nome tecnico per ora. Dopo la scelta provider,
stimare i blocchi con capacità disponibile e checkpoint D1/D3 prima della prova.

Decisioni che cambiano tempi/perimetro:
1. Data e durata esatte della presentazione (modalità locale sintetica confermata).
2. Provider/modello AI e budget massimo per le prove sintetiche.
3. Priorità commerciali diverse dal caso email/follow-up già previsto in specifica.

Prossimo blocco da autorizzare: S1-010 review/regressione security trasversale,
con pubblicazione/verifica CI S1-009 solo su istruzione. Nessun avvio implicito
D2–D4 o ritorno a ricerche tooling. S1-011/012 restano aperti.
