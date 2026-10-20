import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs';
import path from 'node:path';
import { root, compare } from '../support.mjs';

const cases = JSON.parse(fs.readFileSync(path.join(root, 'cases.json')));
for (const item of cases) {
  test(`full preset ${item.id}.${item.extension}`, async () => {
    const result = await compare(item.code, item.extension);
    for (const ruleId of item.expected) assert.ok(result.baseline.some(x => x.ruleId === ruleId), ruleId);
    if (item.expected.length === 0) assert.deepEqual(result.baseline, [], item.id);
    assert.deepEqual(result.candidate, result.baseline);
  });
}

const valid = 'function Child() { return <span/>; } export default function Page() { return <Child/>; }';
for (const extension of ['js', 'jsx', 'mjs', 'tsx']) {
  test(`JSX bindings and legacy React import in ${extension}`, async () => {
    const result = await compare('import React from "react"; ' + valid, extension);
    assert.deepEqual(result.baseline, []);
    assert.deepEqual(result.candidate, []);
  });
}
for (const extension of ['js', 'mjs', 'ts', 'tsx', 'mts', 'cts']) {
  test(`deprecated destructured ReactDOM import in ${extension}`, async () => {
    const code = 'import {render} from "react-dom"; export function go() { return render(null, null); }';
    const result = await compare(code, extension);
    assert.ok(result.baseline.some(x => x.ruleId === 'react/no-deprecated'));
    assert.deepEqual(result.candidate, result.baseline);
  });
}
