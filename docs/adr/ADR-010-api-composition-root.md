# ADR-010 — Composition root API per Identity e Tenants

Data: 2026-10-20. Accepted limitatamente all'integrazione S1-004C.
L'utente ha rifiutato inizialmente il riferimento, chiedendo completezza security;
dopo chiarimento ha autorizzato: "procedi come avevi pensato, importante ... sicurezza".

Estensione accettata il 21 ottobre: dopo richiesta esplicita di approvare
composizione host-only Tenants e pubblicazione dei prerequisiti, l'utente risponde
"ok procedi con tutto quanto è in sospeso". Include il solo wiring S1-008:
API può referenziare anche l'adapter Tenants esclusivamente in Composition per DI.
Handler dipendono dai contratti Application; nessun DbContext o accesso tabelle
fra moduli. RLS, stack, tenancy, UI/S1-009 e deploy non inclusi.

L'API può referenziare l'adapter Identity per registrare i servizi nel composition
root. I suoi handler usano contratti Application, non DbContext/UserManager/tabelle.
Il riferimento non abilita comunicazione diretta Identity/Tenants o ruoli globali.
Modular monolith, stack, tenancy e ownership tabelle restano invariati.

Il test sul grafo include queste sole eccezioni host; un test aggiuntivo mantiene
il divieto di usare tipi persistence fuori da Composition. Nessuna reflection o
service locator per aggirare i confini. Infrastructure conserva i riferimenti attuali.

MFA/UI/isolation rimangono blocchi necessari successivi per il percorso sicuro;
questo ADR non li considera implementati e non approva una beta senza quei gate.
