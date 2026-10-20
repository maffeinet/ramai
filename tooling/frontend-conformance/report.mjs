import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const root = import.meta.dirname;
// Diagnostic artifacts only: an interrupted/failed run must not leave a previous green result.
const destination = path.resolve(root, '../../artifacts/frontend-conformance');
if (!destination.startsWith(path.resolve(root, '../..') + path.sep)) throw new Error('Unsafe artifact path');
fs.mkdirSync(destination, { recursive: true });
const startedAt = new Date().toISOString();
fs.writeFileSync(path.join(destination, 'results.json'), JSON.stringify({
  status: 'running', startedAt, readyForAdoption: false,
}, null, 2) + '\n');
let run;
try {
const { parseTestResult } = await import('./report-core.mjs');
const { inventory, handledByESLint, targetName } = await import('./support.mjs');
const files = fs.readdirSync(path.join(root, 'tests')).filter(x => x.endsWith('.test.mjs'))
  .sort().map(x => path.join(root, 'tests', x));
if (!files.length) throw new Error('No conformance test files');
run = spawnSync(process.execPath, ['--test', '--test-reporter=tap', ...files], {
  cwd: root, encoding: 'utf8', timeout: 120000, maxBuffer: 8 * 1024 * 1024,
});
fs.writeFileSync(path.join(destination, 'tests.tap'), run.stdout ?? '');
const counts = parseTestResult(run);
const summary = {
  status: 'passed', startedAt,
  ...counts,
  scope: 'Isolated prototype; not the active frontend tooling',
  readyForAdoption: false,
  environment: { platform: process.platform, arch: process.arch, node: process.version },
  limitations: ['No GitHub run',
    '86-rule ownership is not exhaustive semantic/fixer equivalence',
    'Vendored compatibility rules require explicit maintenance ownership'],
  profiles: Object.fromEntries(Object.entries(inventory.profiles).map(([name, profile]) => [name, {
    activeRules: Object.keys(profile.rules).length,
    owners: Object.fromEntries(Object.entries(profile.rules).map(([rule, options]) => [rule, {
      engine: handledByESLint(rule) ? 'eslint10' : 'oxlint',
      target: handledByESLint(rule) ? rule : targetName(rule), options,
    }])),
  }])),
};
// Generated diagnostic output is ignored by Git; no snapshot or installed source is rewritten.
fs.writeFileSync(path.join(destination, 'results.json'), JSON.stringify(summary, null, 2) + '\n');
console.log(JSON.stringify({ tests: summary.tests, passed: summary.passed, failed: summary.failed,
  skipped: summary.skipped, readyForAdoption: summary.readyForAdoption, report: path.join(destination, 'results.json') }, null, 2));
} catch (error) {
  fs.writeFileSync(path.join(destination, 'results.json'), JSON.stringify({
    status: 'failed', startedAt, readyForAdoption: false, exitCode: run?.status ?? null,
    reason: error.message,
  }, null, 2) + '\n');
  process.stderr.write(run?.stderr ?? '');
  throw error;
}
