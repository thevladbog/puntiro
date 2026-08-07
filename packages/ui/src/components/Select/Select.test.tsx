import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { Select } from './Select';

describe('Select', () => {
  it('associates its text label and invalid guidance with the trigger', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider><Select label="Язык принтера" items={[{ id: 'zpl', label: 'ZPL' }]} description="Выберите язык." errorMessage="Выберите вариант." /></PuntiroProvider>
    );

    expect(html).toContain('Язык принтера');
    expect(html).toContain('data-invalid="true"');
    expect(html).toContain('Выберите язык.');
    expect(html).toContain('Выберите вариант.');
  });

  it('honors an uncontrolled selected string id', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider><Select label="Язык принтера" defaultSelectedId="epl" items={[{ id: 'epl', label: 'EPL', isDisabled: true }]} /></PuntiroProvider>
    );

    expect(html).toContain('EPL');
    expect(html).toContain('selected=""');
  });
});
