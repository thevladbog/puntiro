import { defineDocumentation } from '../../docs/defineDocumentation';

export const placeCounterDocumentation = defineDocumentation({
  ru: {
    overview: 'PlaceCounter вводит фактическое количество итоговых мест и постоянно показывает текущий счётчик этикеток.',
    anatomy: ['Локализованный счётчик current из total', 'NumberInput с крупными действиями уменьшения и увеличения'],
    variants: ['Диапазон задаётся minValue и maxValue без отдельных визуальных вариантов.'],
    states: ['Значения 1, 10 и 100 сохраняют одинаковую иерархию.', 'Disabled блокирует ввод и обе stepper-кнопки.'],
    behavior: ['Любое изменение передаётся через onChange; pattern остаётся контролируемым.', 'Pattern не сохраняет количество и не запускает печать.'],
    content: ['В русском используется форма «1 из 5», в английском — «1 of 5».', 'Счётчик набран IBM Plex Mono для быстрого сопоставления чисел.'],
    accessibility: ['NumberInput сохраняет роль spinbutton, доступное имя и клавиатурное управление.', 'В touch mode цели не меньше 64 px, в standard mode — 44 px.'],
    usage: ['Используйте после выбора отгрузки, когда оператор пересчитал короба.', 'Корень отмечен data-kiosk-working-region для проверки будущей композиции.'],
    do: ['Оставляйте актуальный total видимым рядом с вводом.', 'Обновляйте value снаружи после onChange.'],
    dont: ['Не выводите товарный состав.', 'Не добавляйте persistence или печать внутрь pattern.'],
    changelog: ['Beta: первая публичная версия PlaceCounter; требуется ручная проверка в перчатках.']
  },
  en: {
    overview: 'PlaceCounter captures the actual final package count and keeps the current label counter visible.',
    anatomy: ['Localized current-of-total counter', 'NumberInput with large decrement and increment actions'],
    variants: ['The range is set by minValue and maxValue without separate visual variants.'],
    states: ['Values 1, 10, and 100 preserve the same hierarchy.', 'Disabled blocks the input and both stepper buttons.'],
    behavior: ['Every change is emitted through onChange; the pattern stays controlled.', 'The pattern does not persist the count or start printing.'],
    content: ['Russian uses “1 из 5”; English uses “1 of 5”.', 'The counter uses IBM Plex Mono for fast numeric comparison.'],
    accessibility: ['NumberInput retains its spinbutton role, accessible name, and keyboard support.', 'Targets are at least 64 px in touch mode and 44 px in standard mode.'],
    usage: ['Use after shipment selection when the operator has counted the boxes.', 'The root has data-kiosk-working-region for future composition checks.'],
    do: ['Keep the current total visible beside the input.', 'Update value externally after onChange.'],
    dont: ['Do not show item contents.', 'Do not add persistence or printing to the pattern.'],
    changelog: ['Beta: first public PlaceCounter; a manual gloved-touch review remains required.']
  }
});
