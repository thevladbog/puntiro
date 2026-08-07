import { Button, StatusBadge, usePuntiro } from '@puntiro/ui';
import type { Locale } from '@puntiro/ui';

export function StartPreview({ locale }: { locale?: Locale }) {
  const { locale: providerLocale } = usePuntiro();
  const activeLocale = locale ?? providerLocale;
  const isRussian = activeLocale === 'ru';

  return <div className="puntiro-start__preview-controls">
    <Button iconBefore="printer">{isRussian ? 'Напечатать' : 'Print'}</Button>
    <StatusBadge tone="success" label={isRussian ? 'Готов к печати' : 'Ready to print'} />
  </div>;
}
