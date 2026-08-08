import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateFoundation } from './check-foundation.mjs';

test('product process boundaries are present and one-way', async () => {
  const errors = await validateFoundation(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});
