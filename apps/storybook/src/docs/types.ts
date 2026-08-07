import type { Locale } from '@puntiro/ui';

export type Maturity = 'draft' | 'beta' | 'stable' | 'deprecated';

export interface Documentation {
  overview: string;
  anatomy: string[];
  variants: string[];
  states: string[];
  behavior: string[];
  content: string[];
  accessibility: string[];
  usage: string[];
  do: string[];
  dont: string[];
  changelog: string[];
}

export type LocalizedDocumentation = Record<Locale, Documentation>;

export interface ComponentDocumentationOptions {
  title: string;
  maturity: Maturity;
  documentation: LocalizedDocumentation;
}
