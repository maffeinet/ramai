import assert from 'node:assert/strict';
import test from 'node:test';
import { compare } from '../support.mjs';

const cases = [
  {
    "name": "null",
    "code": "import React from \"react\"; export default React.memo(()=>null);",
    "count": 1
  },
  {
    "name": "number",
    "code": "import React from \"react\"; export default React.memo(()=>1);",
    "count": 1
  },
  {
    "name": "no return",
    "code": "import React from \"react\"; export default React.memo(()=>{});",
    "count": 1
  },
  {
    "name": "createElement",
    "code": "import React from \"react\"; export default React.memo(()=>React.createElement(\"div\"));",
    "count": 1
  },
  {
    "name": "fragment",
    "code": "import React from \"react\"; export default React.memo(()=><></>);",
    "count": 1
  },
  {
    "name": "named import",
    "code": "import {memo} from \"react\"; export default memo(()=><div/>);",
    "count": 1
  },
  {
    "name": "aliased import",
    "code": "import {memo as wrap} from \"react\"; export default wrap(()=><div/>);",
    "count": 0
  },
  {
    "name": "namespace import",
    "code": "import * as React from \"react\"; export default React.memo(()=><div/>);",
    "count": 1
  },
  {
    "name": "default alias",
    "code": "import R from \"react\"; export default R.memo(()=><div/>);",
    "count": 0
  },
  {
    "name": "no import",
    "code": "export default React.memo(()=><div/>);",
    "count": 1
  },
  {
    "name": "computed string",
    "code": "import React from \"react\"; export default React[\"memo\"](()=><div/>);",
    "count": 0
  },
  {
    "name": "computed identifier",
    "code": "import React from \"react\"; const memo=\"memo\"; export default React[memo](()=><div/>);",
    "count": 1
  },
  {
    "name": "function callback",
    "code": "import React from \"react\"; export default React.memo(function(){return <div/>;});",
    "count": 1
  },
  {
    "name": "named callback",
    "code": "import React from \"react\"; export default React.memo(function Page(){return <div/>;});",
    "count": 0
  },
  {
    "name": "async callback",
    "code": "import React from \"react\"; export default React.memo(async()=> <div/>);",
    "count": 1
  },
  {
    "name": "generator callback",
    "code": "import React from \"react\"; export default React.memo(function*(){yield <div/>;});",
    "count": 1
  },
  {
    "name": "named child",
    "code": "import React from \"react\"; function Child(){return <div/>;} export default React.memo(()=><Child/>);",
    "count": 1
  },
  {
    "name": "nested memo",
    "code": "import React from \"react\"; export default React.memo(React.memo(()=><div/>));",
    "count": 0
  },
  {
    "name": "require destructured",
    "code": "const {memo}=require(\"react\"); export default memo(()=><div/>);",
    "count": 1
  },
  {
    "name": "nonreact destructured",
    "code": "import {memo} from \"other\"; export default memo(()=><div/>);",
    "count": 0
  },
  {
    "name": "call argument identifier",
    "code": "import React from \"react\"; const page=()=> <div/>; export default React.memo(page);",
    "count": 0
  },
  {
    "name": "commonjs",
    "code": "const React=require(\"react\"); module.exports=React.memo(()=><div/>);",
    "count": 1
  },
  {
    "name": "TS assertion",
    "code": "import React from \"react\"; export default React.memo((()=> <div/>) as any);",
    "count": 0
  },
  {
    "name": "nested unrelated",
    "code": "import React from \"react\"; export default outer(React.memo(()=><div/>));",
    "count": 1
  }
];
for (const { name, code, count } of cases) test(`display-name wrapper matrix: ${name}`, async () => {
  const result = await compare(code);
  assert.equal(result.baseline.filter(x => x.ruleId === 'react/display-name').length, count);
  assert.deepEqual(result.candidate, result.baseline);
});
