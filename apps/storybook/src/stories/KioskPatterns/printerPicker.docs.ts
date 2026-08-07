import { defineDocumentation } from '../../docs/defineDocumentation';

export const printerPickerDocumentation = defineDocumentation({
  ru: {
    overview: 'PrinterPicker показывает подключённые label-принтеры крупным списком и позволяет выбрать только готовый принтер.',
    anatomy: ['Заголовок radiogroup', 'Полное имя очереди и язык ZPL или TSPL', 'Текстовый StatusBadge', 'Крупная radio-цель'],
    variants: ['Один или несколько принтеров', 'Смешанный список ZPL и TSPL', 'Пустой список'],
    states: ['ready доступен для выбора.', 'busy, offline и error видимы со статусом, но недоступны.'],
    behavior: ['Выбор ready-принтера вызывает onSelectionChange с его id.', 'Стрелки клавиатуры перемещают выбор между доступными radio options.'],
    content: ['Показывайте полное Windows queue name без многоточия.', 'Язык принтера всегда видим в верхнем регистре рядом с именем.'],
    accessibility: ['Используется единая radiogroup с доступным локализованным заголовком.', 'Статус передаётся текстом и иконкой, а недоступность — native disabled semantics.'],
    usage: ['Показывайте picker, когда подключено несколько label-принтеров.', 'Данные и выбранный id передаются приложением.'],
    do: ['Оставляйте busy/offline/error printers видимыми для диагностики.', 'Сохраняйте цели не меньше активного interaction mode.'],
    dont: ['Не используйте компактный dropdown для kiosk-выбора.', 'Не обнаруживайте устройства и не обращайтесь к драйверам внутри pattern.'],
    changelog: ['Beta: первая публичная версия PrinterPicker; требуется ручная проверка с Windows queues.']
  },
  en: {
    overview: 'PrinterPicker presents connected label printers as a large list and allows selection of ready printers only.',
    anatomy: ['Radiogroup heading', 'Full queue name and ZPL or TSPL language', 'Text StatusBadge', 'Large radio target'],
    variants: ['One or multiple printers', 'Mixed ZPL and TSPL list', 'Empty list'],
    states: ['ready is selectable.', 'busy, offline, and error remain visible with status but are disabled.'],
    behavior: ['Selecting a ready printer invokes onSelectionChange with its id.', 'Keyboard arrows move selection between enabled radio options.'],
    content: ['Show the complete Windows queue name without ellipsis.', 'Keep the printer language visible in uppercase beside the name.'],
    accessibility: ['One radiogroup provides a localized accessible heading.', 'Status uses text and an icon, while unavailable options use native disabled semantics.'],
    usage: ['Show the picker when multiple label printers are connected.', 'The application supplies data and the selected id.'],
    do: ['Keep busy, offline, and error printers visible for diagnosis.', 'Keep targets at least as large as the active interaction mode.'],
    dont: ['Do not use a compact dropdown for kiosk selection.', 'Do not discover devices or call drivers inside the pattern.'],
    changelog: ['Beta: first public PrinterPicker; a manual Windows queue review remains required.']
  }
});
