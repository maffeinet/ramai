import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { spawnSync } from 'node:child_process';
import { root, app, inventory, BaselineESLint, baselineRequire, handledByESLint,
  targetName, nativeConfig, createCandidate } from '../support.mjs';

test('frozen baseline versions and active rules/options/severities have not drifted', async () => {
  assert.equal(baselineRequire('eslint/package.json').version, inventory.baseline.eslint);
  const manifest = JSON.parse(fs.readFileSync(path.join(app, 'package.json')));
  assert.equal(manifest.devDependencies['eslint-config-next'], inventory.baseline.nextConfig);
  const linter = new BaselineESLint({ cwd: app });
  for (const profile of Object.values(inventory.profiles)) {
    const config = await linter.calculateConfigForFile(profile.file);
    const rules = Object.fromEntries(Object.entries(config.rules).filter(([, options]) => options[0] !== 0));
    assert.deepEqual(rules, profile.rules);
    assert.deepEqual(config.languageOptions.parser.meta, profile.parser);
  }
});

test('every frozen active rule has exactly one candidate owner and identical severity/options', async () => {
  const result = spawnSync(process.execPath, [path.join(root, 'node_modules/oxlint/bin/oxlint'),
    '--rules', '--format', 'json'], { encoding: 'utf8', cwd: root, maxBuffer: 4 * 1024 * 1024 });
  assert.equal(result.status, 0, result.stderr);
  const known = new Set(JSON.parse(result.stdout).map(x => `${x.scope.replaceAll('_', '-')}/${x.value}`));
  for (const profile of Object.values(inventory.profiles)) {
    const candidate = await createCandidate(profile.rules).calculateConfigForFile(profile.file);
    const native = nativeConfig(profile.rules).rules;
    for (const [name, options] of Object.entries(profile.rules)) {
      if (handledByESLint(name)) assert.deepEqual(candidate.rules[name], options, name);
      else {
        assert.ok(known.has(targetName(name)), `Unimplemented native rule ${name}`);
        assert.deepEqual(native[targetName(name)], options, name);
        assert.equal(candidate.rules[name], undefined, `Duplicate ownership: ${name}`);
      }
    }
    assert.equal(Object.keys(native).length + Object.keys(candidate.rules).length, Object.keys(profile.rules).length);
  }
});

test('upstream sources, helpers, licenses and fixture snapshots match normalized SHA256', () => {
  const upstream = JSON.parse(fs.readFileSync(path.join(root, 'UPSTREAM.json')));
  const expectedFiles = { ...upstream.files, ...upstream.localFiles };
  for (const [relative, originalHash] of Object.entries(expectedFiles)) {
    const expected = upstream.adaptations?.[relative]?.sha256 ?? originalHash;
    const source = fs.readFileSync(path.join(root, relative), 'utf8').replaceAll('\r\n', '\n').trimEnd() + '\n';
    assert.equal(crypto.createHash('sha256').update(source).digest('hex'), expected, relative);
  }
  assert.ok(upstream.files['vendor/react/LICENSE']);
  assert.ok(upstream.files['vendor/next/LICENSE.md']);
  assert.ok(upstream.localFiles['vendor/react/ESLINT-LICENSE']);
  function list(directory) {
    return fs.readdirSync(path.join(root, directory), { withFileTypes: true })
      .flatMap(entry => entry.isDirectory() ? list(`${directory}/${entry.name}`) : [`${directory}/${entry.name}`]);
  }
  assert.deepEqual(list('vendor').sort(), Object.keys(expectedFiles).sort(), 'Unlisted vendor files are forbidden');
});

test('candidate dependency graph has no legacy lint chain or ESLint 9', () => {
  const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json')));
  const forbidden = ['braces', 'micromatch', 'fast-glob', 'eslint-config-next', '@next/eslint-plugin-next', 'eslint-plugin-react'];
  for (const [name, entry] of Object.entries(lock.packages)) {
    for (const dependency of forbidden)
      assert.ok(!name.endsWith(`/node_modules/${dependency}`) && name !== `node_modules/${dependency}`, name);
    if (name.endsWith('/eslint')) assert.ok(entry.version.startsWith('10.'), name);
  }
  const require = createRequire(import.meta.url);
  assert.equal(require('eslint/package.json').version, '10.13.0');
});

test('candidate preserves baseline generated-file exclusions', async () => {
  const old = new BaselineESLint({ cwd: app });
  const candidate = createCandidate(inventory.profiles.typescript.rules);
  for (const file of ['.next/a.tsx', 'out/a.tsx', 'build/a.tsx', 'dist/a.tsx', 'next-env.d.ts', 'node_modules/example/a.js']) {
    assert.equal(await old.isPathIgnored(file), true, file);
    assert.equal(await candidate.isPathIgnored(file), true, file);
  }
});
