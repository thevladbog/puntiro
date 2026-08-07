import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { expect, it } from 'vitest';

const checker = new URL('../../../../../scripts/check-ui-declarations.mjs', import.meta.url).pathname;

function runChecker(contents: string) {
  const directory = mkdtempSync(join(tmpdir(), 'puntiro-ui-declarations-'));
  writeFileSync(join(directory, 'fixture.d.ts'), contents);

  try {
    return spawnSync(process.execPath, [checker, directory], { encoding: 'utf8' });
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

it('accepts a declaration fixture without Lucide', () => {
  const result = runChecker("export type IconName = 'printer';\n");

  expect(result.status).toBe(0);
  expect(result.stderr).toBe('');
});

it('rejects a declaration fixture that leaks Lucide', () => {
  const result = runChecker("export type Glyph = import('lucide-react').LucideIcon;\n");

  expect(result.status).toBe(1);
  expect(result.stderr).toContain('fixture.d.ts');
  expect(result.stderr).toContain('lucide-react');
});
