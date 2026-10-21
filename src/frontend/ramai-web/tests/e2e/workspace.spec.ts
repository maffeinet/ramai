import { mkdir } from "node:fs/promises";
import { resolve } from "node:path";
import type { Page, Locator } from "@playwright/test";
import { test, expect, state, totp } from "./fixtures";

async function secret(field: Locator, value: string) {
  try { await field.fill(value); } catch { throw new Error("Sensitive input action failed; credential details withheld."); }
}
async function login(page: Page, label: string) {
  const account = state().accounts[label];
  await page.goto("/login"); await page.getByLabel("Email", { exact: true }).fill(account.email);
  await secret(page.getByLabel("Password", { exact: true }), account.password);
  if (account.sharedKey) await secret(page.getByLabel("Codice autenticatore (se MFA attiva)"), totp(account.sharedKey));
  await page.getByRole("button", { name: "Accedi", exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await expect(page.getByRole("heading", { name: "Area di lavoro", exact: true })).toBeVisible();
}
async function select(page: Page, id: string) {
  await page.getByLabel("Codice workspace", { exact: true }).fill(id);
  await page.getByRole("button", { name: "Apri workspace", exact: true }).click();
}

test("anonymous protected route returns to login without showing workspace data", async ({ page }) => {
  await page.goto("/workspace?returnUrl=https://hostile.example.invalid");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Accedi ad Agentilio" })).toBeVisible();
  await expect(page.getByText("Alfa Demo", { exact: true })).toHaveCount(0);
});

test("member login opens only its authorized workspace and logout removes access", async ({ page, context }) => {
  await login(page, "member"); await select(page, state().workspaces.a);
  await expect(page.getByRole("heading", { name: "Alfa Demo", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Salva nome" })).toHaveCount(0);
  const cookie = (await context.cookies()).find(value => value.name === "__Host-Ramai.Session");
  expect(cookie?.secure).toBe(true); expect(cookie?.httpOnly).toBe(true); expect(cookie?.sameSite).toBe("Strict");
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length, cookie: document.cookie.includes("__Host-Ramai.Session") })))
    .toEqual({ local: 0, session: 0, cookie: false });
  await select(page, state().workspaces.b);
  await expect(page.getByRole("alert").filter({ hasText: "Non hai i permessi" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Alfa Demo", exact: true })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Beta Demo", exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Esci", exact: true }).click(); await expect(page).toHaveURL(/\/login$/);
  await page.goto("/workspace"); await expect(page).toHaveURL(/\/login$/);
});

test("MFA owner can edit A but is only Member in B; backend rejects bypass attempts", async ({ page }) => {
  await login(page, "privileged"); await select(page, state().workspaces.a);
  await expect(page.getByRole("button", { name: "Salva nome" })).toBeVisible();
  await page.getByLabel("Nome workspace", { exact: true }).fill("Alfa Demo aggiornata");
  await page.getByRole("button", { name: "Salva nome" }).click();
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toBeVisible();
  await select(page, state().workspaces.b); await expect(page.getByRole("heading", { name: "Beta Demo", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Salva nome" })).toHaveCount(0);
  const results = await page.evaluate(async () => {
    const token = await (await fetch("/api/v1/account/antiforgery", { cache: "no-store" })).json();
    const denied = await fetch("/api/v1/workspace/settings", { method: "PUT", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.requestToken }, body: JSON.stringify({ name: "Should not persist" }) });
    const csrf = await fetch("/api/v1/workspace/settings", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: "Should not persist" }) });
    return [denied.status, csrf.status];
  });
  expect(results).toEqual([403, 400]);
  await mkdir(resolve("../../../artifacts/ui-preview"), { recursive: true });
  await page.screenshot({ path: resolve("../../../artifacts/ui-preview/workspace.png"), fullPage: true });
  await page.getByRole("button", { name: "Esci", exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test("enrollment and recovery login allow explicit creation of the first workspace", async ({ page }) => {
  await login(page, "onboarding");
  await page.getByRole("link", { name: "Configura MFA prima di creare" }).click();
  await expect(page.getByRole("heading", { name: "Sicurezza account", exact: true })).toBeVisible();
  await secret(page.getByLabel("Password corrente", { exact: true }), state().accounts.onboarding.password);
  await page.getByRole("button", { name: "Prepara autenticatore" }).click();
  await expect(page.getByRole("heading", { name: "Conferma l’autenticatore" })).toBeVisible();
  const key = (await page.getByTestId("authenticator-key").textContent())!;
  await secret(page.getByLabel("Password corrente", { exact: true }), state().accounts.onboarding.password);
  await secret(page.getByLabel("Codice della nuova app", { exact: true }), totp(key));
  await page.getByRole("button", { name: "Conferma MFA" }).click();
  await expect(page.getByRole("heading", { name: "Conserva i codici di recupero" })).toBeVisible();
  const recovery = (await page.getByLabel("Codici di recupero", { exact: true }).locator("li").first().textContent())!;
  await page.getByRole("checkbox", { name: "Ho conservato i codici in un luogo sicuro" }).check();
  await page.getByRole("link", { name: "Torna al login", exact: true }).click();
  await page.getByLabel("Email", { exact: true }).fill(state().accounts.onboarding.email);
  await secret(page.getByLabel("Password", { exact: true }), state().accounts.onboarding.password);
  await page.getByRole("button", { name: "Usa un codice di recupero" }).click();
  await secret(page.getByLabel("Codice di recupero", { exact: true }), recovery);
  await page.getByRole("button", { name: "Accedi", exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await page.getByLabel("Nome nuova azienda").fill("Nuovo Workspace Demo");
  await page.getByRole("button", { name: "Crea e apri workspace" }).click();
  await expect(page.getByRole("heading", { name: "Nuovo Workspace Demo", exact: true })).toBeVisible();
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
});

test("network failure removes the previously displayed workspace and gives a safe error", async ({ page }) => {
  await login(page, "member"); await select(page, state().workspaces.a);
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toBeVisible();
  await page.route("**/api/v1/workspaces/select", route => route.abort());
  await select(page, state().workspaces.a);
  await expect(page.getByRole("alert").filter({ hasText: "Connessione non disponibile" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toHaveCount(0);
});

test("mobile workspace fits the viewport and revalidates a simulated bfcache restore", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, "member"); await select(page, state().workspaces.a);
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await mkdir(resolve("../../../artifacts/ui-preview"), { recursive: true });
  await page.screenshot({ path: resolve("../../../artifacts/ui-preview/mobile.png"), fullPage: true });
  await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent("pagehide", { persisted: true })));
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toHaveCount(0);
  await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent("pageshow", { persisted: true })));
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toBeVisible();
});

test("invalid credentials give a generic error without leaving the local login", async ({ page }) => {
  await page.goto("/login?returnUrl=https://hostile.example.invalid");
  await page.getByLabel("Email", { exact: true }).fill(state().accounts.outsider.email);
  await secret(page.getByLabel("Password", { exact: true }), "synthetic incorrect login input");
  await page.getByRole("button", { name: "Accedi", exact: true }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Accesso non riuscito" })).toBeVisible();
  expect(new URL(page.url()).origin).toBe(state().uiOrigin);
  await expect(page.getByLabel("Password", { exact: true })).toHaveValue("");
});

test("MFA enrollment key is removed on a simulated page-cache transition", async ({ page }) => {
  await login(page, "lifecycle");
  await page.getByRole("link", { name: "Configura MFA prima di creare" }).click();
  await secret(page.getByLabel("Password corrente", { exact: true }), state().accounts.lifecycle.password);
  await page.getByRole("button", { name: "Prepara autenticatore" }).click();
  await expect(page.getByTestId("authenticator-key")).toHaveCount(1);
  await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent("pagehide", { persisted: true })));
  await expect(page.getByTestId("authenticator-key")).toHaveCount(0);
  await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent("pageshow", { persisted: true })));
  await expect(page.getByRole("heading", { name: "Proteggi il tuo account" })).toBeVisible();
  await expect(page.getByTestId("authenticator-key")).toHaveCount(0);
  await expect(page.getByLabel("Password corrente", { exact: true })).toHaveValue("");
});

test("revoked session is cleared on the next protected action", async ({ page }) => {
  await login(page, "member"); await select(page, state().workspaces.a);
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toBeVisible();
  expect(await page.evaluate(async () => {
    const csrf = await (await fetch("/api/v1/account/antiforgery", { cache: "no-store" })).json();
    return (await fetch("/api/v1/account/revoke-all", { method: "POST", headers: { "X-CSRF-TOKEN": csrf.requestToken } })).status;
  })).toBe(204);
  await select(page, state().workspaces.a); await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Alfa Demo aggiornata", exact: true })).toHaveCount(0);
});
