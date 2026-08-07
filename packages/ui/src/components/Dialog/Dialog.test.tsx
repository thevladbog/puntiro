import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { Button } from '../Button/Button';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { Dialog } from './Dialog';

describe('Dialog', () => {
  it('keeps the title, description, content, and actions in its public composition', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider><Dialog trigger={<Button>Открыть</Button>} title="Подтвердить?" description="Это действие важно." actions={[{ id: 'confirm', label: 'Подтвердить', variant: 'danger', onPress: () => undefined }]}>
        <p>Проверьте данные.</p>
      </Dialog></PuntiroProvider>
    );

    expect(html).toContain('Открыть');
    expect(html).not.toContain('undefined');
  });

  it('supports a non-dismissible busy composition without inventing an action', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider><Dialog trigger={<Button>Открыть</Button>} title="Выполняется" actions={[]} isDismissible={false}><p>Подождите.</p></Dialog></PuntiroProvider>
    );

    expect(html).toContain('Открыть');
    expect(html).not.toContain('undefined');
  });
});
