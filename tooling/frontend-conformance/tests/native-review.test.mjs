import assert from 'node:assert/strict';
import test from 'node:test';
import { compare } from '../support.mjs';

const cases = [
  ['react/display-name', 'import React from "react"; export default React.memo(() => <div/>);'],
  ['react/jsx-no-comment-textnodes', 'export default function Page(){return <div>// mistaken comment</div>;}'],
  ['react/jsx-no-duplicate-props', 'export default function Page(){return <div id="a" id="b"/>;}'],
  ['react/jsx-no-undef', 'export default function Page(){return <Missing/>;}'],
  ['react/no-children-prop', 'export default function Page(){return <div children="x"/>;}'],
  ['react/no-danger-with-children', 'export default function Page(){return <div dangerouslySetInnerHTML={{__html:"x"}}>child</div>;}'],
  ['react/no-direct-mutation-state', 'import React from "react"; export default class Page extends React.Component { update(){this.state.x=1;} render(){return null;} }'],
  ['react/no-find-dom-node', 'import ReactDOM from "react-dom"; export function go(){return ReactDOM.findDOMNode(null);}'],
  ['react/no-is-mounted', 'import React from "react"; export default class Page extends React.Component { componentDidMount(){this.isMounted();} render(){return null;} }'],
  ['react/no-render-return-value', 'import ReactDOM from "react-dom"; export const result = ReactDOM.render(null, null);'],
  ['react/no-string-refs', 'import React from "react"; export default class Page extends React.Component { render(){return <div ref="legacy"/>;} }'],
  ['react/no-unescaped-entities', 'export default function Page(){return <div>don\'t</div>;}'],
  ['react/require-render-return', 'import React from "react"; export default class Page extends React.Component { render(){} }'],
  ['@next/next/google-font-display', 'export default function Page(){return <link href="https://fonts.googleapis.com/css?family=Roboto"/>;}'],
  ['@next/next/google-font-preconnect', 'export default function Page(){return <link href="https://fonts.gstatic.com"/>;}'],
  ['@next/next/next-script-for-ga', 'export default function Page(){return <script async src="https://www.googletagmanager.com/gtag/js?id=G-EXAMPLE"/>;}'],
  ['@next/next/no-async-client-component', '"use client"; export default async function Page(){return <div/>;}'],
  ['@next/next/no-before-interactive-script-outside-document', 'import Script from "next/script"; export default function Page(){return <Script strategy="beforeInteractive" src="/a.js"/>;}', 'pages/page.tsx'],
  ['@next/next/no-css-tags', 'export default function Page(){return <link rel="stylesheet" href="/a.css"/>;}'],
  ['@next/next/no-head-element', 'export default function Page(){return <head><title>x</title></head>;}', 'pages/page.tsx'],
  ['@next/next/no-html-link-for-pages', 'export default function Page(){return <a href="/">Home</a>;}'],
  ['@next/next/no-img-element', 'export default function Page(){return <img src="/a.png" alt="test"/>;}'],
  ['@next/next/no-page-custom-font', 'export default function Page(){return <link rel="stylesheet" href="https://fonts.googleapis.com/css?family=Roboto&display=swap"/>;}'],
  ['@next/next/no-styled-jsx-in-document', 'export default function Document(){return <style jsx>{`div {color:red}`}</style>;}', 'pages/_document.tsx'],
  ['@next/next/no-title-in-document-head', 'import {Head} from "next/document"; export default function Document(){return <Head><title>x</title></Head>;}', 'pages/_document.tsx'],
  ['@next/next/no-typos', 'export const getStaticprops = () => ({props:{}}); export default function Page(){return null;}', 'pages/page.tsx'],
  ['@next/next/no-unwanted-polyfillio', 'export default function Page(){return <script async src="https://polyfill.io/v3/polyfill.min.js?features=es2015"/>;}'],
  ['@next/next/no-assign-module-variable', 'export function example(){const module = {}; return module;}'],
  ['@next/next/no-document-import-in-page', 'import Document from "next/document"; export default Document;'],
  ['@next/next/no-duplicate-head', 'import Document,{Html,Head} from "next/document"; export default class MyDocument extends Document { render(){return <Html><Head/><Head/></Html>;} }', 'pages/_document.tsx'],
  ['@next/next/no-head-import-in-document', 'import Head from "next/head"; export default Head;', 'pages/_document.tsx'],
  ['@next/next/no-script-component-in-head', 'import Head from "next/head"; import Script from "next/script"; export default function Page(){return <Head><Script src="/a.js"/></Head>;}'],
  ['import/no-anonymous-default-export', 'export default { value: 1 };'],
  ['jsx-a11y/aria-proptypes', 'export default function Page(){return <div aria-hidden="maybe"/>;}'],
  ['jsx-a11y/aria-unsupported-elements', 'export default function Page(){return <meta aria-label="bad"/>;}'],
  ['jsx-a11y/role-has-required-aria-props', 'export default function Page(){return <div role="checkbox"/>;}'],
  ['jsx-a11y/role-supports-aria-props', 'export default function Page(){return <div role="button" aria-checked="true"/>;}'],
];
for (const [rule, code, filename] of cases) {
  test(`native review ${rule}`, async () => {
    const result = await compare(code, 'tsx', filename);
    assert.ok(result.baseline.some(x => x.ruleId === rule), `Fixture does not exercise ${rule}: ${JSON.stringify(result)}`);
    assert.deepEqual(result.candidate, result.baseline);
  });
}

for (const [name, code, filename] of [
  ['App Router permits head', 'export default function Page(){return <head><title>x</title></head>;}', 'app/page.tsx'],
  ['functional Document duplicate Head', 'import {Html,Head} from "next/document"; export default function Document(){return <Html><Head/><Head/></Html>;}', 'pages/_document.tsx'],
]) test(`native review valid scope ${name}`, async () => {
  const result = await compare(code, 'tsx', filename);
  assert.deepEqual(result.baseline, []);
  assert.deepEqual(result.candidate, result.baseline);
});
