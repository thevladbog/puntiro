import { defineDocumentation } from '../../docs/defineDocumentation';

export const shipmentTaskCardDocumentation = defineDocumentation({
  ru: {
    overview: 'ShipmentTaskCard — крупная кнопка задания для выбора отгрузки или документа продажи в kiosk-очереди.',
    anatomy: ['Полный номер отгрузки или документа', 'Время получения и плановой отгрузки', 'StatusBadge с текстом и иконкой', 'Явная отметка выбранного задания'],
    variants: ['ready — задание готово к печати', 'updated — данные обновились после печати', 'attention — задание требует внимания'],
    states: ['Выбранное состояние передается через aria-pressed и текст «Выбрано», не только цветом.', 'Длинный номер переносится целиком без обрезки.'],
    behavior: ['Нажатие на всю карточку один раз вызывает onOpen.', 'Карточка не загружает данные, не меняет маршрут и не обращается к принтеру.'],
    content: ['Передавайте полный номер; не сокращайте его многоточием.', 'Время форматируется через Intl.DateTimeFormat для locale провайдера в UTC, чтобы примеры и проверки были детерминированными.'],
    accessibility: ['Корень — нативная доступная button с полным номером в видимом тексте и accessible name.', 'Статус всегда содержит иконку и текст через StatusBadge; его текст входит в aria-description карточки без вложенной live-region.', 'Touch mode сохраняет цель не менее 64 px, standard mode — не менее 44 px.'],
    usage: ['Размещайте pattern в рабочей области kiosk-композиции; его корень отмечен data-kiosk-working-region.', 'Используйте как один очевидный переход к следующему шагу, а не как компактную таблицу.'],
    do: ['Показывайте полученное время и плановую отгрузку, когда она известна.', 'Сообщайте обновление после печати отдельным status.'],
    dont: ['Не прячьте часть номера.', 'Не добавляйте в pattern маршрутизацию, API или печать.'],
    changelog: ['Beta: первая публичная версия ShipmentTaskCard; требуется ручная проверка на kiosk 1280 × 800 и в перчатках.']
  },
  en: {
    overview: 'ShipmentTaskCard is a large task button for choosing a shipment or sales document in a kiosk queue.',
    anatomy: ['Full shipment or document number', 'Received and planned-shipment times', 'StatusBadge with text and icon', 'Explicit selected-task marker'],
    variants: ['ready — task is ready to print', 'updated — data changed after printing', 'attention — task needs attention'],
    states: ['Selected state is conveyed by aria-pressed and a Selected label, not color alone.', 'A long number wraps in full without truncation.'],
    behavior: ['One press anywhere on the card invokes onOpen once.', 'The card does not load data, change routes, or access a printer.'],
    content: ['Pass the full number; never abbreviate it with an ellipsis.', 'Times use Intl.DateTimeFormat for the provider locale in UTC, keeping examples and checks deterministic.'],
    accessibility: ['The root is a native accessible button with the full number in visible text and its accessible name.', 'Each status has text and an icon through StatusBadge; its text is in the card aria-description without a nested live region.', 'Touch mode retains a 64 px target; standard mode retains 44 px.'],
    usage: ['Place the pattern in a kiosk-composition working area; its root has data-kiosk-working-region.', 'Use it as one clear step forward, not as a compact table.'],
    do: ['Show received time and planned shipment when it is known.', 'Expose an after-print update through a distinct status.'],
    dont: ['Do not hide part of a number.', 'Do not add routing, API, or print I/O to the pattern.'],
    changelog: ['Beta: first public ShipmentTaskCard; a 1280 × 800 kiosk and gloved-touch review is still required.']
  }
});
