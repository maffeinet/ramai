# RAMAI — Baseline identità locale, sicurezza e privacy

Data proposta: 2026-10-16. Approvazione: 2026-10-17, Europe/Rome.
Stato: **PARAMETRI TECNICI APPROVATI — IMPLEMENTAZIONE PARZIALE INTERNA**.
Aggiornamento 19 ottobre: S1-004A implementa componenti ID-01 e primi controlli ID-02,
verificati localmente, non ancora collegati a endpoint/login. Dettagli e limiti in
[account-security-baseline.md](account-security-baseline.md). S1-004B aggiunge conferma
email/reset interni con provider standard, lifetimes 24h/30min e sender solo nei test:
[dettagli B](account-recovery-baseline.md). Nessun endpoint o email reale; ID-04/05 e
parti HTTP/operative non implementate. Nessuna approvazione beta o attestazione normativa.
Riferimento: [ADR-009](adr/ADR-009-identity-access.md), S1-001.
Aggiornamento 20 ottobre: [S1-004C](account-session-baseline.md) aggiunge sessioni/API
opt-in e prove ID-05 nel perimetro descritto. Non completa ID-04/06/07, UI o beta.
Aggiornamento successivo: [S1-004D](account-mfa-baseline.md) implementa TOTP/enrollment,
recovery monouso, lock/replay, materiale cifrato e revoca. L'obbligo Owner/Admin
richiede ancora l'integrazione con TenantContext/RBAC: ID-04 non autorizza la beta.
Approvazione esplicita dell'utente: "ok parametri tecnici S1-001 approvati".
Aggiornamento S1-005: [TenantContext interno](tenant-context-baseline.md) verifica
membership/ruolo e richiede prova MFA per emettere contesto Owner/Admin. Endpoint
workspace e RBAC dei comandi sono ancora da integrare; non completa ID-06/07 o beta.
NIS2/GDPR sono riferimenti di progetto, non una dichiarazione di conformità legale.

Aggiornamento 21 ottobre: [S1-007](tenant-isolation-baseline.md) e
[S1-008](workspace-api-baseline.md) implementano isolamento ORM e RBAC API interno,
membership/ruolo riletti, MFA Owner/Admin, selezione protetta e test HTTP reali.
Non completa governance/audit segregato, email reale, UI o autorizzazione beta.
La review normativa/organizzativa e le prove di efficacia restano da svolgere.

## Ambito e significato di compliance

Identità locale ASP.NET Core Identity per RAMAI SaaS multi-azienda, con cookie web
first-party, utenti personali e membership per tenant. Nessun CIAM esterno obbligatorio.
NIS2/GDPR non impongono di acquistare Entra e non sono una configurazione predefinita
del framework. Controlli tecnici, procedure organizzative ed evidenze devono essere
valutati insieme. Non pubblicare claim "NIS2/GDPR compliant" sulla sola base del codice.

GDPR: definire trattamenti, ruoli, basi giuridiche, informativa, diritti, retention,
fornitori e misure appropriate; articoli 5, 6, 13, 25, 28 e 32 sono riferimenti,
non una checklist esaustiva [L1]. Il ruolo RAMAI può differire tra account/security
e dati business trattati per i clienti: non attribuirne uno unico automaticamente.

NIS2: valutare applicabilità alla persona giuridica/servizio, settore, dimensioni,
eventuali fattispecie specifiche e recepimento italiano, non il solo numero MAU.
La direttiva richiede misure proporzionate al rischio e contempla accessi, supply
chain, continuità e autenticazione multifattore ove appropriato [L2, L3]. Anche i
requisiti contrattuali dei clienti vanno valutati separatamente dall'assoggettamento.

Serve review con referente security/privacy e consulente competente prima della
beta esterna: non si determina qui una classificazione NIS2 né una certificazione.

## Parametri tecnici approvati per la prima implementazione

I valori seguenti sono decisioni progettuali RAMAI approvate, da implementare/testare; **non sono
numeri prescritti da NIS2/GDPR**. NIST è usato come riferimento tecnico distinto [T1].

### ID-01 — Password e credenziali

- Minimo 15 caratteri; massimo applicativo 128 (supporto quindi superiore a 64),
  passphrase/spazi/password manager e incolla consentiti. Unicode e conteggio della
  lunghezza devono avere comportamento esplicito e testato, senza troncamenti silenziosi.
- Nessun obbligo arbitrario di maiuscole/simboli o cambio periodico a calendario;
  reset forzato in caso di compromissione. Non usare domande di sicurezza.
- Blocco delle password comuni/attese/compromesse. Per il primo ambiente interno
  valutare lista locale versionata/licenziata e aggiornabile; una piccola lista non
  equivale a un controllo completo. Non spedire password a servizi esterni o nei log.
- Hash/verify con PasswordHasher standard Identity; algoritmo, costo e rehash da
  verificare sulla versione .NET effettiva e con benchmark, non parametri copiati
  da un tutorial. Niente hash reversibili o password in chiaro persistite.

Acceptance: validator server-side più test limiti, passphrase, password bloccata,
hash non uguale al segreto e verifica/rehash; nessun leak di password/hash nei log.

### ID-02 — Brute force e account enumeration

Baseline approvata: lockout temporaneo dopo 5 fallimenti consecutivi per 15 minuti,
da validare contro denial-of-service sugli account; rate limit aggiuntivo per endpoint,
origine e account. Il solo limite per IP non basta e non deve bloccare intere aziende
dietro NAT. Le soglie della rate policy vanno misurate e definite nel task, senza
nuova infrastruttura implicita. Risposte equivalenti per account assente/esistente;
valutare differenze di timing. Nessun blocco permanente automatico.

Acceptance: test progressione/sblocco, IP distribuiti e account multipli, risposte
generiche e messaggi di retry; log security sintetici senza credenziali.

### ID-03 — Email verificata e recupero account

Conferma obbligatoria prima di accesso tenant-scoped. Token standard purpose-bound;
durata conferma 24 ore e reset 30 minuti. Dimostrare scadenza e invalidazione
reset dopo uso/modifica credenziali; nuova emissione e concorrenza testate. Risposte
generiche per richiesta recupero, rate limit invii, URL di ritorno controllate.
Cambio email richiede verifica del nuovo indirizzo e riautenticazione appropriata.

Test: sender in memoria con account sintetici, mai token stampati o API pubbliche
che li restituiscono. Prima di utenti reali: canale email e recupero affidabili,
fornitore/secret/retention valutati e approvati; non auto-confermare per aggirarlo.
Token non in analytics, referrer o log delle URL dei link.

### ID-04 — MFA e recupero MFA

Baseline approvata: TOTP con app autenticatore per Owner/Admin prima di beta esterna; consentire
MFA anche ai Member e definire eventuale obbligo tenant/risk-based. Conferma email
non sostituisce MFA. Privilegi elevati solo dopo enrollment soddisfatto negli ambienti
che lo richiedono. Recovery code e secret MFA da proteggere; niente log o supporto
che disabilita MFA senza verifica. TOTP non viene dichiarato phishing-resistant.
Passkey/metodi phishing-resistant sono evoluzione da valutare, non implementati qui.

Acceptance: enrollment, replay/scadenza codici, tentativi limitati, recovery e revoca
testati; nessun downgrade silenzioso quando MFA non è disponibile. Non dichiarare
la futura beta autorizzata finché mancano implementazione e review di questo controllo.

### ID-05 — Sessioni, CSRF e revoca

HTTPS, cookie host-only HttpOnly/Secure e SameSite appropriato; nessun bearer token
in localStorage. Antiforgery su mutazioni/logout, return URL locali. Timeout approvati:
30 minuti idle e 12 ore assolute; SecurityStamp e ticket store da integrare/testare,
non revoca immediata assunta dal default. Per offboarding/reset/cambio ruolo, nessuna
nuova richiesta protetta accettata con la sessione revocata dopo commit della revoca;
definire anche trattamento delle operazioni già in corso.
DB/session store non disponibili: fail-closed. Keyring persistente protetto e recovery
verificati prima di dichiarare funzionante un deploy multi-istanza.

### ID-06 — Ruoli e isolamento aziende

User globale, ruolo per Membership e tenant attivo autorizzato dal backend. Owner di
A non è Owner di B. No join da dominio email, mass assignment TenantId/Role o fiducia
in header/client. Filtri read e protezioni write con test reali tenant A/B, ID validi
di altro tenant, cambio workspace e richieste concorrenti. Guard UI non è authz.

### ID-07 — Audit e minimizzazione

Separare log applicativi generici e audit security con accesso ristretto. Eventi:
login/logout/fallimenti, reset richiesto/eseguito senza token, MFA, revoca, cambi
permessi e accessi negati. Campi minimi: data UTC, tipo/esito, UserId pseudonimo,
TenantId se pertinente e correlation ID; IP solo se motivato/minimizzato.
Gli identificativi pseudonimi restano dati personali, non diventano anonimi.

Nessun body business, password/hash, cookie, token o secret. Definire durata,
cancellazione/rotazione e accesso audit prima di dati reali; nessun periodo è qui
inventato come "obbligo GDPR/NIS2". Testare redazione, segregazione e retention.

## Privacy e organizzazione: gate separati dal login

| Tema | Output da predisporre prima di utenti/dati reali | Stato |
| --- | --- | --- |
| Accountability e ruoli | Data map account/security/business, responsabili e registro decisioni | Da definire |
| Informativa e base giuridica | Valutazione per finalità; non usare consenso indistinto per tutto | Da validare |
| Minimizzazione e retention | Campi necessari, tempi motivati e cancellazione anche backup/log | Da definire |
| Diritti | Accesso/rettifica/export/cancellazione con verifica identità e limiti motivati | Da progettare |
| Fornitori | Hosting/email/subprocessor, accordi e trasferimenti/residency verificati | Da valutare |
| Incidenti e continuità | Owner, escalation, revoca, backup e restore provato | Da progettare/testare |
| NIS2 | Applicabilità, eventuali obblighi/notifiche e misure ACN pertinenti | Review esterna necessaria |

Le responsabilità e notifiche GDPR/NIS2 hanno presupposti e destinatari distinti:
il runbook dovrà mapparli correttamente, non usare una singola scadenza generica.
Nessun DPO/DPIA obbligatorio viene assunto o escluso senza valutazione dei presupposti.
La cancellazione di un utente globale non cancella automaticamente dati delle altre
aziende: definire ownership, disattivazione membership e retention prima del codice relativo.

## Tracciabilità e verifica

S1-001: approvazione policy/threat model e questioni aperte. S1-002/003: ownership dati,
membership, schema e migrazioni. S1-004: ID-01–05, includendo test email/reset e lifecycle;
se troppo ampio dividerlo in task piccoli, non saltare le protezioni. S1-005/007/008:
ID-06 e ID-07. S1-010/011: negative tests e CI/security scans. S1-012: review e prove.

I controlli erano interamente pianificati alla data di approvazione; lo stato parziale
S1-004A è riportato sopra, senza dichiarare completato il lifecycle. MFA e gate privacy prima della
beta non sono implicitamente rinviati a dopo la vendita. Questa baseline non amplia
SEC-EX-001 a vulnerabilità runtime, auth o isolamento tenant e non rinnova la deroga.
S1-001 è completato nel perimetro documentale di analisi/approvazione tecnica.
Le soglie da tarare, i dettagli dei componenti e le prove richieste saranno lavorati
nei singoli task, prima di dichiararne il controllo implementato o autorizzare la beta.

## Registro approvazione e limiti

| Data | Decisione | Evidenza |
| --- | --- | --- |
| 2026-10-16 | Baseline proposta; nessuna implementazione | Analisi ADR-009/S1-001 |
| 2026-10-17 | Approvazione parametri tecnici S1-001 | Utente: "ok parametri tecnici S1-001 approvati" |

Questa approvazione non acquista servizi, non certifica GDPR/NIS2 e non autorizza
in blocco task applicativi, migrazioni, commit/push o commercializzazione.

## Fonti ufficiali consultate

- L1: [GDPR, Regolamento UE 2016/679](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32016R0679), artt. 5/6/13/25/28/32.
- L2: [Direttiva NIS2, UE 2022/2555](https://eur-lex.europa.eu/legal-content/EN-FR/TXT/?uri=CELEX:32022L2555), artt. 2/20/21/23.
- L3: [D.lgs. 138/2024, Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2024/10/01/24G00155/SG).
- T1: [NIST SP 800-63B-4, password e autenticazione](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/), riferimento tecnico, non legge UE.

Data verifica: 16 ottobre 2026. Consultati testi EUR-Lex indicizzati/versioni disponibili;
gli endpoint italiani hanno restituito anti-bot e il portale ACN 403. Non si dichiara
verificata l'ultima determinazione ACN o una mappatura completa alle sue misure.
Questa lacuna deve essere chiusa prima di attestare obblighi/applicabilità specifici.
