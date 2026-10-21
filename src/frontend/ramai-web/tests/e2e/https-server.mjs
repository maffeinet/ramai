// HTTPS wrapper for the isolated production-build browser tests only, not a deployment choice.
import { createServer } from "node:https";
import { readFileSync } from "node:fs";
import next from "next";
const port = Number(process.env.RAMAI_UI_PORT);
const app = next({ dev: false, hostname: "127.0.0.1", port });
await app.prepare();
createServer({ cert: readFileSync(process.env.RAMAI_UI_CERT), key: readFileSync(process.env.RAMAI_UI_KEY) }, app.getRequestHandler())
  .listen(port, "127.0.0.1");
