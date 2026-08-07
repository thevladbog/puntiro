import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';

const stylesheet = readFileSync(
  new URL('../../../../../packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.module.css', import.meta.url),
  'utf8'
);

it('keeps semantic native hover and pressed style contracts for shipment cards', () => {
  expect(stylesheet).toMatch(
    /\.root:hover\s*\{\s*background: var\(--puntiro-semantic-color-surface-muted\);/
  );
  expect(stylesheet).toMatch(
    /\.root:active\s*\{\s*border-color: var\(--puntiro-semantic-color-action-primary\);/
  );
  expect(stylesheet).not.toContain('--puntiro-reference-');
});
