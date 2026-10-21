import { defineConfig } from "@playwright/test";
export default defineConfig({ testDir: "./tests/e2e", testMatch: "*.spec.ts", globalSetup: "./tests/e2e/global-setup.ts",
  workers: 1, retries: 0, timeout: 60_000, expect: { timeout: 10_000 },
  outputDir: "../../../artifacts/ui-e2e", preserveOutput: "never", reporter: "./tests/e2e/safe-reporter.ts",
  use: { trace: "off", screenshot: "off", video: "off" } });
