import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { NumberInput } from './NumberInput';

describe('NumberInput', () => {
  it('rejects an inverted range instead of silently changing an explicit configuration', () => {
    expect(() => renderToStaticMarkup(
      <PuntiroProvider><NumberInput label="Количество мест" minValue={100} maxValue={1} /></PuntiroProvider>
    )).toThrow('minValue must be less than or equal to maxValue');
  });

  it('exposes bounded spinbutton semantics while React Aria handles locale parsing', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider><NumberInput label="Количество мест" defaultValue={2} /></PuntiroProvider>
    );

    expect(html).toContain('role="spinbutton"');
    expect(html).toContain('aria-valuemin="1"');
    expect(html).toContain('aria-valuemax="100"');
    expect(html).toContain('aria-valuenow="2"');
    expect(html).toContain('aria-valuetext="2"');
    expect(html).toContain('aria-roledescription="Числовое поле"');
  });

  it.each([
    ['minValue', Number.NaN, 'minValue must be finite'],
    ['maxValue', Number.POSITIVE_INFINITY, 'maxValue must be finite'],
    ['value', Number.NaN, 'value must be finite'],
    ['defaultValue', Number.NEGATIVE_INFINITY, 'defaultValue must be finite'],
    ['step', 0, 'step must be finite and greater than zero'],
    ['step', -1, 'step must be finite and greater than zero'],
    ['step', Number.NaN, 'step must be finite and greater than zero'],
    ['step', Number.POSITIVE_INFINITY, 'step must be finite and greater than zero']
  ] as const)('rejects invalid %s configuration', (prop, value, message) => {
    expect(() => renderToStaticMarkup(
      <PuntiroProvider><NumberInput label="Количество мест" {...{ [prop]: value }} /></PuntiroProvider>
    )).toThrow(message);
  });
});
