import { test as base, chromium, type Browser } from "@playwright/test";
import { readFileSync } from "node:fs";
import { createHmac } from "node:crypto";
type Account = { email: string; password: string; sharedKey: string | null };
export type State = { uiOrigin: string; pin: string; workspaces: { a: string; b: string; c: string }; accounts: Record<string, Account> };
export function state(): State {
  const path = process.env.RAMAI_UI_TEST_STATE;
  if (!path) throw new Error("Isolated UI state missing");
  return JSON.parse(readFileSync(path, "utf8")) as State;
}
// Independent RFC6238 client for synthetic tests only; production MFA stays ASP.NET Identity.
export function totp(secret: string) {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; let bits = "";
  for (const value of secret) bits += alphabet.indexOf(value).toString(2).padStart(5, "0");
  const key = Buffer.from(Array.from({ length: Math.floor(bits.length / 8) }, (_, index) => parseInt(bits.slice(index * 8, index * 8 + 8), 2)));
  const counter = Buffer.alloc(8); counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000)));
  const hash = createHmac("sha1", key).update(counter).digest(); const offset = hash[19] & 15;
  return String((hash.readUInt32BE(offset) & 0x7fffffff) % 1_000_000).padStart(6, "0");
}
export const test = base.extend<object, { browser: Browser }>({
  browser: [async ({}, provide) => {
    // Pin only the freshly generated fixture certificate; never ignore all HTTPS errors.
    const browser = await chromium.launch({ args: [`--ignore-certificate-errors-spki-list=${state().pin}`] });
    await provide(browser); await browser.close();
  }, { scope: "worker" }],
  baseURL: async ({}, provide) => { await provide(state().uiOrigin); },
});
export { expect } from "@playwright/test";
