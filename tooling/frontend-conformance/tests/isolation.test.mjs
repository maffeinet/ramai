import assert from 'node:assert/strict';
import test from 'node:test';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
test('compatibility rules execute without loading the baseline or legacy plugin indexes', () => {
  const source = `
    import Module from 'node:module';
    const original = Module._load;
    Module._load = function(specifier, ...rest) {
      if (/fast-glob|micromatch|braces|eslint-config-next/.test(specifier)
          || /src[\\\\/]frontend[\\\\/]ramai-web/.test(specifier))
        throw new Error('Forbidden baseline dependency: ' + specifier);
      return original.call(this, specifier, ...rest);
    };
    const { ESLint } = await import('eslint');
    const { reactCompatibility, nextCompatibility } = await import('./compatibility.mjs');
    const { default: globals } = await import('globals');
    const lint = new ESLint({ overrideConfigFile: true, overrideConfig: [{
      languageOptions: { globals: globals.browser, parserOptions: { ecmaFeatures: { jsx: true } } },
      plugins: { react: reactCompatibility, '@next/next': nextCompatibility },
      settings: { react: { version: '19.3.0' } },
      rules: { 'react/no-deprecated': 'error',
        'react/display-name': 'error',
        '@next/next/no-location-assign-relative-destination': 'warn' }
    }] });
    const [result] = await lint.lintText('React.createClass({}); window.location.assign("/x");');
    if (result.messages.length !== 2 || result.fatalErrorCount) throw new Error(JSON.stringify(result.messages));
    const [wrapper] = await lint.lintText('export default React.memo(() => <div/>);');
    if (wrapper.messages.length !== 1 || wrapper.messages[0].ruleId !== 'react/display-name'
        || wrapper.fatalErrorCount) throw new Error(JSON.stringify(wrapper.messages));
    console.log('isolated compatibility execution: PASS');
  `;
  const result = spawnSync(process.execPath, ['--input-type=module', '-e', source], {
    cwd: root, encoding: 'utf8', timeout: 15000,
  });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /isolated compatibility execution: PASS/);
});
