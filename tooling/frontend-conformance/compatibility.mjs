import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
// Only these audited dependency closures are loaded, never the original plugin index.
export const reactCompatibility = {
  rules: Object.fromEntries(['display-name', 'no-deprecated', 'jsx-uses-react', 'jsx-uses-vars'].map(name =>
    [name, require(`./vendor/react/lib/rules/${name}.js`)])),
};
export const nextCompatibility = {
  rules: {
    'no-duplicate-head': require('./vendor/next/rules/no-duplicate-head.js').default,
    'no-location-assign-relative-destination':
      require('./vendor/next/rules/no-location-assign-relative-destination.js').default,
  },
};
