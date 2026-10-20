import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { ESLint } from 'eslint';
import tseslint from 'typescript-eslint';
import hermes from 'hermes-eslint';
import { root, app, BaselineESLint, baselineRequire, reactRequire } from '../support.mjs';
import { reactCompatibility } from '../compatibility.mjs';

// Capture pinned MIT test data only. Do not execute upstream helpers or their runner.
function fixtures(name) {
  const batches = [];
  const core = baselineRequire('eslint/use-at-your-own-risk').builtinRules;
  class Capture {
    constructor(options) { this.options = options; }
    run(rule, _implementation, cases) { batches.push({ rule, options: this.options, cases }); }
  }
  const allowed = {
    '../../helpers/ruleTester': Capture,
    '../../helpers/parsers': { all: x => x, disableNewTS: x => x },
    '../../helpers/getESLintCoreRule': name => core.get(name),
    '../../helpers/getRuleDefiner': () => ({ defineRule() {} }),
    [`../../../lib/rules/${name}`]: reactCompatibility.rules[name],
  };
  vm.runInNewContext(fs.readFileSync(path.join(root, 'vendor/react/upstream-tests', `${name}.js`), 'utf8'), {
    require(specifier) {
      if (!(specifier in allowed)) throw new Error(`Unexpected upstream import: ${specifier}`);
      return allowed[specifier];
    },
  }, { timeout: 2000, filename: `${name}.upstream.js` });
  return batches;
}

function signature(messages) {
  return messages.map(({ ruleId, severity, messageId, line, column, endLine, endColumn }) =>
    ({ ruleId, severity, messageId, line, column, endLine, endColumn }));
}

for (const name of ['display-name', 'no-deprecated', 'jsx-uses-react', 'jsx-uses-vars']) {
  for (const batch of fixtures(name)) {
    for (const validity of ['valid', 'invalid']) {
      batch.cases[validity].forEach((fixture, index) => {
        const item = typeof fixture === 'string' ? { code: fixture } : fixture;
        test(`upstream ${name}/${batch.rule}/${validity}/${index}`, async () => {
          const settings = { ...item.settings, react: { version: '19.3.0', ...item.settings?.react } };
          const ruleId = batch.rule === name ? `react/${name}` : batch.rule;
          const typed = item.features?.some(feature => /^(types|ts)$|annotations/.test(feature));
          const flow = item.features?.includes('flow');
          const filename = typed ? 'app/upstream.tsx' : 'app/upstream.jsx';
          const baselineParser = typed
            ? (await new BaselineESLint({ cwd: app }).calculateConfigForFile(filename)).languageOptions.parser
            : flow ? hermes : undefined;
          const config = (plugin, parser) => [{
            files: ['**/*.{jsx,tsx}'],
            languageOptions: { sourceType: 'module', ecmaVersion: 2022,
              ...(parser ? { parser } : {}),
              parserOptions: { ecmaFeatures: { jsx: true } } },
            plugins: { react: plugin }, settings,
            rules: { [ruleId]: [2, ...(item.options ?? [])] },
          }];
          const oldPlugin = reactRequire('eslint-plugin-react');
          const old = new BaselineESLint({ cwd: app, overrideConfigFile: true, overrideConfig: config(oldPlugin, baselineParser) });
          const candidate = new ESLint({ cwd: app, overrideConfigFile: true, overrideConfig: config(reactCompatibility, typed ? tseslint.parser : flow ? hermes : undefined) });
          const [expected] = await old.lintText(item.code, { filePath: filename });
          const [actual] = await candidate.lintText(item.code, { filePath: filename });
          assert.equal(expected.fatalErrorCount, 0, JSON.stringify(expected.messages));
          assert.equal(actual.fatalErrorCount, 0, JSON.stringify(actual.messages));
          assert.equal(expected.messages.length, validity === 'valid' ? 0 : item.errors.length,
            'The upstream fixture must remain a meaningful oracle');
          assert.deepEqual(signature(actual.messages), signature(expected.messages));
        });
      });
    }
  }
}
