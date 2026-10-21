import { spawn, type ChildProcess } from "node:child_process";
import { mkdtemp, readFile, writeFile, rm, chmod } from "node:fs/promises";
import { resolve, dirname, basename, join } from "node:path";
import { tmpdir } from "node:os";
import { createServer } from "node:net";
import { get } from "node:https";
import { X509Certificate, createHash } from "node:crypto";

type Fixture = { configuration: Record<string, string>; certificate: string; privateKey: string; pfx: string };
const webRoot = resolve(__dirname, "../..");
const repo = resolve(webRoot, "../../..");
function port(): Promise<number> { return new Promise((done, reject) => {
  const server = createServer(); server.on("error", reject); server.listen(0, "127.0.0.1", () => {
    const address = server.address(); if (!address || typeof address === "string") return reject(new Error("No test port"));
    const result = address.port; server.close(error => error ? reject(error) : done(result));
  });
}); }
function run(command: string, args: string[], env: NodeJS.ProcessEnv, cwd = repo): Promise<void> {
  return new Promise((done, reject) => {
    const child = spawn(command, args, { cwd, env, windowsHide: true, stdio: "inherit" });
    child.on("error", reject); child.on("exit", code => code === 0 ? done() : reject(new Error("UI test preparation command failed")));
  });
}
async function ready(origin: string, ca: string) {
  for (let index = 0; index < 120; index++) {
    const status = await new Promise<number>(done => {
      const request = get(origin, { ca, rejectUnauthorized: true, timeout: 1000 }, response => { response.resume(); done(response.statusCode ?? 0); });
      request.on("error", () => done(0)); request.on("timeout", () => { request.destroy(); done(0); });
    });
    if (status >= 200 && status < 400) return;
    await new Promise(done => setTimeout(done, 250));
  }
  throw new Error("Isolated HTTPS test server did not become ready");
}
async function stop(child: ChildProcess | undefined) {
  if (!child || child.exitCode !== null) return;
  child.kill();
  await Promise.race([new Promise(done => child.once("exit", done)), new Promise(done => setTimeout(done, 3000))]);
  if (child.exitCode === null) child.kill("SIGKILL");
}
export default async function setup() {
  if (!process.env.RAMAI_DB_PASSWORD) throw new Error("Supply isolated PostgreSQL/Redis test environment before browser tests");
  const root = await mkdtemp(join(tmpdir(), "ramai-ui-"));
  if (process.platform !== "win32") await chmod(root, 0o700);
  const apiPort = await port(); const uiPort = await port();
  const apiOrigin = `https://127.0.0.1:${apiPort}`; const uiOrigin = `https://127.0.0.1:${uiPort}`;
  const env = { ...process.env, RAMAI_UI_TESTS: "1", RAMAI_UI_TEST_ROOT: root, RAMAI_UI_ORIGIN: uiOrigin };
  const helperProject = "tests/Ramai.UiTestSetup/Ramai.UiTestSetup.csproj";
  const helper = join(repo, "tests/Ramai.UiTestSetup/bin/Release/net10.0/Ramai.UiTestSetup.dll");
  let api: ChildProcess | undefined; let ui: ChildProcess | undefined;
  let built = false;
  const cleanup = async () => {
    await stop(ui); await stop(api);
    // Keep the private manifest if cleanup fails, so owned resources remain identifiable.
    // Never erase the only recovery information while a database/namespace may still exist.
    if (built) await run("dotnet", [helper, "cleanup"], env);
    const target = resolve(root);
    if (dirname(target) !== resolve(tmpdir()) || !basename(target).startsWith("ramai-ui-")) throw new Error("Unsafe UI fixture cleanup path");
    await rm(target, { recursive: true, force: true });
  };
  try {
    await run("dotnet", ["build", helperProject, "-c", "Release", "-m:1", "--verbosity", "quiet"], env); built = true;
    await run("dotnet", [helper, "prepare"], env);
    const statePath = join(root, "fixture.json");
    const state = JSON.parse(await readFile(statePath, "utf8")) as Fixture;
    const ca = await readFile(state.certificate, "utf8");
    const pin = createHash("sha256").update(new X509Certificate(ca).publicKey.export({ type: "spki", format: "der" })).digest("base64");
    await writeFile(statePath, JSON.stringify({ ...state, apiOrigin, uiOrigin, pin }), { mode: 0o600 });
    process.env.RAMAI_UI_TEST_STATE = statePath;
    const apiEnv = { ...env, ...state.configuration, ASPNETCORE_URLS: apiOrigin,
      ASPNETCORE_Kestrel__Certificates__Default__Path: state.pfx,
      ASPNETCORE_Kestrel__Certificates__Default__Password: state.configuration.RAMAI_AUTH_CERTIFICATE_PASSWORD };
    api = spawn("dotnet", [join(repo, "src/backend/Ramai.Api/bin/Release/net10.0/Ramai.Api.dll")], { cwd: repo, env: apiEnv, windowsHide: true, stdio: "ignore" });
    const uiEnv = { ...env, RAMAI_API_ORIGIN: apiOrigin, NODE_EXTRA_CA_CERTS: state.certificate,
      RAMAI_UI_PORT: String(uiPort), RAMAI_UI_CERT: state.certificate, RAMAI_UI_KEY: state.privateKey };
    await run(process.execPath, ["node_modules/next/dist/bin/next", "build"], uiEnv, webRoot);
    ui = spawn(process.execPath, ["tests/e2e/https-server.mjs"], { cwd: webRoot, env: uiEnv, windowsHide: true, stdio: "ignore" });
    await ready(apiOrigin, ca); await ready(uiOrigin + "/login", ca);
    return cleanup;
  } catch (error) { await cleanup(); throw error; }
}
