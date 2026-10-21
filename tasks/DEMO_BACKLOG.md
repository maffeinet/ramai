# RAMAI — Backlog verticale demo stakeholder

Data: 2026-10-20, aggiornato 21 ottobre. Piano esecutivo dopo riallineamento.
Fonte: [roadmap demo](../docs/demo-roadmap.md). Demo guidata locale sintetica
confermata. Sprint 1 conserva numerazione e acceptance criteria originali.

| Blocco | Scope minimo | Dipendenza | Stato |
| --- | --- | --- | --- |
| DEMO-01 / D1 | S1-004C/D e accesso/workspace protetto, contesto e isolamento A/B, API/UI minime | A/B presenti; policy approvata | Percorso tecnico verificato; S1-009 solo locale, CI/review security ancora aperte |
| DEMO-02 / D2 | Dataset conversazioni sintetiche, caricamento esplicito dev/test, evidenze e lettura tenant-scoped | D1 | Proposto |
| DEMO-03 / D3 | Adapter AI, output strutturati, validazione evidenze, metering/budget e valutazioni | D2 + scelta provider/budget | Proposto, provider da decidere |
| DEMO-04 / D4 | Today, priorità spiegate, dettaglio fonti, bozza modificabile e feedback | D3 | Proposto |
| DEMO-05 / D5 | E2E, clone pulito, CI, sceneggiatura e review readiness | D4 | Proposto |

## Acceptance trasversali

- Nessun tenant fidato da parametro client; test lettura/scrittura A/B.
- Nessuna scorciatoia auth in produzione. Bootstrap dataset/account soltanto in
  ambiente locale/test esplicito, senza password persistenti o secret nei log.
- Ogni risultato importante contiene ID sorgente verificabili nel tenant.
- JSON non valido/evidenza inesistente: risultato rifiutato, non pubblicato.
- Email contenenti istruzioni malevole trattate come dati, non comandi al modello.
- Nessun importo/data certo inventato, nessun invio o modifica esterna autonoma.
- Job lunghi asincroni con stato, failure comprensibile e niente retry illimitati.
- Dati sintetici e reset della demo deterministico/idempotente, senza cancellare
  database/volumi utente esistenti.
- Test mock e analisi AI reale distinti; provider indisponibile non dà falso successo.
- Costi/limiti, latenza e versione prompt/modello osservabili, senza contenuti sensibili nei log.

## Definition of Done della prima demo

Percorso D1–D5 eseguito e registrato con scenari positivi/negativi; gate verdi e
nessuno skipped security, CI del commit verificata, fonti e bozza visibili nella UI,
limiti comunicati agli stakeholder e feedback raccolto. Non equivale a beta o
commercial readiness. Eccezione tooling e gate MFA/privacy non bypassati.

Fuori scope: multi-agente generalista, connector reali, import storico di caselle,
CRM/ERP, pagamenti, RAG generale, invio email, deploy pubblico e dati clienti.

## Ordine immediato

1. Pubblicazione/verifica CI S1-009 se autorizzata; S1-010 review trasversale,
   poi S1-011/012. C/D e 005–008 già pubblicati, UI verificata localmente.
2. Dati sintetici di prodotto e adapter AI dopo scope dedicato/provider/budget.
3. Prima prova AI proposta 6–13 novembre, generale entro 20 e stakeholder entro
   30 novembre 2026: previsione condizionata, non garanzia. Riesame SEC-EX-001
   entro la sua scadenza anticipata, non prorogato da questo calendario.

Annunciare per ciascun sottoblocco file, dipendenze e test prima delle modifiche.
Non riaprire il laboratorio tooling salvo nuova evidenza bloccante o scadenza.
