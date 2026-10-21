import type { Page, Locator } from "@playwright/test";
import { test, expect, state, totp } from "./fixtures";

async function secret(field: Locator, value: string) {
  try { await field.fill(value); } catch { throw new Error("Sensitive input failed; values withheld."); }
}
async function signIn(page: Page, label: string) {
  const account = state().accounts[label];
  await page.goto("/login"); await page.getByLabel("Email", { exact: true }).fill(account.email);
  await secret(page.getByLabel("Password", { exact: true }), account.password);
  if (account.sharedKey) await secret(page.getByLabel("Codice autenticatore (se MFA attiva)"), totp(account.sharedKey));
  await page.getByRole("button", { name: "Accedi", exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await expect(page.getByRole("heading", { name: "Area di lavoro", exact: true })).toBeVisible();
}
async function open(page: Page, id: string) {
  await page.getByLabel("Codice workspace", { exact: true }).fill(id);
  await page.getByRole("button", { name: "Apri workspace", exact: true }).click();
}

test("stored hostile workspace name remains inert text and cannot grant another tenant", async ({ page }) => {
  await signIn(page, "security"); await open(page, state().workspaces.c);
  await expect(page.getByRole("heading", { name: "Security Demo", exact: true })).toBeVisible();
  const hostile = '<img src=x onerror="window.__ramaiXss=true">';
  await page.getByLabel("Nome workspace", { exact: true }).fill(hostile);
  await page.getByRole("button", { name: "Salva nome" }).click();
  await expect(page.getByRole("heading", { name: hostile, exact: true })).toBeVisible();
  await expect(page.locator('img[src="x"]')).toHaveCount(0);
  expect(await page.evaluate(() => "__ramaiXss" in window)).toBe(false);
  const result = await page.evaluate(async foreign => {
    const response = await fetch(`/api/v1/workspace?tenantId=${foreign}`, { headers: { "X-Tenant-ID": foreign }, cache: "no-store" });
    return { status: response.status, workspaceId: (await response.json()).workspaceId, cache: response.headers.get("cache-control") };
  }, state().workspaces.a);
  expect(result.status).toBe(200);
  expect(result.workspaceId).toBe(state().workspaces.c);
  expect(result.cache?.split(",").map(value => value.trim().toLowerCase())).toContain("no-store");
  await open(page, state().workspaces.a);
  await expect(page.getByRole("alert").filter({ hasText: "Non hai i permessi" })).toBeVisible();
  await expect(page.getByRole("heading", { name: hostile, exact: true })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: /^Alfa Demo/ })).toHaveCount(0);
  await page.getByRole("button", { name: "Esci", exact: true }).click(); await expect(page).toHaveURL(/\/login$/);
});

test("logout in one browser tab denies the next protected action in another tab", async ({ page, context }) => {
  await signIn(page, "member"); await open(page, state().workspaces.a);
  await expect(page.getByRole("heading", { name: /^Alfa Demo(?: aggiornata)?$/ })).toBeVisible();
  const second = await context.newPage(); await second.goto("/workspace");
  await expect(second.getByRole("heading", { name: /^Alfa Demo(?: aggiornata)?$/ })).toBeVisible();
  await page.getByRole("button", { name: "Esci", exact: true }).click(); await expect(page).toHaveURL(/\/login$/);
  await open(second, state().workspaces.a); await expect(second).toHaveURL(/\/login$/);
  await expect(second.getByRole("heading", { name: /^Alfa Demo(?: aggiornata)?$/ })).toHaveCount(0);
  expect(await second.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
  await second.close();
});
