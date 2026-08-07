import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { StatusBadge } from './StatusBadge';
import type { FeedbackTone, StatusBadgeProps } from './StatusBadge.types';

type Equal<Left, Right> = (<Value>() => Value extends Left ? 1 : 2) extends (<Value>() => Value extends Right ? 1 : 2) ? true : false;

const statusBadgePropsAreExact: Equal<StatusBadgeProps, { label: string; tone?: FeedbackTone }> = true;
void statusBadgePropsAreExact;

describe('StatusBadge', () => {
  it('announces a labelled success state with a decorative approved icon', () => {
    const html = renderToStaticMarkup(<StatusBadge tone="success" label="Готов к печати" />);

    expect(html).toContain('role="status"');
    expect(html).toContain('Готов к печати');
    expect(html).toContain('<svg');
    expect(html).toContain('aria-hidden="true"');
  });
});
