import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

for (const app of ['admin', 'kiosk-web']) {
  test(`${app} is a private buildable UI package`, async () => {
    const pkg = JSON.parse(await readFile(new URL(`../apps/${app}/package.json`, import.meta.url), 'utf8'));
    assert.equal(pkg.private, true);
    assert.equal(pkg.dependencies['@puntiro/ui'], 'workspace:*');
    assert.equal(pkg.dependencies.react, '19.2.8');
    assert.equal(pkg.devDependencies.vite, '8.1.5');
  });
}
