# S1-002 — Modello Identity/Tenants e contratti

Aggiornamento S1-003, 18 ottobre: vincoli DB e reader ora implementati negli adapter
separati, senza alterare il modello di tenancy o introdurre auth. Questa pagina
conserva il perimetro storico S1-002; stato corrente e dettagli in
[database-migrations.md](database-migrations.md).

Implementato localmente: 2026-10-17. Scope: modelli puri e contratti, non persistenza
o autenticazione. Riferimenti: [ADR-009](adr/ADR-009-identity-access.md),
[policy](identity-security-policy.md), [backlog](../tasks/SPRINT1_BACKLOG.md).

## Ownership e forma del modello

| Elemento | Owner | Identificatore/scoping | Garanzia attuale |
| --- | --- | --- | --- |
| UserAccount | Modulo Identity | UserId globale, nessun TenantId | ID non vuoto, email valida/normalizzata, stato attivo e conferma distinti |
| Credenziali/password future | Modulo Identity | Associate allo stesso UserId | Nessun campo password/hash o store implementato in S1-002 |
| Tenant | Modulo Tenants | TenantId della singola azienda | ID/nome validi, esattamente un Owner iniziale |
| Membership | Modulo Tenants | TenantId + UserId | Coppia unica nell'aggregato, ruolo/stato per singola azienda |
| DTO e reader contracts | Ramai.Application.Contracts | Identificatori e snapshot minimi | Nessun riferimento a entità di modulo, ORM o credenziali |

Quando verranno introdotte tabelle: Identity possiederà utenti/credenziali; Tenants
possiederà tenant/membership. Non si aggiunge ora una seconda tabella utenti o un
mirror credenziali: l'integrazione con ASP.NET Core Identity in S1-003/004 deve
definire mapping e source of truth univoca usando UserId stabile. Non leggere
direttamente tabelle interne di altri moduli né inferire membership dall'email.

I modelli non dipendono da EF Core, Identity framework, provider o shared contracts.
I moduli conservano zero ProjectReference, incluso nessun riferimento reciproco.
I contratti sono nel progetto condiviso Application già esistente, con la dipendenza
Application → Domain invariata. Non si creano nuovi progetti o pacchetti.

## Invarianti e transizioni implementate

- UserAccount.Create rifiuta Guid.Empty ed email vuota/invalida/display name.
  Trim e normalizzazione Unicode/case per la chiave email; niente fusione alias Gmail,
  verifica DNS o presunta prova di possesso dell'indirizzo.
- Account inizialmente attivo ma non confermato. ConfirmEmail è una transizione di
  dominio che il futuro servizio potrà invocare solo dopo verifica del token.
  Deactivate impedisce l'idoneità; una conferma successiva non riattiva l'account.
- CanAccessTenantWorkspaces verifica solo idoneità account, NON appartenenza o
  autorizzazione a una specifica azienda. Il DTO esclude anche l'ID vuoto.
- Tenant.Create richiede tenant ID, nome e owner ID validi e crea l'Owner attivo
  nell'aggregato stesso. Non prova che l'owner sia un utente autenticato/confermato:
  quel gate spetta al futuro provisioning S1-006 e ai contratti Identity.
- AddMember accetta Member/Admin e applica il TenantId dell'aggregato, non un
  TenantId della membership fornito dal chiamante. Ruoli enum non definiti rifiutati.
  Valore predefinito Member, non Owner, sia nel modello sia nel contratto.
- Unicità UserId all'interno dello stesso tenant, anche per membership inattive:
  niente duplicazione o riattivazione implicita tramite una seconda AddMember.
- FindActiveMembership restituisce null per utente estraneo/inattivo. Nessun fallback
  a un altro tenant; lo stesso UserId può avere ruoli differenti in aggregati diversi.
- DeactivateMember non permette di disattivare il primo Owner; aggiunta/trasferimento
  Owner, gestione avanzata ruoli, reactivation, inviti e delete tenant restano fuori scope.
- Membership non costruibile pubblicamente, identificatori/ruolo senza setter;
  collezione read-only e mutazioni di stato mediate da Tenant.

Metodi di dominio non sono endpoint autorizzati: il chiamante applicativo futuro deve
verificare actor, ruolo, contesto tenant e stato Identity prima di usarli. Non si
aggiunge in S1-002 una UI/API che accetta liberamente userId o ruolo dal browser.

## Contratti di sola lettura

- IUserAccountReader.FindAsync: UserAccountInfo o null. Espone UserId/stato/conferma,
  non email, password, hash, token, IdentityUser o UserAccount del modulo.
- ITenantMembershipReader.FindActiveAsync: TenantMembershipInfo per la coppia
  tenant/user o null se assente/inattiva. Espone soltanto ID e TenantAccessRole.
- CancellationToken presente, DTO immutabili; role di contratto distinto da enum
  del dominio per evitare una dipendenza dei contratti dalle entità private dei moduli.
  L'eventuale adapter futuro deve mapparlo esplicitamente, senza cast numerici impliciti.

Queste sono interfacce, non repository implementati: nessun adapter, database,
registrazione DI o nuova route introdotti. Identificatori dei reader devono derivare
dal contesto autenticato verificato nei task successivi. Uno snapshot non è un ticket
di sessione o una cache autorizzativa valida per sempre.

## Vincoli persistenti definiti ma rinviati a S1-003

- UserId e TenantId con chiave primaria, NormalizedEmail univoca globalmente in Identity;
- coppia TenantId/UserId univoca anche per righe inattive, vincoli e indici appropriati;
- appartenenza utenti/tenant e consistenza cross-module tramite contratti secondo
  layout migrations/DbContext da revisionare, senza accesso diretto ad altre tabelle;
- concorrenza provisioning/membership e recovery transazioni da verificare su PostgreSQL.

La normalizzazione email è testata, ma due istanze UserAccount con la stessa chiave
possono ancora essere costruite: l'aggregato non è un catalogo globale utenti.
Non si spaccia questo test per l'unicità del database. Analogamente due aggregati
costruiti con lo stesso TenantId non sono impediti senza repository/vincoli persistenti.
Le collezioni aggregate non sono store singleton/thread-safe da condividere tra richieste.

## Test e limiti della prova

Unit: input invalidi, stato account, normalizzazione/alias, primo Owner, ruoli A/B,
estraneo senza membership, duplicati attivi/inattivi, ruolo indefinito/default,
protezione Owner e collezione. Architecture: test originali conservati più controlli
su framework dependencies, scoping e firme dei contratti.

Queste prove dimostrano invarianti in memoria, non IDOR HTTP, query filter SQL,
autenticazione, token/email reali o isolamento in produzione. Tali prove arrivano
con persistence, TenantContext, API e suite security nei rispettivi task approvati.
