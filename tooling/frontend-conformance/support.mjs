import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { createRequire } from 'node:module';
import { spawnSync } from 'node:child_process';
import { ESLint } from 'eslint';
import tseslint from 'typescript-eslint';
import hooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import { reactCompatibility, nextCompatibility } from './compatibility.mjs';

export const root = import.meta.dirname;
export const app = path.resolve(root, '../../src/frontend/ramai-web');
export const baselineRequire = createRequire(path.join(app, 'package.json'));
export const { ESLint: BaselineESLint } = baselineRequire('eslint');
export const reactRequire = createRequire(baselineRequire.resolve('eslint-config-next'));
export const inventory = JSON.parse(fs.readFileSync(path.join(root, 'inventory.json'), 'utf8'));
export const ignores = ['.next/**', 'out/**', 'build/**', 'dist/**', 'next-env.d.ts', 'node_modules/**'];
export const bridgeRules = new Set([
  'react/display-name',
  'react/no-deprecated', 'react/jsx-uses-react', 'react/jsx-uses-vars',
  '@next/next/no-location-assign-relative-destination',
  '@next/next/no-duplicate-head',
]);
export const targetName = name => name.replace('@next/next/', 'nextjs/');
export const handledByESLint = name => bridgeRules.has(name) ||
  name.startsWith('@typescript-eslint/') || name.startsWith('react-hooks/') || !name.includes('/');

export function candidateConfig(rules, settings = {}) {
  return [{ ignores }, {
    files: ['**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}'],
    languageOptions: {
      parser: tseslint.parser, sourceType: 'module',
      parserOptions: { ecmaFeatures: { jsx: true } },
      globals: { ...globals.browser, ...globals.node },
    },
    settings: { react: { version: '19.3.0' }, ...settings },
    plugins: {
      '@typescript-eslint': tseslint.plugin, 'react-hooks': hooks,
      react: reactCompatibility, '@next/next': nextCompatibility,
    },
    rules: Object.fromEntries(Object.entries(rules).filter(([name]) => handledByESLint(name))),
  }];
}

export function createCandidate(rules, settings) {
  return new ESLint({ cwd: app, overrideConfigFile: true,
    overrideConfig: candidateConfig(rules, settings) });
}

export function nativeConfig(rules) {
  return {
    plugins: ['react', 'nextjs', 'jsx-a11y', 'import'],
    categories: Object.fromEntries(['correctness', 'suspicious', 'pedantic', 'perf',
      'style', 'restriction', 'nursery'].map(name => [name, 'off'])),
    rules: Object.fromEntries(Object.entries(rules).filter(([name]) => !handledByESLint(name))
      .map(([name, options]) => [targetName(name), options])),
    settings: { react: { version: '19.3.0' } },
    ignorePatterns: ignores,
  };
}

export function nativeLint(code, extension, rules, relativeFile = `app/probe.${extension}`) {
  const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'ramai-native-conformance-'));
  try {
    const file = path.resolve(scratch, relativeFile);
    if (!file.startsWith(path.resolve(scratch) + path.sep)) throw new Error('Unsafe fixture path');
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const config = path.join(scratch, '.oxlintrc.json');
    fs.writeFileSync(file, code);
    fs.writeFileSync(config, JSON.stringify(nativeConfig(rules)));
    const result = spawnSync(process.execPath, [path.join(root, 'node_modules/oxlint/bin/oxlint'),
      '-c', config, file, '--format', 'json', '--threads', '1'], { cwd: app, encoding: 'utf8', timeout: 15000,
      maxBuffer: 4 * 1024 * 1024 });
    if (result.error || ![0, 1].includes(result.status))
      throw new Error(`Oxlint execution failed: ${result.error ?? result.stderr}`);
    const output = JSON.parse(result.stdout);
    if (!Array.isArray(output.diagnostics)) throw new Error('Missing Oxlint diagnostics');
    if (output.number_of_files !== 1 || output.number_of_rules !== Object.keys(nativeConfig(rules).rules).length)
      throw new Error('Oxlint did not run the complete requested rule set on the fixture');
    return output.diagnostics.map(item => {
      const match = /^(.*?)\((.*?)\)$/.exec(item.code ?? '');
      if (!match) throw new Error(`Unrecognized Oxlint rule: ${JSON.stringify(item)}`);
      if (!['error', 'warning'].includes(item.severity))
        throw new Error(`Unrecognized Oxlint severity: ${item.severity}`);
      const prefix = match[1] === 'next' ? '@next/next' : match[1];
      return { ruleId: `${prefix}/${match[2]}`, severity: item.severity === 'error' ? 2 : 1 };
    });
  } finally {
    // Only remove this freshly created, literal temporary directory, never the workspace.
    if (path.dirname(path.resolve(scratch)) !== path.resolve(os.tmpdir()) ||
        !path.basename(scratch).startsWith('ramai-native-conformance-'))
      throw new Error('Unsafe scratch cleanup path');
    fs.rmSync(scratch, { recursive: true, force: true });
  }
}

export function diagnosticSignature(messages) {
  return messages.map(({ ruleId, severity }) => ({ ruleId, severity }))
    .sort((a, b) => a.ruleId.localeCompare(b.ruleId) || a.severity - b.severity);
}

export async function compare(code, extension = 'tsx', filename = `app/conformance.${extension}`) {
  const profile = ['ts', 'tsx', 'mts', 'cts'].includes(extension) ? 'typescript' : 'javascript';
  const rules = inventory.profiles[profile].rules;
  const baseline = new BaselineESLint({ cwd: app,
    overrideConfig: { settings: { react: { version: '19.3.0' } } } });
  const candidate = createCandidate(rules);
  const [old] = await baseline.lintText(code, { filePath: filename });
  const [current] = await candidate.lintText(code, { filePath: filename });
  if (old.messages.some(x => x.fatal) || current.messages.some(x => x.fatal))
    throw new Error(`Parser failure: ${JSON.stringify({ old: old.messages, current: current.messages })}`);
  return {
    baseline: diagnosticSignature(old.messages),
    candidate: diagnosticSignature([...current.messages, ...nativeLint(code, extension, rules, filename)]),
  };
}
