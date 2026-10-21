# S1-010 — Piano regressione security

21 ottobre 2026. Autorizzazione utente: pubblicare/verificare S1-009, poi S1-010.
S1-009 pubblicato `bbe24f4cb1ca316366623cdf04059959303a20f4`;
[CI reale](https://github.com/maffeinet/ramai/actions/runs/39580728584) success,
backend e frontend GitHub-hosted Ubuntu 24.04, anche browser HTTPS reale.
Nessun verde storico attribuito al nuovo codice S1-010.

## Perimetro e file annunciati

- `tests/Ramai.IntegrationTests/WorkspaceHttpTests.cs`: renderlo partial per
  riusare lo scenario reale senza duplicare le fixture; assert anti-leak sicuri.
- `WorkspaceSecurityRegressionTests.cs`: matrice post-revoca su endpoint protetti,
  isolamento fra due account/tenant reali anche concorrenti, CSRF tra soggetti,
  idempotenza per creator, scadenza e leak di body/cookie/key/credenziali nei log.
- `SensitiveAssertions.cs` e test: i fallimenti non devono stampare canary o log.
- `WebSessionTests.cs` e `MfaTests.cs`: sostituire i soli assert sensibili che
  potrebbero riversare credenziali nei messaggi del runner se fallissero.
- `tests/Ramai.UiTestSetup/Program.cs`, fixture browser e nuovo
  `tests/e2e/security-regression.spec.ts`: account/tenant sintetico dedicato,
  rendering inerte di input ostile, controlli backend e logout fra schede.
- `docs/security-regression-baseline.md`: matrice minacce/endpoint/evidenze/gap;
  report Sprint 1, memory/handoff/backlog e note S1-009 aggiornati.

Nessuna nuova dipendenza, servizio, endpoint, migration o cambio confini. Test e
documentazione soltanto, salvo bug concreto: in quel caso annunciare la correzione
minima. Non S1-011/012, AI, email operativa, UI business, deploy o beta.

## Strategia e limiti

Identity/cookie/MFA/PG/Redis reali, fixture temporanee separate da dati utente.
TestServer per matrice HTTP con clock controllato; Kestrel/browser HTTPS già
esistenti rieseguiti, nuovi test browser senza auth mock. API guard UI non sostituito.
16/3 test client/rendering esistenti rieseguiti; review dei percorsi ORM A/B,
ruoli, CSRF, replay, timeout e dipendenze fail-closed mediante matrice tracciabile.
Build/format, suite senza skipped, npm ci/lint/tipi/build/E2E, audit e Gitleaks.
Nessuna regola disattivata, secret nei log, fix --force o nuova eccezione debt.

L'autorizzazione commit/push della prima fase riguarda S1-009. Le modifiche S1-010
restano locali per review umana prima di merge; CI S1-010 e approvazione umana
non si considerano soddisfatte dall'esecuzione locale o dalla CI S1-009.
SEC-EX-001 non prorogata. Nessuna attestazione GDPR/NIS2 o penetration test esterno.

Autorizzazione successiva del 21 ottobre: creare branch, commit/push e PR delle
sole modifiche S1-010, verificare la CI e preparare riepilogo per review umana.
Supera il precedente limite di pubblicazione; **non autorizza merge automatico**,
chiusura della review umana o avvio S1-011. Evidenze effettive nella baseline.
