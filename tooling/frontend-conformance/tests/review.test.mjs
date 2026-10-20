import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { parseTestResult } from '../report-core.mjs';
import { root, createCandidate, inventory } from '../support.mjs';

for (const code of [
  'let first = second; let second = first; window.location.assign(first);',
  'let self = self; window.location.href = self;',
  Array.from({ length: 140 }, (_, i) => `const v${i} = ${i === 139 ? 'getUnknown()' : `v${i + 1}`};`).join('') + 'window.location.assign(v0);',
]) {
  test(`review bounded Next fallback ${code.slice(0, 42)}`, async () => {
    await assert.rejects(createCandidate(inventory.profiles.typescript.rules)
      .lintText(code, { filePath: 'app/bounded.tsx' }), /RAMAI navigation target exceeds bounded static analysis/);
  });
}

for (const invalid of [
  { status: 1, stdout: '' }, { status: 0, stdout: '' },
  { status: 0, stdout: '# tests 0\n# pass 0\n# fail 0\n# skipped 0\n# cancelled 0\n# todo 0\n' },
  { status: 0, stdout: '# tests 2\n# pass 1\n# fail 0\n# skipped 1\n# cancelled 0\n# todo 0\n' },
]) test(`review reject incomplete report ${JSON.stringify(invalid)}`, () => {
  assert.throws(() => parseTestResult(invalid));
});

test('review reject non-green summary even when child returns zero', () => {
  assert.throws(() => parseTestResult({ status: 0,
    stdout: '# tests 2\n# pass 1\n# fail 1\n# skipped 0\n# cancelled 0\n# todo 0\n' }));
});

for (const missing of ['support.mjs', 'report-core.mjs']) test(`review report invalidates old green before missing ${missing}`, () => {
  const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'ramai-report-review-'));
  try {
    const lab = path.join(scratch, 'tooling/frontend-conformance');
    const artifacts = path.join(scratch, 'artifacts/frontend-conformance');
    fs.mkdirSync(lab, { recursive: true });
    fs.mkdirSync(artifacts, { recursive: true });
    for (const file of ['report.mjs', 'report-core.mjs'].filter(file => file !== missing))
      fs.copyFileSync(path.join(root, file), path.join(lab, file));
    fs.writeFileSync(path.join(artifacts, 'results.json'), '{"status":"passed"}');
    const run = spawnSync(process.execPath, [path.join(lab, 'report.mjs')], { encoding: 'utf8', timeout: 10000 });
    assert.notEqual(run.status, 0);
    const result = JSON.parse(fs.readFileSync(path.join(artifacts, 'results.json')));
    assert.equal(result.status, 'failed');
    assert.equal(result.readyForAdoption, false);
    assert.ok(result.reason.includes(missing));
  } finally {
    assert.equal(path.dirname(path.resolve(scratch)), path.resolve(os.tmpdir()));
    assert.ok(path.basename(scratch).startsWith('ramai-report-review-'));
    fs.rmSync(scratch, { recursive: true, force: true });
  }
});
