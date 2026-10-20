import assert from 'node:assert/strict';
import test from 'node:test';
import { compare } from '../support.mjs';

const rule = '@next/next/no-duplicate-head';
for (const [name, code, count] of [
  ['aliased Document', 'import Base,{Html,Head} from "next/document"; export default class Page extends Base {render(){return <Html><Head/><Head/></Html>;}}', 1],
  ['three heads', 'import Document,{Html,Head} from "next/document"; export default class Page extends Document {render(){return <Html><Head/><Head/><Head/></Html>;}}', 2],
  ['one head', 'import Document,{Html,Head} from "next/document"; export default class Page extends Document {render(){return <Html><Head/></Html>;}}', 0],
  ['unrelated base', 'import Document,{Html,Head} from "next/document"; class Other {} export default class Page extends Other {render(){return <Html><Head/><Head/></Html>;}}', 0],
  ['function', 'import Document,{Html,Head} from "next/document"; export default function Page(){return <Html><Head/><Head/></Html>;}', 0],
  ['nested heads', 'import Document,{Html,Head} from "next/document"; export default class Page extends Document {render(){return <Html><div><Head/><Head/></div></Html>;}}', 0],
  ['aliased head', 'import Document,{Html,Head as Header} from "next/document"; export default class Page extends Document {render(){return <Html><Header/><Header/></Html>;}}', 0],
]) test(`duplicate Head exact upstream scope: ${name}`, async () => {
  const result = await compare(code, 'tsx', 'pages/_document.tsx');
  assert.equal(result.baseline.filter(x => x.ruleId === rule).length, count);
  assert.deepEqual(result.candidate, result.baseline);
});
