import { defineDocumentation } from '../../docs/defineDocumentation';

export const unknownPrintResultDocumentation = defineDocumentation({
  ru: {
    overview: 'UnknownPrintResult применяется, когда система не может доказать, были ли напечатаны все этикетки комплекта.',
    anatomy: ['Warning heading', 'Неизменяемое объяснение неопределённости', 'Номер отгрузки и количество мест', 'Три явных варианта решения'],
    variants: ['1 место', '100 мест', 'Длинный номер документа', 'RU и EN'],
    states: ['Ожидает решения оператора.', 'После первого выбора все действия блокируются от повторного нажатия.'],
    behavior: ['Каждое из трёх действий имеет отдельный callback.', 'Автоматическая повторная печать отсутствует.'],
    content: ['Текст не утверждает ни успех, ни ошибку печати.', 'Полный номер отгрузки и количество мест остаются видимыми.'],
    accessibility: ['Warning heading доступен как heading.', 'Pattern не использует role="alert" и не маскируется под обычную ошибку.'],
    usage: ['Показывайте только после потери достоверного результата печати.', 'Решение и дальнейший flow выполняет приложение.'],
    do: ['Давайте оператору повторить весь комплект, выбрать места или продолжить без перепечатки.', 'Сохраняйте варианты различимыми текстом.'],
    dont: ['Не выбирайте действие автоматически.', 'Не сообщайте, что печать точно завершилась или точно упала.'],
    changelog: ['Beta: первая публичная версия UnknownPrintResult; требуется ручной review критичного сценария.']
  },
  en: {
    overview: 'UnknownPrintResult is used when the system cannot prove whether every label in the set printed.',
    anatomy: ['Warning heading', 'Immutable uncertainty explanation', 'Shipment number and place count', 'Three explicit resolution choices'],
    variants: ['1 place', '100 places', 'Long document number', 'RU and EN'],
    states: ['Awaiting an operator decision.', 'After the first choice, every action is disabled against repeated input.'],
    behavior: ['Each of the three actions has its own callback.', 'No automatic reprint occurs.'],
    content: ['Copy claims neither print success nor print failure.', 'The full shipment number and place count stay visible.'],
    accessibility: ['The warning title is exposed as a heading.', 'The pattern does not use role="alert" or masquerade as an ordinary error.'],
    usage: ['Show only after losing a trustworthy print outcome.', 'The application owns resolution and the next flow.'],
    do: ['Let the operator retry all, select places, or continue without reprint.', 'Keep choices distinguishable with text.'],
    dont: ['Do not choose automatically.', 'Do not claim that printing definitely completed or definitely failed.'],
    changelog: ['Beta: first public UnknownPrintResult; manual review of this critical scenario is still required.']
  }
});
