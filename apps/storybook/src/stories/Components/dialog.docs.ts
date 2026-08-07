import { defineDocumentation } from '../../docs/defineDocumentation';

export const dialogDocumentation = defineDocumentation({
  ru: {
    overview: 'Dialog изолирует одно решение и возвращает фокус к его trigger после закрытия.',
    anatomy: ['Trigger', 'Overlay', 'Modal surface', 'Title', 'Необязательное description', 'Content', 'Action buttons'],
    variants: ['Варианты действий определяются Button: secondary или danger.'],
    states: ['Подтверждение, destructive, busy non-dismissible, длинный RU и EN контент.'],
    behavior: ['По умолчанию overlay click и Escape закрывают окно.', 'isDismissible=false блокирует оба способа закрытия.', 'Каждому action передаётся close callback; пустой actions поддерживает busy state.'],
    content: ['title формулирует решение.', 'description сообщает значимый риск или контекст.', 'children содержат необходимую оператору информацию.'],
    accessibility: ['Используются dialog role, title и description associations, focus trap и восстановление фокуса.', 'Keyboard focus заметен на action buttons.', 'Контент переносится в 1280 × 800: Dialog не вводит page-level kiosk assumptions.'],
    usage: ['Используйте для решения, а не для навигации.', 'Показывайте destructive действие явной danger-кнопкой.'],
    do: ['Давайте trigger, title и действиям ясные текстовые имена.'],
    dont: ['Не закрывайте busy dialog кликом по overlay или Escape.', 'Не передавайте className или style для переопределения контракта.'],
    changelog: ['Beta: первая публичная версия; требуется ручная проверка touch-дисплея и экранного диктора.']
  },
  en: {
    overview: 'Dialog isolates one decision and restores focus to its trigger after closing.',
    anatomy: ['Trigger', 'Overlay', 'Modal surface', 'Title', 'Optional description', 'Content', 'Action buttons'],
    variants: ['Action variants are provided by Button: secondary or danger.'],
    states: ['Confirmation, destructive, busy non-dismissible, and long RU and EN content.'],
    behavior: ['Overlay click and Escape close by default.', 'isDismissible=false blocks both dismissal paths.', 'Each action receives a close callback; empty actions support a busy state.'],
    content: ['title states the decision.', 'description communicates material risk or context.', 'children contain the information needed by the operator.'],
    accessibility: ['Dialog role, title and description associations, focus trap, and focus restoration are preserved.', 'Keyboard focus is visible on action buttons.', 'Content wraps at 1280 × 800: Dialog does not make page-level kiosk assumptions.'],
    usage: ['Use for a decision, not navigation.', 'Use an explicit danger button for destructive action.'],
    do: ['Give the trigger, title, and actions clear text names.'],
    dont: ['Do not close a busy dialog with overlay click or Escape.', 'Do not pass className or style to override the contract.'],
    changelog: ['Beta: first public version; manual touch-display and screen-reader review remains required.']
  }
});
