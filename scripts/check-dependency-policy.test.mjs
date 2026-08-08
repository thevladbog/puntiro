import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateDependencyPolicy } from './check-dependency-policy.mjs';

test('toolchains and package manifests use exact stable versions', async () => {
  const errors = await validateDependencyPolicy(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});
