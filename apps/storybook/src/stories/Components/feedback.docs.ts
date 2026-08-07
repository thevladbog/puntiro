import { defineDocumentation } from '../../docs/defineDocumentation';

export const surfaceDocumentation = defineDocumentation({
  ru: {
    overview: 'Surface задает спокойную рамку для содержимого без клика, выбора или скрытого поведения.',
    anatomy: ['Семантический контейнер', 'Содержимое', 'Вариант рамки', 'Отступ'],
    variants: ['plain для нейтрального блока', 'raised для отделенной области', 'outlined для явной рабочей границы'],
    states: ['Обычное содержимое, компактный и комфортный отступ, section или article.'],
    behavior: ['Surface не становится интерактивной.', 'Тип контейнера выбирается из закрытого набора.'],
    content: ['Длинный текст остается видимым внутри поверхности.'],
    accessibility: ['Используйте article или section только когда семантика содержимого это оправдывает.', 'Не добавляйте обработчики нажатия к Surface.'],
    usage: ['Отделяйте рабочие данные рамкой и пространством, а не декоративной тенью.'],
    do: ['Выбирайте один из утвержденных вариантов и отступов.'],
    dont: ['Не используйте Surface как кнопку или карточку выбора.'],
    changelog: ['Beta: первая публичная версия Surface.']
  },
  en: {
    overview: 'Surface provides a calm boundary for content without click, selection, or hidden behavior.',
    anatomy: ['Semantic container', 'Content', 'Border variant', 'Padding'],
    variants: ['plain for a neutral block', 'raised for a separated area', 'outlined for an explicit working boundary'],
    states: ['Default content, compact and comfortable padding, section or article.'],
    behavior: ['Surface is not interactive.', 'The container element comes from a closed set.'],
    content: ['Long text remains visible inside the surface.'],
    accessibility: ['Use article or section only when the content semantics justify it.', 'Do not add press handlers to Surface.'],
    usage: ['Separate work data with border and space instead of a decorative shadow.'],
    do: ['Choose one approved variant and padding value.'],
    dont: ['Do not use Surface as a button or selectable card.'],
    changelog: ['Beta: first public Surface release.']
  }
});

export const statusBadgeDocumentation = defineDocumentation({
  ru: {
    overview: 'StatusBadge показывает короткое состояние текстом и утвержденной иконкой.',
    anatomy: ['Иконка статуса', 'Текстовая метка', 'Граница тона'],
    variants: ['neutral, info, success, warning и danger.'],
    states: ['Каждый тон остается различимым по иконке и тексту, включая монохромный контекст.'],
    behavior: ['Компонент сообщает status.', 'Иконка всегда рендерится рядом с меткой.'],
    content: ['Пишите наблюдаемое состояние: Готов к печати, Нужна бумага, Печать остановлена.'],
    accessibility: ['Не полагайтесь только на цвет.', 'Декоративная иконка дополняет текстовую метку.'],
    usage: ['Показывайте один короткий статус в видимой зоне операции.'],
    do: ['Сочетайте утвержденный тон с точной меткой.'],
    dont: ['Не заменяйте текст состояния одним цветом или иконкой.'],
    changelog: ['Beta: первая публичная версия StatusBadge.']
  },
  en: {
    overview: 'StatusBadge shows a short state with text and an approved icon.',
    anatomy: ['Status icon', 'Text label', 'Tone border'],
    variants: ['neutral, info, success, warning, and danger.'],
    states: ['Every tone remains distinguishable by icon and text, including a monochrome context.'],
    behavior: ['The component announces status.', 'An icon always renders beside the label.'],
    content: ['Write an observable state: Ready to print, Paper needed, Printing stopped.'],
    accessibility: ['Do not rely on color alone.', 'The decorative icon supplements the text label.'],
    usage: ['Keep one short status in the operation’s visible area.'],
    do: ['Pair an approved tone with a precise label.'],
    dont: ['Do not replace a state label with color or an icon alone.'],
    changelog: ['Beta: first public StatusBadge release.']
  }
});

export const inlineMessageDocumentation = defineDocumentation({
  ru: {
    overview: 'InlineMessage объясняет локальное состояние и следующий шаг без модального прерывания.',
    anatomy: ['Иконка тона', 'Заголовок', 'Необязательное пояснение'],
    variants: ['neutral, info, success, warning и danger.'],
    states: ['Danger — срочное alert-сообщение; остальные тона сообщаются как status.'],
    behavior: ['Текст и иконка дополняют границу тона.', 'Длинное пояснение остается полностью читаемым.'],
    content: ['Начинайте заголовок с состояния, затем коротко укажите следующий шаг.'],
    accessibility: ['role="alert" используется только для danger.', 'Не полагайтесь на цвет для смысла.'],
    usage: ['Размещайте сообщение рядом с затронутым полем или операцией.'],
    do: ['Сообщайте, что произошло и что сделать дальше.'],
    dont: ['Не используйте alert для обычной справочной информации.'],
    changelog: ['Beta: первая публичная версия InlineMessage.']
  },
  en: {
    overview: 'InlineMessage explains a local state and next step without a modal interruption.',
    anatomy: ['Tone icon', 'Title', 'Optional explanation'],
    variants: ['neutral, info, success, warning, and danger.'],
    states: ['Danger is an urgent alert; other tones announce as status.'],
    behavior: ['Text and icon supplement the tone border.', 'Long explanation remains fully readable.'],
    content: ['Start the title with the state, then state the next step briefly.'],
    accessibility: ['role="alert" is used only for danger.', 'Do not rely on color for meaning.'],
    usage: ['Place the message beside the affected field or operation.'],
    do: ['Say what happened and what to do next.'],
    dont: ['Do not use an alert for routine reference information.'],
    changelog: ['Beta: first public InlineMessage release.']
  }
});

export const progressIndicatorDocumentation = defineDocumentation({
  ru: {
    overview: 'ProgressIndicator показывает содержательный дискретный ход операции нативным progress-элементом.',
    anatomy: ['Текстовая метка', 'Нативная шкала', 'Счетчик IBM Plex Mono'],
    variants: ['Нулевой, частичный и завершенный прогресс; произвольный текст счетчика.'],
    states: ['Некорректные входные диапазоны безопасно приводятся к допустимым значениям.'],
    behavior: ['value ограничивается диапазоном от 0 до max.', 'Шкала получает label и aria-значения.'],
    content: ['Называйте текущую операцию: Печать этикеток, Загрузка кодов.'],
    accessibility: ['Используется нативная семантика progressbar.', 'Счетчик дает числовой контекст без анимации.'],
    usage: ['Показывайте только измеримый ход операции.'],
    do: ['Передавайте точные value, max и label.'],
    dont: ['Не заменяйте ход декоративной бесконечной анимацией.'],
    changelog: ['Beta: первая публичная версия ProgressIndicator.']
  },
  en: {
    overview: 'ProgressIndicator shows meaningful discrete operation progress through a native progress element.',
    anatomy: ['Text label', 'Native track', 'IBM Plex Mono counter'],
    variants: ['Zero, partial, and complete progress; custom counter text.'],
    states: ['Invalid input ranges are safely normalized to valid values.'],
    behavior: ['value is constrained between 0 and max.', 'The track receives a label and ARIA values.'],
    content: ['Name the current operation: Printing labels, Loading codes.'],
    accessibility: ['Native progressbar semantics are used.', 'The counter gives numeric context without animation.'],
    usage: ['Show only measurable operation progress.'],
    do: ['Pass accurate value, max, and label values.'],
    dont: ['Do not replace progress with a decorative infinite animation.'],
    changelog: ['Beta: first public ProgressIndicator release.']
  }
});
