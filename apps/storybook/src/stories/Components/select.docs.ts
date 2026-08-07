import { defineDocumentation } from '../../docs/defineDocumentation';

export const selectDocumentation = defineDocumentation({
  ru: {
    overview: 'Select выбирает один универсальный вариант из короткого списка.',
    anatomy: ['Label', 'Кнопка-триггер', 'Текущее значение или placeholder', 'Список вариантов', 'Description или error message'],
    variants: ['Плотность задаётся PuntiroProvider: touch или standard.'],
    states: ['Placeholder, выбранное значение, disabled option, invalid и disabled.'],
    behavior: ['Поддерживает controlled selectedId и uncontrolled defaultSelectedId.', 'Стрелки и Enter выбирают вариант с клавиатуры.', 'Disabled option остаётся видимым, но не выбирается.'],
    content: ['label называет выбираемую сущность.', 'description объясняет выбор, errorMessage — способ исправить ошибку.'],
    accessibility: ['Label даёт триггеру доступное имя, description и error message связаны с ним.', 'Фокус и выбранный вариант имеют контрастные состояния.', 'В touch триггер и каждый вариант не меньше 64 px; в standard — 44 px.'],
    usage: ['Используйте для короткого универсального списка.', 'Для выбора принтера в киоске используйте PrinterPicker, а не компактный Select.'],
    do: ['Передавайте стабильные строковые id и точную label.'],
    dont: ['Не используйте Select для kiosk printer choice.', 'Не передавайте className или style для переопределения контракта.'],
    changelog: ['Beta: первая публичная версия; требуется ручная проверка touch-дисплея и экранного диктора.']
  },
  en: {
    overview: 'Select chooses one general-purpose option from a short list.',
    anatomy: ['Label', 'Trigger button', 'Current value or placeholder', 'Option list', 'Description or error message'],
    variants: ['PuntiroProvider sets touch or standard density.'],
    states: ['Placeholder, selected value, disabled option, invalid, and disabled.'],
    behavior: ['Supports controlled selectedId and uncontrolled defaultSelectedId.', 'Arrow keys and Enter select an option.', 'A disabled option remains visible but cannot be selected.'],
    content: ['label names the selected entity.', 'description explains the choice and errorMessage explains a correction.'],
    accessibility: ['The label gives the trigger its accessible name, and description and errorMessage are associated with it.', 'Focus and selected states remain contrast-safe.', 'The trigger and every option are at least 64 px in touch and 44 px in standard.'],
    usage: ['Use for a short, general-purpose list.', 'Use PrinterPicker, not Select, for kiosk printer choice.'],
    do: ['Provide stable string ids and a specific label.'],
    dont: ['Do not use Select for kiosk printer choice.', 'Do not pass className or style to override the contract.'],
    changelog: ['Beta: first public version; manual touch-display and screen-reader review remains required.']
  }
});
