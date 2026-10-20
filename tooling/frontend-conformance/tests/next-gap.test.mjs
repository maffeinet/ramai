import assert from 'node:assert/strict';
import test from 'node:test';
import { ESLint } from 'eslint';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import { app, BaselineESLint, baselineRequire } from '../support.mjs';
import { nextCompatibility } from '../compatibility.mjs';

const rule = 'no-location-assign-relative-destination';
const oldParser = (await new BaselineESLint({ cwd: app }).calculateConfigForFile('app/page.tsx')).languageOptions.parser;
const invalid = [
  'window.location.assign("/dashboard")', 'location.assign("./dashboard")',
  'globalThis.location.assign("../dashboard")', 'self.location.assign("dashboard")',
  'document.location.assign("?page=2")', 'location.assign("#anchor")',
  'window.location["assign"]("/x")', 'window["location"].assign("/x")',
  'window.location.href = "/x"', 'location["href"] = "/x"',
  'document.location.href = "/x"', 'globalThis.location.href = "/x"',
  'const target = "/x"; window.location.assign(target)',
  'const target = "/x"; const alias = target; location.assign(alias)',
  'let target = "/first"; target = "/second"; location.href = target',
  'const id = 3; window.location.assign(`/users/${id}`)',
  'const id = 3; location.href = "/users/" + id',
  'location.assign("")', 'window.location.href += "/x"',
];
const valid = [
  'window.location.assign("https://example.invalid/path")',
  'location.assign("//example.invalid/path")',
  'location.href = "mailto:user@example.invalid"',
  'const url = getUrl(); window.location.assign(url)',
  'window.location.assign()', 'const args = ["/x"]; location.assign(...args)',
  'function navigate(window) { window.location.assign("/x"); }',
  'function navigate(location) { location.href = "/x"; }',
  'const window = { location: { assign() {} } }; window.location.assign("/x")',
  'const location = { href: "" }; location.href = "/x"',
  'const dest = "https://example.invalid"; window.location.assign(dest)',
  'const x = {}; x.location.assign("/x")',
  'window.location.replace("/x")',
  'const location = { assign() {} }; location.assign("/x")',
  'window.location.assign("HTTPS://example.invalid")',
];

for (const [validity, cases] of [['invalid', invalid], ['valid', valid]]) {
  cases.forEach((code, index) => test(`Next gap ${validity}/${index}`, async () => {
    const makeConfig = (plugin, parser) => [{
      files: ['**/*.tsx'], languageOptions: { parser,
        globals: { ...globals.browser, ...globals.node }, sourceType: 'module' },
      plugins: { '@next/next': plugin }, rules: { [`@next/next/${rule}`]: 'warn' },
    }];
    const originalPlugin = baselineRequire('@next/eslint-plugin-next');
    const baseline = new BaselineESLint({ cwd: app, overrideConfigFile: true,
      overrideConfig: makeConfig(originalPlugin, oldParser) });
    const candidate = new ESLint({ cwd: app, overrideConfigFile: true,
      overrideConfig: makeConfig(nextCompatibility, tseslint.parser) });
    const [old] = await baseline.lintText(code, { filePath: 'app/gap.tsx' });
    const [actual] = await candidate.lintText(code, { filePath: 'app/gap.tsx' });
    assert.equal(old.fatalErrorCount, 0);
    assert.equal(actual.fatalErrorCount, 0);
    assert.equal(old.messages.length, validity === 'invalid' ? 1 : 0, code);
    const signature = messages => messages.map(({ ruleId, severity, messageId, message, line, column, endLine, endColumn }) =>
      ({ ruleId, severity, messageId, message, line, column, endLine, endColumn }));
    assert.deepEqual(signature(actual.messages), signature(old.messages));
  }));
}
