'use strict';

// Class-only adaptation of ESLint 9.39.5 SourceCode.getJSDocComment (MIT).
// Copyright OpenJS Foundation and other contributors; see ../../ESLINT-LICENSE.
// This is deliberately NOT a replacement for the full removed SourceCode API.
module.exports = function getClassJSDoc(sourceCode, node) {
  let anchor;
  if (node.type === 'ClassDeclaration') {
    const parent = node.parent;
    anchor = parent && ['ExportDefaultDeclaration', 'ExportNamedDeclaration',
      'ExportAllDeclaration', 'ExportSpecifier'].includes(parent.type) ? parent : node;
  } else if (node.type === 'ClassExpression') {
    anchor = node.parent.parent;
  } else {
    return null;
  }
  const before = sourceCode.getTokenBefore(anchor, { includeComments: true });
  return before && before.type === 'Block' && before.value.charAt(0) === '*'
    && anchor.loc.start.line - before.loc.end.line <= 1 ? before : null;
};
