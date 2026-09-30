# AGENTS.md — RAMAI

## Regole non negoziabili
1. Modular Monolith, non microservizi, per l'MVP.
2. Backend ASP.NET Core .NET 10.
3. Frontend Next.js + React + TypeScript.
4. PostgreSQL come datastore primario; pgvector previsto.
5. Redis per cache/short-lived state.
6. Ogni entità tenant-scoped deve avere `TenantId`.
7. Il tenant deriva dal contesto autenticato, mai da un parametro libero fidato del client.
8. Vietato accesso diretto alle tabelle interne di un altro modulo.
9. Comunicazione tra moduli via application contracts o eventi interni.
10. Nessun secret nel codice, commit o log.
11. Output AI futuri sempre strutturati e validati.
12. Nessuna azione irreversibile autonoma nell'MVP.
13. Ogni modifica deve avere test proporzionati al rischio.
14. Logging strutturato con correlation id.
15. Operazioni pesanti future asincrone.
16. Non introdurre dipendenze infrastrutturali non previste senza ADR.
17. Non implementare feature fuori Sprint 0 senza istruzione esplicita.
18. Non cambiare stack, tenancy model o module boundaries senza approvazione umana.

## Quality gate
- build green
- test green
- lint/format green
- nessun secret
- acceptance criteria verificati
- documentazione aggiornata

## Working style
Per ogni task: leggere acceptance criteria, proporre i file da modificare, implementare il minimo necessario, eseguire test, riportare file modificati/test/limiti residui. Evitare refactor estesi non richiesti.
