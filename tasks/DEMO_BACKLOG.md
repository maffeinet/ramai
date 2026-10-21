# RAMAI — Backlog verticale demo stakeholder

Data: 2026-10-20. Piano esecutivo proposto dopo richiesta di riallineamento.
Fonte: [roadmap demo](../docs/demo-roadmap.md). Nessuno dei task sotto è completato
da questo documento. Sprint 1 conserva numerazione e acceptance criteria originali.

| Blocco | Scope minimo | Dipendenza | Stato |
| --- | --- | --- | --- |
| DEMO-01 / D1 | S1-004C e accesso/workspace protetto, contesto e isolamento A/B, API/UI minime | A/B presenti; policy approvata | Da implementare in sottoblocchi |
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

1. S1-004C con test sessione/scadenza/revoca/CSRF e logging redatto.
2. TenantContext/provisioning/isolation con test reali, poi UI protetta.
3. Dati sintetici e adapter AI dopo decisione provider/budget.

Annunciare per ciascun sottoblocco file, dipendenze e test prima delle modifiche.
Non riaprire il laboratorio tooling salvo nuova evidenza bloccante o scadenza.
