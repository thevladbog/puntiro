export interface ArticleCopy {
  title: string;
  lead: string;
  sections: readonly { title: string; body: string }[];
}

export const ruContent = {
  start: {
    title: 'Puntiro Design System Platform',
    lead: 'Точное место. Ясная последовательность. Уверенная передача.',
    sections: [
      { title: 'Живая документация', body: 'Puntiro объединяет токены, компоненты, правила и проверяемые примеры для киоска и админки.' },
      { title: 'Рабочий контекст', body: 'Базовый киоск работает в landscape 1280 × 800. Критичные действия остаются крупными, понятными и доступны без hover.' },
    ],
  },
  'foundation.brand': { title: 'Бренд', lead: 'Бренд Puntiro обозначает точное завершение отгрузки.', sections: [{ title: 'P-Sequence', body: 'Registration stem, упорядоченные модули и конечная оранжевая точка показывают последовательность без декоративного шума.' }] },
  'foundation.color': { title: 'Цвет', lead: 'Цвет ведет действие и сохраняет рабочий контраст.', sections: [{ title: 'Один сигнал', body: 'Handoff Orange обозначает primary action, выбор или текущий этап. Он не становится большой декоративной заливкой.' }] },
  'foundation.typography': { title: 'Типографика', lead: 'Onest делает документацию и интерфейс читаемыми.', sections: [{ title: 'Технические данные', body: 'IBM Plex Mono используется для номеров отгрузок, счетчиков, кодов и технических параметров.' }] },
  'foundation.spacing': { title: 'Отступы', lead: 'Система строится на сетке 8 px.', sections: [{ title: 'Ритм', body: 'Используйте утвержденные интервалы, чтобы сохранять порядок между метками, controls и surfaces.' }] },
  'foundation.sizing': { title: 'Размеры и touch targets', lead: 'Размер элемента поддерживает уверенную работу пальцем и в перчатках.', sections: [{ title: 'Режимы', body: 'В touch режиме минимальная цель 64 px, комфортная primary action 72 px. В standard режиме минимум 44 px.' }] },
  'foundation.surface': { title: 'Радиус, рамки и поверхности', lead: 'Поверхности отделяются рамкой и пространством.', sections: [{ title: 'Форма', body: '4 px применяется для служебных labels, 12 px для controls и 24 px для крупных surfaces. Glassmorphism исключен.' }] },
  'foundation.motion': { title: 'Motion', lead: 'Motion подтверждает действие, а не украшает страницу.', sections: [{ title: 'Длительности', body: '120 ms используется для hover, press и focus. 180 ms используется для подтверждения выбора и завершения действия.' }] },
  'foundation.focus': { title: 'Focus', lead: 'Keyboard focus делает текущую точку взаимодействия очевидной.', sections: [{ title: 'Порядок', body: 'Компоненты сохраняют логичный focus order, видимое кольцо фокуса и корректные accessible names.' }] },
  'foundation.accessibility': { title: 'Доступность', lead: 'Touch first не отменяет полную клавиатурную доступность.', sections: [{ title: 'Состояния', body: 'Loading, empty, offline, invalid, failed и unknown являются частью продукта и получают текстовое объяснение.' }] },
  'foundation.tokens': { title: 'Каталог токенов', lead: 'Токены остаются единственным источником утвержденных значений.', sections: [{ title: 'Три уровня', body: 'Reference tokens задают шкалы, semantic tokens задают роли, component tokens существуют только для локальной вариативности.' }] },
} as const satisfies Record<string, ArticleCopy>;

export type ArticleId = keyof typeof ruContent;
