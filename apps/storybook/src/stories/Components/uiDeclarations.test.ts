import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { expect, it } from 'vitest';

const checker = new URL('../../../../../scripts/check-ui-declarations.mjs', import.meta.url).pathname;

function runChecker(directory: string) {
  return spawnSync(process.execPath, [checker, directory], { encoding: 'utf8' });
}

function withFixture(contents: string, assertion: (directory: string) => void) {
  const directory = mkdtempSync(join(tmpdir(), 'puntiro-ui-declarations-'));
  writeFileSync(join(directory, 'fixture.d.ts'), contents);

  try {
    assertion(directory);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

it('accepts a declaration fixture without Lucide', () => {
  withFixture("export type IconName = 'printer';\n", (directory) => {
    const result = runChecker(directory);

    expect(result.status).toBe(0);
    expect(result.stderr).toBe('');
  });
});

it('rejects a declaration fixture that leaks Lucide', () => {
  withFixture("export type Glyph = import('lucide-react').LucideIcon;\n", (directory) => {
    const result = runChecker(directory);

    expect(result.status).toBe(1);
    expect(result.stderr).toContain('fixture.d.ts');
    expect(result.stderr).toContain('lucide-react');
  });
});

it('reports a missing directory as missing declarations', () => {
  const parent = mkdtempSync(join(tmpdir(), 'puntiro-ui-declarations-'));

  try {
    const result = runChecker(join(parent, 'missing'));

    expect(result.status).not.toBe(0);
    expect(result.stderr).toContain('No UI declarations found');
  } finally {
    rmSync(parent, { recursive: true, force: true });
  }
});

it('reports an empty directory as missing declarations', () => {
  const directory = mkdtempSync(join(tmpdir(), 'puntiro-ui-declarations-'));

  try {
    const result = runChecker(directory);

    expect(result.status).not.toBe(0);
    expect(result.stderr).toContain('No UI declarations found');
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
