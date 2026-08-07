import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { InlineMessage } from './InlineMessage';

describe('InlineMessage', () => {
  it('uses an alert only for a danger message', () => {
    const danger = renderToStaticMarkup(<InlineMessage tone="danger" title="Печать остановлена">Проверьте принтер.</InlineMessage>);
    const informational = renderToStaticMarkup(<InlineMessage tone="info" title="Принтер подключен" />);

    expect(danger).toContain('role="alert"');
    expect(informational).toContain('role="status"');
    expect(informational).not.toContain('role="alert"');
  });
});
