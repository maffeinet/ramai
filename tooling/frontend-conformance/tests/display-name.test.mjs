import assert from 'node:assert/strict';
import test from 'node:test';
import { compare } from '../support.mjs';

for (const [name, code, count] of [
  ['variable memo', 'import React from "react"; export const Page = React.memo(() => <div/>);', 1],
  ['named memo', 'import React from "react"; export default React.memo(function Page(){return <div/>;});', 0],
  ['default forwardRef', 'import React from "react"; export default React.forwardRef((p, ref) => <div ref={ref}/>);', 1],
  ['nested memo forwardRef', 'import React from "react"; export default React.memo(React.forwardRef((p,ref) => <div ref={ref}/>));', 0],
  ['anonymous export', 'export default () => <div/>;', 1],
  ['named function', 'export default function Page(){return <div/>;}', 0],
  ['explicit displayName', 'import React from "react"; const Page=React.memo(() => <div/>); Page.displayName="Page"; export default Page;', 0],
  ['unrelated memo', 'const other = {memo: x=>x}; export default other.memo(()=><div/>);', 0],
]) test(`display-name exact baseline: ${name}`, async () => {
  const result = await compare(code);
  assert.equal(result.baseline.filter(x => x.ruleId === 'react/display-name').length, count);
  assert.deepEqual(result.candidate, result.baseline);
});
