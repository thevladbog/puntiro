import { defineDocumentation } from '../../docs/defineDocumentation';

export const printProgressDocumentation = defineDocumentation({
  ru: {
    overview: 'PrintProgress показывает дискретный ход печати комплекта этикеток для одной отгрузки.',
    anatomy: ['Полный номер отгрузки', 'Имя выбранного принтера', 'Текстовый статус печати', 'Счётчик и ProgressIndicator'],
    variants: ['Текущий прогресс от 0 до общего количества мест', 'Длинный номер документа', 'RU и EN'],
    states: ['В процессе печати', 'Комплект завершён', 'Нормализованное граничное значение прогресса'],
    behavior: ['Pattern только отображает переданные значения.', 'Команды печати и опрос оборудования выполняются приложением.'],
    content: ['Номер и имя принтера показываются полностью.', 'Счётчик использует формат «3 из 5» и локаль провайдера.'],
    accessibility: ['Нативный progressbar сообщает текущее и максимальное значения.', 'Корень отмечен data-kiosk-working-region.'],
    usage: ['Оставляйте PrintProgress видимым до подтверждённого результата печати.', 'Передавайте фактический дискретный прогресс снаружи.'],
    do: ['Показывайте номер задания и принтер рядом с прогрессом.', 'Отличайте подтверждённое завершение от UnknownPrintResult.'],
    dont: ['Не запускайте печать из pattern.', 'Не показывайте неопределённый декоративный индикатор вместо счётчика мест.'],
    changelog: ['Beta: первая публичная версия PrintProgress; требуется ручная проверка с реальным принтером на следующем этапе.']
  },
  en: {
    overview: 'PrintProgress shows discrete progress while a label set is being printed for one shipment.',
    anatomy: ['Full shipment number', 'Selected printer name', 'Text print status', 'Counter and ProgressIndicator'],
    variants: ['Current progress from zero to the total place count', 'Long document number', 'RU and EN'],
    states: ['Printing in progress', 'Set completed', 'Normalized progress boundary'],
    behavior: ['The pattern only presents supplied values.', 'The application owns print commands and hardware polling.'],
    content: ['Show the shipment number and printer name in full.', 'The counter uses the “3 of 5” format and provider locale.'],
    accessibility: ['A native progressbar announces current and maximum values.', 'The root has data-kiosk-working-region.'],
    usage: ['Keep PrintProgress visible until the print outcome is confirmed.', 'Supply actual discrete progress from outside the pattern.'],
    do: ['Keep job identity and printer beside progress.', 'Distinguish confirmed completion from UnknownPrintResult.'],
    dont: ['Do not initiate printing inside the pattern.', 'Do not replace the place counter with an indeterminate decorative loader.'],
    changelog: ['Beta: first public PrintProgress; manual real-printer review is deferred to the hardware stage.']
  }
});
