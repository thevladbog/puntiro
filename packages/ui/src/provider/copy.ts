import type { Locale } from './types';

export const russianCopy = {
  'action.print': 'Напечатать',
  'action.cancel': 'Отменить',
  'action.retry': 'Повторить',
  'action.close': 'Закрыть',
  'action.selectPrinter': 'Выбрать принтер',
  'action.increment': 'Увеличить',
  'action.decrement': 'Уменьшить',
  'status.loading': 'Загрузка',
  'status.offline': 'Нет подключения',
  'status.error': 'Ошибка',
  'status.unknownResult': 'Результат печати неизвестен',
  'label.places': 'Места',
  'label.placeCounter': 'Мест: {current} из {total}'
} as const;

export type TranslationKey = keyof typeof russianCopy;

export const englishCopy = {
  'action.print': 'Print',
  'action.cancel': 'Cancel',
  'action.retry': 'Retry',
  'action.close': 'Close',
  'action.selectPrinter': 'Select printer',
  'action.increment': 'Increase',
  'action.decrement': 'Decrease',
  'status.loading': 'Loading',
  'status.offline': 'Offline',
  'status.error': 'Error',
  'status.unknownResult': 'Print result unknown',
  'label.places': 'Places',
  'label.placeCounter': 'Places: {current} of {total}'
} satisfies Record<TranslationKey, string>;

export const copy: Record<Locale, Record<TranslationKey, string>> = {
  ru: russianCopy,
  en: englishCopy
};
