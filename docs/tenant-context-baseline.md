# S1-005 — TenantContext verificato server-side

Aggiornamento 21 ottobre: il wiring e la selezione HTTP sono ora implementati in
[S1-008](workspace-api-baseline.md), dopo approvazione esplicita dell'estensione
ADR-010 host-only. Le limitazioni HTTP descritte sotto sono il checkpoint S1-005.

Data: 2026-10-20, Europe/Rome. Autorizzazione esplicita dell'utente per il solo
S1-005. Componenti Application interni; nessun provisioning, filtro SQL business,
endpoint workspace, UI, commit/push o nuova infrastruttura.
Riferimenti: [modello](identity-tenant-model.md), [ADR-009](adr/ADR-009-identity-access.md),
[policy](identity-security-policy.md), [report](sprint1-progress.md).

## Perimetro e confini

TenantContext è uno snapshot immutabile della richiesta, con TenantId, UserId,
ruolo di membership e prova MFA. Il costruttore non è pubblico; viene emesso solo
dal resolver dopo i controlli. Non è un DTO di input, un token o una cache permessi.

TenantContextResolver dipende esclusivamente da IAccountSessionValidator,
ITenantMembershipReader e TimeProvider. Identity verifica il proprio account;
Tenants legge soltanto le proprie membership. Application non usa DbContext,
HttpContext, UserManager o tipi dei moduli. Nessun nuovo riferimento fra progetti,
schema, pacchetto o dipendenza infrastrutturale. Il timeout assoluto di dodici ore
è ora condiviso tramite AccountSessionClaims.MaximumAgeSeconds, senza cambiarlo.

Vincolo di integrazione: ADR-010 consente attualmente alla composition API soltanto
l'adapter Identity. Questo blocco **non aggiunge** una dipendenza API→Tenants né
aggira il vincolo con reflection/service locator. L'host attuale non registra il
resolver/reader Tenants e non espone nuove route. La futura composition e il
trasporto HTTP appartengono a S1-008 e richiederanno revisione/approvazione del
riferimento host-only necessario. Il contratto interno è verificato con gli adapter
reali nei test; non viene dichiarata già disponibile la selezione da browser.

## Risoluzione e selezione

- ResolveAsync riceve esclusivamente il principal prodotto dal cookie handler
  configurato. Non prende TenantId da header, URL, query o body. Richiede una sola
  identità autenticata Ramai.Session, UserId non vuoto, stamp/inizio univoci e validi,
  timeout assoluto rispettato e sessione ancora ammessa dal validator Identity.
- Nessun tenant selezionato: SelectionRequired, mai primo tenant o default globale.
  Claim duplicati/tenant malformato: Denied. Identità/sessione invalida: Unauthenticated.
- Il claim ramai.selected_tenant è soltanto la selezione conservata nel ticket
  protetto: **non** costituisce appartenenza. La membership attiva viene riletta
  a ogni chiamata, senza cache di ruoli o risultato positivo precedente.
- Il reader deve restituire la coppia esatta tenant/utente e un ruolo noto. Mismatch,
  membership assente/inattiva o ruolo sconosciuto: Denied, nessun contesto.
- Il ruolo proviene soltanto dalla membership, mai da ClaimTypes.Role o input client.
  Owner/Admin richiedono la prova MFA della sessione verificata: altrimenti
  MfaRequired e nessun contesto privilegiato. Member può usare il contesto senza MFA.
- SelectAsync riceve una proposta non fidata di TenantId; ripete tutti i controlli
  e produce soltanto se ammessa un nuovo principal con il tenant selezionato.
  Non modifica l'input, non inserisce ruoli nel cookie e non cambia stamp/istante
  iniziale o estende la durata assoluta. Una selezione negata non cambia nulla.

SelectedPrincipal è un handoff interno al futuro host, mai un body JSON da
serializzare al client. Il transport dovrà applicare HTTPS/CSRF, validare la
sessione con l'handler cookie e salvare la selezione nel ticket protetto tramite
handler standard. La semplice stringa AuthenticationType o un principal costruito
dal client non prova crittograficamente l'autenticazione: il resolver non decifra
cookie e non può sostituire quel confine. Nessun endpoint attuale accetta principal
o claim dal browser. Cookie manomessi sono coperti dai test C, non da una finta
verifica della firma nel servizio Application.

Il contesto va risolto nuovamente per ogni operazione/richiesta protetta; non usare
il risultato precedente dopo cambi membership, ruoli o stamp. Non ci sono setter
del tenant corrente, singleton mutabili, AsyncLocal o fallback globali. Registrazione
futura scoped e DbContext separati per richiesta. Le eccezioni delle dipendenze e la
cancellazione propagano senza produrre contesto; il futuro host dovrà mantenere
gli errori sanitizzati/fail-closed già previsti, non trasformarli in accesso valido.

## Prove e limiti

Unit test: principal anonimo/altro scheme, claim duplicati/malformati, timeout,
nessun tenant, membership assente/mismatch/ruolo sconosciuto, gate MFA per ruolo,
nessuna mutazione input/estensione sessione, revoca, cancellazione e guasto reader.

Integration: PostgreSQL/Redis dedicati e servizi account/sessioni veri, senza
validator auth simulato. L'identità è prodotta dal servizio login reale con password;
la prova MFA nel caso Owner deriva da enrollment HTTP reale e login recovery
verificato, non da un claim appiccicato nel test. Verificati Owner in A/Member in B,
assenza accesso a C, deactivation/role reload, account inattivo/non confermato/
bloccato/revocato e venti richieste con scope/DbContext separati su A/B. I dati
aziendali sono solo fixture sintetici: non è implementato il provisioning S1-006.

Gate finali nel [report](sprint1-progress.md): **178 pass/0 skipped**, 64 unit,
107 integration e 7 architecture; 32 casi nuovi rispetto a C/D. Build Release
0 warning/errori, format e frontend PASS; Gitleaks 38 file senza leak.
Non sono test HTTP dell'endpoint di selezione inesistente o E2E browser. Non si
dichiara isolamento completo di letture/scritture business (S1-007) o RBAC endpoint
(S1-008). Una membership/ruolo modificata dopo una verifica già ammessa non annulla
retroattivamente l'operazione in corso: i blocchi write futuri dovranno verificare
al loro confine. Nessuna entità Tenant.IsActive inventata: il modello attuale ha
stato membership, non sospensione globale azienda.

Restano email/operatività account, keyring/backup, audit segregato, UI e review
security prima dell'esterno. SEC-EX-001 e warning npm invariati, nessuna proroga.
Prossimo task da autorizzare: S1-006 provisioning workspace/Owner, non implementato.
