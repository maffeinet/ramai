import assert from 'node:assert/strict';
import test from 'node:test';
import { ESLint } from 'eslint';
import { app, BaselineESLint, reactRequire } from '../support.mjs';
import { reactCompatibility } from '../compatibility.mjs';

const cases = [
  { id: 'explicit-jsdoc-component', code: '/** @extends React.Component */ class View { componentWillMount() {} }' },
  { id: 'exported-jsdoc-component', code: '/** @extends React.Component */ export class View { componentWillUpdate() {} }' },
  { id: 'default-export-jsdoc-component', code: '/** @extends React.PureComponent */ export default class View { componentWillUpdate() {} }' },
  { id: 'class-expression-jsdoc', code: '/** @extends React.Component */ const View = class { componentWillUpdate() {} }' },
  { id: 'jsdoc-too-far', code: '/** @extends React.Component */\n\nclass View { componentWillMount() {} }' },
  { id: 'ordinary-comment-not-jsdoc', code: '/* @extends React.Component */ class View { componentWillMount() {} }' },
  { id: 'non-react-jsdoc', code: '/** @extends Other.Component */ class View { componentWillMount() {} }' },
  { id: 'unrelated-class', code: 'class View { componentWillMount() {} }' },
  { id: 'pure-component', code: 'class View extends React.PureComponent { componentWillReceiveProps() {} }' },
  { id: 'es5-component', code: 'React.createClass({componentWillMount: function() {}})' },
  { id: 'non-react-factory', code: 'otherFactory({componentWillMount: function() {}})' },
  { id: 'detect-version', code: 'React.createClass({})', version: 'detect' },
  { id: 'before-deprecation', code: 'React.createClass({})', version: '15.4.0' },
  { id: 'custom-pragma', code: 'Custom.createClass({})', pragma: 'Custom' },
  { id: 'destructured-require', code: 'const { render } = require("react-dom"); render(null, null)' },
  { id: 'destructured-alias', code: 'const { PropTypes: Legacy } = React; Legacy.string' },
  { id: 'legacy-render-import', code: 'import {render as legacyRender} from "react-dom"; legacyRender(null, null)' },
  { id: 'modern-server-render', code: 'import {renderToString} from "react-dom/server"; renderToString(null)' },
];
for (const item of cases) {
  test(`React compatibility edge ${item.id}`, async () => {
    const config = plugin => [{ files: ['**/*.jsx'], plugins: { react: plugin },
      languageOptions: { sourceType: 'module', ecmaVersion: 2022,
        parserOptions: { ecmaFeatures: { jsx: true } } },
      settings: { react: { version: item.version ?? '19.3.0', pragma: item.pragma ?? 'React' } },
      rules: { 'react/no-deprecated': 'error' } }];
    const old = new BaselineESLint({ cwd: app, overrideConfigFile: true,
      overrideConfig: config(reactRequire('eslint-plugin-react')) });
    const candidate = new ESLint({ cwd: app, overrideConfigFile: true,
      overrideConfig: config(reactCompatibility) });
    const [expected] = await old.lintText(item.code, { filePath: 'app/edge.jsx' });
    const [actual] = await candidate.lintText(item.code, { filePath: 'app/edge.jsx' });
    assert.equal(expected.fatalErrorCount, 0);
    assert.equal(actual.fatalErrorCount, 0);
    const normalize = messages => messages.map(({ ruleId, severity, message, line, column, endLine, endColumn }) =>
      ({ ruleId, severity, message, line, column, endLine, endColumn }));
    assert.deepEqual(normalize(actual.messages), normalize(expected.messages));
  });
}
