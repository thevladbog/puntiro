import { defineDocumentation } from '../../docs/defineDocumentation';

export const connectivityBannerDocumentation = defineDocumentation({
  ru: {
    overview: 'ConnectivityBanner показывает текущее подключение и безопасную возможность продолжить работу офлайн.',
    anatomy: ['Иконка состояния', 'Текст подключения', 'Время последней синхронизации', 'Оставшееся offline-разрешение при необходимости'],
    variants: ['online — подключение активно', 'offlineAllowed — работа офлайн еще разрешена', 'offlineExpired — offline-лимит истек'],
    states: ['Каждое состояние содержит текст и иконку; отдельная цветная точка не используется.', 'Нецелые секунды и секунды меньше нуля нормализуются вниз до целых неотрицательных секунд, а оставшееся время округляется вверх до полной минуты.'],
    behavior: ['Корень сообщает role="status" и не становится действием.', 'Pattern не синхронизирует данные и не принимает решение о доступе; он только отображает переданное состояние.'],
    content: ['Время последней синхронизации форматируется через Intl.DateTimeFormat для locale провайдера в UTC.', 'Для offlineAllowed передавайте оставшиеся секунды; 61 секунда сообщается как «ещё 2 мин.».'],
    accessibility: ['Текст, иконка и тон вместе передают смысл; цвет не является единственным сигналом.', 'Корень отмечен data-kiosk-working-region для проверки kiosk-frame.'],
    usage: ['Держите banner видимым в kiosk-композиции.', 'Обновляйте входные данные снаружи pattern.'],
    do: ['Показывайте последнюю синхронизацию во всех состояниях.', 'Явно сообщайте, можно ли продолжать работу офлайн.'],
    dont: ['Не заменяйте статус зеленой точкой.', 'Не добавляйте сетевой polling или persistence.'],
    changelog: ['Beta: первая публичная версия ConnectivityBanner; требуется ручная kiosk-проверка.']
  },
  en: {
    overview: 'ConnectivityBanner shows current connectivity and whether it is safe to continue working offline.',
    anatomy: ['State icon', 'Connectivity text', 'Last-sync time', 'Remaining offline allowance when relevant'],
    variants: ['online — connection is active', 'offlineAllowed — offline work is still allowed', 'offlineExpired — offline allowance has expired'],
    states: ['Every state has text and an icon; a standalone colored dot is not used.', 'Fractional seconds and negative values are normalized down to whole non-negative seconds, then remaining time rounds up to a full minute.'],
    behavior: ['The root announces role="status" and is not an action.', 'The pattern does not synchronize or decide access; it only displays supplied state.'],
    content: ['Last-sync time uses Intl.DateTimeFormat for the provider locale in UTC.', 'For offlineAllowed pass remaining seconds; 61 seconds is announced as 2 min remaining.'],
    accessibility: ['Text, icon, and tone carry meaning together; color is never the sole signal.', 'The root has data-kiosk-working-region for kiosk-frame checks.'],
    usage: ['Keep the banner visible in a kiosk composition.', 'Refresh inputs outside the pattern.'],
    do: ['Show last synchronization in every state.', 'State explicitly whether offline work can continue.'],
    dont: ['Do not replace status text with a green dot.', 'Do not add network polling or persistence.'],
    changelog: ['Beta: first public ConnectivityBanner; a manual kiosk review remains required.']
  }
});
