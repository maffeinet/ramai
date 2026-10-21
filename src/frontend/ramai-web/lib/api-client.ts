export type Profile = { userId: string; emailConfirmed: boolean; mfaVerified: boolean };
export type Workspace = { workspaceId: string; name: string; role: "Owner" | "Admin" | "Member" };
export type MfaInput = { password: string; code?: string; recoveryCode?: string };
export class ApiError extends Error {
  constructor(public readonly status: number, public readonly correlationId?: string) {
    super("Richiesta non completata");
    this.name = "ApiError";
  }
}
export const isUuid = (value: unknown): value is string => typeof value === "string"
  && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
  && value !== "00000000-0000-0000-0000-000000000000";
const object = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);
const profile = (value: unknown): value is Profile => object(value) && isUuid(value.userId)
  && value.emailConfirmed === true && typeof value.mfaVerified === "boolean";
const workspace = (value: unknown): value is Workspace => object(value) && isUuid(value.workspaceId)
  && typeof value.name === "string" && value.name.length > 0 && value.name.length <= 256
  && typeof value.role === "string" && ["Owner", "Admin", "Member"].includes(value.role);

// No browser storage, cookie parsing, token cache, logging or configurable client-side origin.
export function createApiClient(transport: typeof fetch = (...args) => fetch(...args)) {
  async function send(path: string, method = "GET", body?: object, signal?: AbortSignal): Promise<Response> {
    let csrf: string | undefined;
    if (method !== "GET") {
      const token = await read("/account/antiforgery", (value): value is { requestToken: string } =>
        object(value) && typeof value.requestToken === "string" && value.requestToken.length > 0, signal);
      csrf = token.requestToken;
    }
    let response: Response;
    try {
      response = await transport("/api/v1" + path, {
        method, credentials: "same-origin", mode: "same-origin", cache: "no-store", redirect: "error", signal,
        headers: { Accept: "application/json", ...(body ? { "Content-Type": "application/json" } : {}),
          ...(csrf ? { "X-CSRF-TOKEN": csrf } : {}) },
        ...(body ? { body: JSON.stringify(body) } : {}),
      });
    } catch (error) {
      if (signal?.aborted) { throw error; }
      throw new ApiError(0);
    }
    if (!response.ok) {
      const candidate = response.headers.get("X-Correlation-ID");
      const correlation = candidate && /^[a-zA-Z0-9_.:-]{1,128}$/.test(candidate) ? candidate : undefined;
      // Never read/render arbitrary server error text, headers, credentials or exception details.
      throw new ApiError(response.status, correlation);
    }
    return response;
  }
  async function parse<T>(response: Response, validate: (value: unknown) => value is T): Promise<T> {
    let value: unknown;
    try { value = await response.json(); } catch { throw new ApiError(502); }
    if (!validate(value)) { throw new ApiError(502); }
    return value;
  }
  async function read<T>(path: string, validate: (value: unknown) => value is T, signal?: AbortSignal) {
    return parse(await send(path, "GET", undefined, signal), validate);
  }
  async function empty(path: string, body: object, signal?: AbortSignal) {
    if ((await send(path, "POST", body, signal)).status !== 204) { throw new ApiError(502); }
  }
  return {
    profile: (signal?: AbortSignal) => read("/profile", profile, signal),
    workspace: (signal?: AbortSignal) => read("/workspace", workspace, signal),
    login: (input: { email: string; password: string; code?: string; recoveryCode?: string }, signal?: AbortSignal) => empty("/account/login", input, signal),
    logout: (signal?: AbortSignal) => empty("/account/logout", {}, signal),
    select: (workspaceId: string, signal?: AbortSignal) => empty("/workspaces/select", { workspaceId }, signal),
    async create(name: string, requestId: string, signal?: AbortSignal) {
      return parse(await send("/workspaces", "POST", { name, requestId }, signal),
        (value): value is { workspaceId: string } => object(value) && isUuid(value.workspaceId));
    },
    async rename(name: string, signal?: AbortSignal) {
      return parse(await send("/workspace/settings", "PUT", { name }, signal), workspace);
    },
    async enroll(input: MfaInput, signal?: AbortSignal) {
      return parse(await send("/account/mfa/enrollment", "POST", input, signal),
        (value): value is { sharedKey: string | null; requiresSignIn: boolean; expiresInSeconds: number } => object(value)
          && (value.sharedKey === null || (typeof value.sharedKey === "string" && /^[A-Z2-7]{16,128}$/.test(value.sharedKey)))
          && typeof value.requiresSignIn === "boolean" && value.expiresInSeconds === 600);
    },
    async confirm(input: MfaInput, signal?: AbortSignal) {
      return parse(await send("/account/mfa/confirm", "POST", input, signal),
        (value): value is { recoveryCodes: string[] } => object(value) && Array.isArray(value.recoveryCodes)
          && value.recoveryCodes.length === 10 && value.recoveryCodes.every(code => typeof code === "string" && /^[a-zA-Z0-9-]{8,64}$/.test(code)));
    },
  };
}
export const api = createApiClient();
