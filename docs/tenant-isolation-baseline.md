# S1-007 — Isolamento ORM tenant-scoped

20 ottobre 2026. Scope: dati Tenants esistenti, nessuna Business Memory o RLS.

## Percorsi distinti

- `TenantDataContext` operativo: costruttore con `TenantContext` verificato, tenant
  immutabile, nessun pooling; la factory risolve nuovamente sessione e membership.
- Filtri su Tenant, Membership e registro idempotente, parametrizzati dall'istanza
  del contesto, non dal primo tenant incontrato nella cache del modello EF.
- Tracking rifiuta entità di altro tenant; SaveChanges sincrono/asincrono forza
  DetectChanges e verifica TenantId corrente/originale prima di ogni scrittura.
  TenantId è concurrency token; il registro provisioning non è mutabile qui.
- Interceptor rifiuta IgnoreQueryFilters, anche compilato/named, raw SQL e bulk;
  command source ammessi soltanto LINQ e SaveChanges. Nessuna eccezione libera
  per un parametro del client, nessun parsing SQL assunto come barriera di sicurezza.
- `TenantsStoreContext` resta globale **soltanto** per migrazioni/bootstrap, lookup
  membership per coppia esatta e provisioning verificato. Architecture test limita
  i consumer produttivi ai file recensiti. Mapping condiviso in TenantsModel,
  senza migrazione/schema aggiuntivo. Comandi EF richiedono ora `--context
  TenantsStoreContext`, come nelle [procedure](database-migrations.md).

## Test e limiti

20 integration test reali: liste/proiezioni/navigazioni/Find/no-tracking A/B,
stesso utente nelle due aziende, insert/update/delete leciti, detached stranieri,
chiavi current/original manomesse, batch misto senza commit parziale, registro
immutabile, bypass filtri/raw/bulk/compiled, cache/concorrenza e sessione revocata.
Un test iniziale attendeva erroneamente il rifiuto della chiave al SaveChanges:
EF lo produce già all'assegnazione; corretto il test, non indebolito il controllo.
20/20 superati senza skipped; suite completa e scan nel [report](sprint1-progress.md).

Questi sono controlli di codice/ORM, non isolamento contro codice server malevolo
con accesso diretto alle credenziali PostgreSQL. Non dichiarano RLS o permessi DB
per tenant. Dopo un'eccezione di isolamento il contesto va scartato. Il TenantContext
è uno snapshot ammesso al confine dell'operazione: non annulla retroattivamente
operazioni già iniziate quando membership/sessione vengono revocate in concorrenza.
L'host deve usare la factory/risolvere il contesto per ogni nuova operazione;
costruire un contesto non sostituisce RBAC dei comandi o il confine cookie HTTP.
Nessun endpoint/UI introdotto da S1-007. Integrazione HTTP successiva S1-008,
autorizzata separatamente nello stesso piano notturno. Nessuna beta esterna implicita.
