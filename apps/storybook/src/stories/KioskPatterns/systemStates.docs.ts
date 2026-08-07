import { defineDocumentation } from '../../docs/defineDocumentation';

export const systemStatesDocumentation = defineDocumentation({
  ru: {
    overview: 'EmptyState, LoadingState и ErrorState задают единый язык обычных операционных состояний киоска.',
    anatomy: ['Явный заголовок или label', 'Краткое описание', 'Опциональное primary-действие восстановления'],
    variants: ['Пустая очередь или архив', 'Загрузка', 'Восстанавливаемая ошибка', 'Недоступный принтер'],
    states: ['EmptyState может не иметь действия.', 'LoadingState сообщает role="status".', 'ErrorState сообщает обычную ошибку через role="alert".'],
    behavior: ['Callbacks вызываются только по явному действию оператора.', 'Patterns не загружают данные и не повторяют запросы автоматически.'],
    content: ['Сообщайте, что произошло и что оператор может сделать.', 'Передавайте локализованный пользовательский текст через props.'],
    accessibility: ['Все корни отмечены data-kiosk-working-region.', 'Primary-действия используют Button и соблюдают текущий interaction mode.'],
    usage: ['Используйте эти patterns для обычных empty/loading/error состояний.', 'Для неопределённого результата печати используйте отдельный UnknownPrintResult.'],
    do: ['Предлагайте одно понятное восстановление, когда оно доступно.', 'Сохраняйте текст коротким и конкретным.'],
    dont: ['Не сводите UnknownPrintResult к ErrorState.', 'Не запускайте retry без нажатия оператора.'],
    changelog: ['Beta: первая публичная версия операционных system states.']
  },
  en: {
    overview: 'EmptyState, LoadingState, and ErrorState provide one language for ordinary kiosk operational states.',
    anatomy: ['Explicit heading or label', 'Concise description', 'Optional primary recovery action'],
    variants: ['Empty queue or archive', 'Loading', 'Recoverable error', 'Unavailable printer'],
    states: ['EmptyState may omit its action.', 'LoadingState announces role="status".', 'ErrorState announces an ordinary error with role="alert".'],
    behavior: ['Callbacks run only after an explicit operator action.', 'Patterns do not load data or retry requests automatically.'],
    content: ['Say what happened and what the operator can do.', 'Supply localized user-facing text through props.'],
    accessibility: ['Every root has data-kiosk-working-region.', 'Primary actions use Button and respect the active interaction mode.'],
    usage: ['Use these patterns for ordinary empty, loading, and error states.', 'Use the separate UnknownPrintResult for an uncertain print outcome.'],
    do: ['Offer one clear recovery when available.', 'Keep text concise and concrete.'],
    dont: ['Do not reduce UnknownPrintResult to ErrorState.', 'Do not retry without an operator press.'],
    changelog: ['Beta: first public operational system states.']
  }
});
