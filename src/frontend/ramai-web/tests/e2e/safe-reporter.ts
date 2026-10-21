import type { Reporter, TestCase, TestResult, FullResult } from "@playwright/test/reporter";
// Credential-bearing browser call logs/DOM snapshots must not become CI output or artifacts.
// Failures remain failures and preserve the runner exit code; titles/locations contain no test input.
export default class SafeReporter implements Reporter {
  onTestEnd(test: TestCase, result: TestResult) {
    console.log(`${result.status}: ${test.title}`);
    if (result.status !== "passed") {
      console.log(`Failure location: ${test.location.file}:${test.location.line}`);
      for (const error of result.errors) {
        if (error.message?.includes("strict mode violation")) console.log("Failure class: ambiguous locator");
        else if (error.message?.includes("Timed out")) console.log("Failure class: expectation timeout");
        const location = error.stack?.match(/workspace\.spec\.ts:\d+:\d+/)?.[0];
        if (location) console.log(`Failed operation: ${location}`);
      }
    }
  }
  onEnd(result: FullResult) { console.log(`Protected UI suite: ${result.status}`); }
}
