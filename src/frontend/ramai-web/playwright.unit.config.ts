import { defineConfig } from "@playwright/test";
export default defineConfig({ testDir: "./tests/unit", workers: 1, retries: 0,
  outputDir: "../../../artifacts/ui-unit", reporter: "list", use: { trace: "off", screenshot: "off", video: "off" } });
