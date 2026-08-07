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
  });
});
