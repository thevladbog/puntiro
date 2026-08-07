import type {
  Documentation,
  LocalizedDocumentation
} from './types';

const listFields = [
  'anatomy',
  'variants',
  'states',
  'behavior',
  'content',
  'accessibility',
  'usage',
  'do',
  'dont',
  'changelog'
] as const;

function requireNonEmptyString(value: unknown, path: string): asserts value is string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new Error(`${path} must be a non-empty string`);
  }
}

function requireNonEmptyList(value: unknown, path: string): asserts value is string[] {
  if (!Array.isArray(value) || value.length === 0) {
    throw new Error(`${path} must contain at least one entry`);
  }

  value.forEach((entry, index) => requireNonEmptyString(entry, `${path}[${index}]`));
}

function validateDocumentation(locale: string, documentation: Documentation): void {
  requireNonEmptyString(documentation.overview, `${locale}.overview`);

  for (const field of listFields) {
    requireNonEmptyList(documentation[field], `${locale}.${field}`);
  }
}

export function defineDocumentation(documentation: LocalizedDocumentation): LocalizedDocumentation {
  validateDocumentation('ru', documentation.ru);
  validateDocumentation('en', documentation.en);
  return documentation;
}
