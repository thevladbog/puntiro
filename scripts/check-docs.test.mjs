import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateRepositoryDocs } from './check-docs.mjs';

test('required repository documentation is complete', async () => {
  const errors = await validateRepositoryDocs(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});
