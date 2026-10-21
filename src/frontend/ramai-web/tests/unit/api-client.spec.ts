import { test, expect } from "@playwright/test";
import { createApiClient, ApiError } from "../../lib/api-client";
const id = "cc670693-ed18-4c1e-8cf5-21cb6015a9ee";
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
test("all requests remain same-origin and CSRF exists only for the mutation", async () => {
  const calls: { url: string; options: RequestInit }[] = [];
  const transport = (async (url, options) => {
    calls.push({ url: String(url), options: options! });
    return String(url).endsWith("antiforgery") ? json({ requestToken: "synthetic-csrf" }) : new Response(null, { status: 204 });
  }) as typeof fetch;
  await createApiClient(transport).select(id);
  expect(calls.map(call => call.url)).toEqual(["/api/v1/account/antiforgery", "/api/v1/workspaces/select"]);
  for (const call of calls) { expect(call.options.credentials).toBe("same-origin"); expect(call.options.cache).toBe("no-store"); expect(call.options.redirect).toBe("error"); }
  expect(calls[0].options.headers).not.toHaveProperty("X-CSRF-TOKEN");
  expect(calls[1].options.headers).toHaveProperty("X-CSRF-TOKEN", "synthetic-csrf");
  expect(JSON.parse(calls[1].options.body as string)).toEqual({ workspaceId: id });
});
for (const status of [400, 401, 403, 409, 429, 503]) test(`normalizes status ${status} without server exception text`, async () => {
  const transport = (async () => new Response("sensitive-server-exception", { status, headers: { "X-Correlation-ID": "safe-reference" } })) as typeof fetch;
  await expect(createApiClient(transport).profile()).rejects.toMatchObject({ status, correlationId: "safe-reference", message: "Richiesta non completata" });
});
test("network errors do not retain arbitrary exception messages", async () => {
  const transport = (async () => { throw new Error("sensitive-network-detail"); }) as typeof fetch;
  await expect(createApiClient(transport).profile()).rejects.toMatchObject({ status: 0, message: "Richiesta non completata" });
});
for (const payload of [{ userId: id, emailConfirmed: false, mfaVerified: true }, { userId: "untrusted", emailConfirmed: true, mfaVerified: true }])
  test(`rejects invalid profile ${JSON.stringify(payload)}`, async () => { await expect(createApiClient((async () => json(payload)) as typeof fetch).profile()).rejects.toBeInstanceOf(ApiError); });
test("rejects unknown workspace role", async () => {
  await expect(createApiClient((async () => json({ workspaceId: id, name: "Synthetic", role: "SuperAdmin" })) as typeof fetch).workspace()).rejects.toMatchObject({ status: 502 });
});
test("rejects a role array rather than coercing it to Owner", async () => {
  await expect(createApiClient((async () => json({ workspaceId: id, name: "Synthetic", role: ["Owner"] })) as typeof fetch).workspace()).rejects.toMatchObject({ status: 502 });
});
test("rejects invalid JSON without returning response contents", async () => {
  await expect(createApiClient((async () => new Response("sensitive-invalid-json")) as typeof fetch).profile()).rejects.toMatchObject({ status: 502, message: "Richiesta non completata" });
});
test("does not render an unsafe correlation header", async () => {
  await expect(createApiClient((async () => new Response(null, { status: 503, headers: { "X-Correlation-ID": "<unsafe>" } })) as typeof fetch).profile()).rejects.toMatchObject({ status: 503, correlationId: undefined });
});
test("cancellation propagates without becoming a false network failure", async () => {
  const controller = new AbortController(); controller.abort();
  const failure = new Error("cancelled");
  await expect(createApiClient((async () => { throw failure; }) as typeof fetch).profile(controller.signal)).rejects.toBe(failure);
});
test("does not proceed when CSRF cannot be obtained", async () => {
  let calls = 0;
  const client = createApiClient((async () => { calls++; return json({ requestToken: null }); }) as typeof fetch);
  await expect(client.logout()).rejects.toMatchObject({ status: 502 }); expect(calls).toBe(1);
});
