import tseslint from 'typescript-eslint';
import globals from 'globals';

// This config checks the laboratory's own code only, not the RAMAI application.
export default [
  { ignores: ['vendor/**', 'node_modules/**'] },
  ...tseslint.configs.recommended,
  {
    files: ['**/*.mjs'],
    languageOptions: { globals: globals.node },
    rules: { '@typescript-eslint/no-unused-vars': 'error' },
  },
];
