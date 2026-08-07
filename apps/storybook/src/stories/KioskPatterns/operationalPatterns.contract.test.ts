import { readFileSync } from 'node:fs';
import type {
  EmptyStateProps,
  ErrorStateProps,
  LoadingStateProps,
  PrintProgressProps,
  UnknownPrintResultProps
} from '@puntiro/ui';
import { expect, it } from 'vitest';

type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends (<T>() => T extends B ? 1 : 2) ? true : false;

const printProgressPropsAreExact: Equal<PrintProgressProps, {
  shipmentNumber: string;
  completed: number;
  total: number;
  printerName: string;
}> = true;
const emptyStatePropsAreExact: Equal<EmptyStateProps, {
  title: string;
  description: string;
  action?: { label: string; onPress: () => void };
}> = true;
const loadingStatePropsAreExact: Equal<LoadingStateProps, { label: string }> = true;
const errorStatePropsAreExact: Equal<ErrorStateProps, {
  title: string;
  description: string;
  recoveryAction?: { label: string; onPress: () => void };
}> = true;
const unknownPrintResultPropsAreExact: Equal<UnknownPrintResultProps, {
  shipmentNumber: string;
  placeCount: number;
  onRetryAll: () => void;
  onSelectPlaces: () => void;
  onResolveWithoutReprint: () => void;
}> = true;

void printProgressPropsAreExact;
void emptyStatePropsAreExact;
void loadingStatePropsAreExact;
void errorStatePropsAreExact;
void unknownPrintResultPropsAreExact;

it('keeps operational-pattern APIs closed and styling library-owned', () => {
  const typeFiles = [
    'PrintProgress/PrintProgress.types.ts',
    'EmptyState/EmptyState.types.ts',
    'LoadingState/LoadingState.types.ts',
    'ErrorState/ErrorState.types.ts',
    'UnknownPrintResult/UnknownPrintResult.types.ts'
  ];

  for (const relativePath of typeFiles) {
    const source = readFileSync(
      new URL(`../../../../../packages/ui/src/patterns/${relativePath}`, import.meta.url),
      'utf8'
    );
    expect(source).not.toContain('react-aria-components');
    expect(source).not.toMatch(/\bclassName\??:/);
    expect(source).not.toMatch(/\bstyle\??:/);
  }
});

it('uses semantic tokens without leaking reference tokens', () => {
  for (const name of ['PrintProgress', 'EmptyState', 'LoadingState', 'ErrorState', 'UnknownPrintResult']) {
    const css = readFileSync(
      new URL(`../../../../../packages/ui/src/patterns/${name}/${name}.module.css`, import.meta.url),
      'utf8'
    );
    expect(css).not.toContain('--puntiro-reference-');
  }
});
