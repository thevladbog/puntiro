# Puntiro Design System Platform

Дата: 2026-08-07

Статус: согласованный дизайн первой версии; реализация и технический план выполняются после отдельного ревью этого документа.

## 1. Назначение

Puntiro Design System Platform — каноническая живая документация продуктового интерфейса Puntiro и исполняемая компонентная база для Windows-киоска и облачной админки.

Платформа строится поверх Storybook, но не ограничивается каталогом stories. Она должна последовательно описывать:

1. дизайн-токены и базовые решения;
2. компоненты и их API;
3. правила использования компонентов;
4. доменные patterns;
5. правила композиции;
6. позднее — утверждённые рабочие compositions и экраны.

Первая версия формирует язык интерфейса и проверяемые строительные блоки. Она намеренно предшествует отрисовке полноценных экранов.

## 2. Контекст продукта

Система Puntiro предназначена для полностью touch-friendly маркировки готовых отгрузок на складе. Оператор на общем Windows-киоске выбирает отгрузку, вводит фактическое количество мест, выбирает совместимый принтер при необходимости и печатает комплект этикеток `1 из N`.

Основные ограничения продуктового интерфейса:

- базовый viewport киоска — 1280 × 800, landscape, с адаптацией вверх;
- рабочие kiosk-композиции не имеют прокрутки страницы или внутренних рабочих областей;
- primary touch target — не менее 64 px, комфортный — 72 px;
- интерфейс должен оставаться пригодным для работы пальцами и в перчатках;
- критичные действия не зависят от hover, скрытых жестов и вложенных меню;
- главным идентификатором задания является крупный номер отгрузки или документа продажи;
- товарный состав не показывается: система отвечает именно за маркировку уже отобранных товаров;
- состояния offline, printer failure и unknown print result являются первоклассными сценариями.

Полная продуктовая концепция зафиксирована в `2026-08-07-shipment-label-kiosk-design.md`. Утверждённая визуальная основа находится в `brandbook/`.

## 3. Принципы системы

### 3.1. Operational clarity

Каждое состояние должно ясно отвечать на три вопроса: что происходит, что требуется от оператора и что произойдёт после действия.

### 3.2. Touch first, keyboard complete

Киоск проектируется прежде всего для touch и перчаток, но компоненты сохраняют полную клавиатурную доступность, логичный focus order и корректные accessible names.

### 3.3. One source of truth

Токены, компоненты, документация, stories и тесты находятся в одном workspace. Отдельный документационный портал в первой версии не создаётся.

### 3.4. States are product behavior

Loading, empty, offline, invalid, failed и unknown — не декоративные варианты. Они документируются и проверяются как часть контракта компонента или pattern.

### 3.5. Brand as function

Фирменная registration line, последовательность модулей и оранжевая конечная точка объясняют выбор, текущий этап или передачу. Они не используются как фоновая декорация.

### 3.6. Premium through precision

Высокое визуальное качество достигается типографикой, ритмом, материалами, состояниями и обратной связью. Маркетинговые hero/AIDA-паттерны, glassmorphism, декоративные градиенты и тяжёлая motion-сценография не переносятся в операционный интерфейс.

## 4. Границы первой версии

### 4.1. Входит в первую версию

- брендированная оболочка Storybook;
- навигация Start, Foundations, Components, Kiosk Patterns, Rules и Compositions;
- переключатель русского и английского языков;
- DTCG-совместимый источник токенов и генерируемые CSS, JSON и TypeScript exports;
- страницы базовых решений и автоматически сформированный каталог токенов;
- первая волна core components и kiosk patterns;
- единый шаблон документации компонентов;
- статусы зрелости Draft, Beta, Stable и Deprecated;
- локальные component, interaction, accessibility и visual regression tests;
- правила композиции и структура будущего раздела Compositions.

### 4.2. Не входит в первую версию

- бизнес-приложение Windows-киоска;
- интерфейс облачной админки;
- API, БД, синхронизация и маршрутизация;
- интеграция с Zebra, TSC или Bixolon;
- ZPL/TSPL renderer;
- полноценные рабочие экраны и end-to-end пользовательские потоки;
- тёмная продуктовая тема;
- публикация Storybook в платное облако;
- визуальный редактор этикеток.

Раздел Compositions создаётся сразу, но до отдельного этапа содержит методику, требования и структуру, а не готовые экраны.

## 5. Архитектура workspace

Проект создаётся как pnpm workspace с тремя основными единицами:

```text
packages/
  tokens/       DTCG source, Style Dictionary и генерируемые exports
  ui/           React components, patterns, styles, types и locale resources
apps/
  storybook/    Puntiro Design System Platform, MDX/docs, stories и checks
```

Технический фундамент:

- React и TypeScript;
- Vite;
- Storybook 10.x;
- React Aria Components как внутренний поведенческий слой;
- Style Dictionary для преобразования токенов;
- CSS Modules и CSS custom properties;
- Lucide как ограниченный внутренний источник operational icons;
- Vitest browser mode, Playwright и axe для проверок.

Готовый визуальный UI-kit не используется. Публичный API принадлежит Puntiro: типы и детали React Aria и Lucide не должны протекать наружу.

## 6. Информационная архитектура платформы

### 6.1. Start

- назначение системы;
- принципы;
- быстрый вход для дизайнера и разработчика;
- модель зрелости;
- ссылки на Foundations, Components и Rules.

### 6.2. Foundations

- Brand;
- Color;
- Typography;
- Spacing;
- Sizing and touch targets;
- Radius, border and surface;
- Motion;
- Focus;
- Accessibility;
- Token catalog.

### 6.3. Components

Универсальные компоненты без знаний об отгрузках, принтерах и API.

### 6.4. Kiosk Patterns

Доменные сборки нескольких components. Pattern знает предметную область, но не загружает данные, не управляет маршрутизацией и не обращается к оборудованию.

### 6.5. Rules

- touch и standard interaction modes;
- layout 1280 × 800+;
- иерархия действий;
- правила контента и длинных значений;
- feedback;
- offline и printer failure;
- do/don't;
- границы Foundation → Component → Pattern → Composition.

### 6.6. Compositions

На первом этапе раздел описывает контракт будущих композиций. Позднее здесь размещаются собранные состояния очереди отгрузок, ввода количества мест, выбора принтера, печати, успеха, unknown result и архива.

Документационные страницы могут прокручиваться. Ограничение no-scroll применяется только к kiosk-compositions и их проверочным frames.

## 7. Модель токенов

Канонический source использует DTCG JSON и делится на три уровня.

### 7.1. Reference tokens

Исходные брендовые значения и шкалы, например:

- `color.orange.500`;
- `color.ink.900`;
- `space.400`;
- `font.family.body`;
- `radius.control`;
- `duration.fast`.

Reference tokens не используются напрямую внутри компонентов.

### 7.2. Semantic tokens

Описывают роль значения в интерфейсе:

- `color.background.canvas`;
- `color.surface.primary`;
- `color.text.primary`;
- `color.action.primary`;
- `color.status.warning`;
- `size.touch.minimum`;
- `focus.ring.color`.

Компоненты по умолчанию используют semantic layer. Это позволяет изменять брендовые значения или вводить новую тему без переписывания component styles.

### 7.3. Component tokens

Создаются только для реальной локальной вариативности, например:

- `button.primary.background`;
- `shipment-card.border.selected`;
- `number-input.stepper.size`;
- `dialog.action-area.gap`.

Отдельный токен для каждого CSS-свойства не создаётся.

### 7.4. Token pipeline

```text
DTCG JSON source
        ↓
Style Dictionary
        ↓
CSS custom properties + JSON + TypeScript
        ↓
Puntiro UI + Storybook documentation
```

Каждый токен получает type, value, description и категорию. Aliases используются вместо копирования значения. Устаревающие токены помечаются `deprecated` и получают путь миграции.

Существующие `brandbook/tokens.css` и `brandbook/tokens.json` мигрируются в новый source без изменения утверждённых брендовых значений.

## 8. Локализация RU/EN

Русский является языком по умолчанию. Английский закладывается как полноценная ручная локализация с первого релиза.

Глобальный toolbar switcher `RU / EN` управляет Storybook global `locale`. Общий `PuntiroI18nProvider` передаёт locale в docs blocks, stories, patterns и будущие compositions.

Переключаются:

- редакционная документация;
- описания токенов;
- назначение и правила компонентов;
- do/don't;
- демонстрационные данные;
- подписи, ошибки и системные сообщения в stories;
- форматирование чисел и дат.

Требования:

- translation keys типизированы;
- сборка обнаруживает отсутствующие переводы;
- Stable-компонент обязан иметь обе локали;
- длинный контент проверяется отдельно в RU и EN;
- названия компонентов, props, токенов и примеры кода остаются английскими;
- переключатель текстовый, без флагов;
- системная оболочка Storybook остаётся английской: локализация неподдерживаемых частей manager UI через хрупкие overrides не выполняется.

## 9. Interaction modes

Одна библиотека обслуживает киоск и облачную админку через два режима взаимодействия.

### 9.1. Touch

- используется kiosk-приложением;
- минимальная интерактивная цель — 64 px;
- комфортная primary action — 72 px;
- увеличенные интервалы;
- критичные действия имеют текстовые подписи;
- действия не скрываются в hover или жестах.

### 9.2. Standard

- используется облачной админкой и документационными controls;
- минимальная интерактивная цель — 44 px;
- допускает более высокую информационную плотность;
- сохраняет тот же semantic, visual и accessibility contract.

Режим устанавливается один раз через `PuntiroProvider` на корневом уровне, а не повторяющимися `size` props на каждом компоненте.

## 10. Уровни сборки

### 10.1. Foundation

Токены и правила без собственного поведения.

### 10.2. Component

Одна универсальная задача и предметно-независимый публичный API.

### 10.3. Pattern

Композиция components для одной предметной задачи. Получает данные и callbacks, но не выполняет I/O.

### 10.4. Composition

Полностью собранное состояние рабочего задания. Получает подготовленные данные и callbacks. API, routing, persistence и hardware adapters остаются уровнем приложения.

## 11. Первая волна компонентов

### 11.1. Core components

- `Button`;
- `IconButton`;
- `PuntiroIcon`;
- `NumberInput`;
- `Select`;
- `StatusBadge`;
- `Surface`;
- `Dialog`;
- `InlineMessage`;
- `ProgressIndicator`.

`NumberInput` поддерживает диапазон 1–100, крупные stepper actions и корректное удержание кнопки. `Select` остаётся универсальным component; kiosk-выбор нескольких принтеров выполняется не компактным dropdown, а отдельным крупным `PrinterPicker` pattern.

### 11.2. Kiosk patterns

- `ShipmentTaskCard`;
- `PlaceCounter`;
- `PrinterPicker`;
- `ConnectivityBanner`;
- `PrintProgress`;
- `EmptyState`;
- `LoadingState`;
- `ErrorState`;
- `UnknownPrintResult`.

`UnknownPrintResult` является отдельным pattern, потому что неизвестный результат печати нельзя визуально или семантически сводить к обычной ошибке.

## 12. Контракт документации компонента

Каждый component и pattern получает одинаковую структуру страницы:

1. Overview;
2. Anatomy;
3. Variants;
4. States;
5. Behavior;
6. Content;
7. Accessibility;
8. Usage;
9. Do / Don't;
10. API;
11. Changelog.

Обязательные stories:

- default;
- hover, focus-visible и pressed;
- disabled;
- loading, если применимо;
- validation или error;
- длинный русский и английский текст;
- длинный номер отгрузки, если применимо;
- минимальное и максимальное значение;
- touch context;
- состояние в viewport 1280 × 800.

Stories являются исполняемыми тестовыми сценариями, а не только визуальными примерами.

## 13. Зрелость и lifecycle

### Draft

Исследуется, API нестабилен, production use запрещён без явного исключения.

### Beta

Можно использовать в ограниченном scope; допускаются изменения API с release note.

### Stable

Документирован, локализован, проверен и имеет защищённый публичный API.

### Deprecated

Новые использования запрещены; документация содержит replacement и migration path.

Stable требует:

- полной документации;
- RU/EN;
- interaction tests;
- accessibility check;
- local visual baseline;
- keyboard review;
- manual touch review;
- проверки длинного контента;
- changelog entry.

## 14. Визуальная грамматика

### 14.1. Цвет

- Register Ink — основной текст и сильный контраст;
- Label Paper — рабочий фон;
- Label Paper Strong — поверхности;
- Handoff Orange — primary action, выбранное состояние и текущий этап;
- Warehouse Steel — вторичная информация;
- status colors всегда сопровождаются текстом, иконкой или формой;
- градиенты отсутствуют;
- оранжевый не используется как большая декоративная заливка.

### 14.2. Типографика

- Onest — интерфейс и документация;
- IBM Plex Mono — номера отгрузок, `1 из N`, коды и технические параметры;
- номер отгрузки является главным визуальным идентификатором карточки;
- uppercase применяется только для коротких служебных labels.

### 14.3. Форма и поверхность

- 4 px — служебные labels;
- 12 px — controls;
- 24 px — крупные surfaces и документационные секции;
- kiosk-карточки отделяются прежде всего рамкой и пространством;
- тени применяются редко и не создают отдельный слой для каждого элемента;
- glassmorphism исключён;
- pill-форма используется для настоящих statuses, а не обычных buttons.

### 14.4. Иконки

`PuntiroIcon` инкапсулирует ограниченный утверждённый набор Lucide glyphs, фиксирует размеры и stroke. Критичное kiosk-действие не обозначается одной иконкой без текстовой подписи. Публичный API не зависит от Lucide и допускает замену источника без изменения потребителей.

### 14.5. Motion

- 120 ms — hover, press, focus и локальные изменения;
- 180 ms — подтверждение выбора и завершение действия;
- предпочтительны opacity и transform;
- нет анимаций появления страницы и декоративных бесконечных циклов;
- прогресс печати дискретный и содержательный;
- поддерживается `prefers-reduced-motion`.

## 15. Правила kiosk-compositions

- обязательный reference frame 1280 × 800;
- нет прокрутки страницы и вложенных рабочих областей;
- в каждом состоянии одна очевидная primary action;
- status подключения и печати остаётся видимым;
- критичное действие не зависит от hover;
- нет скрытых жестов и вложенных меню;
- Dialog используется для решения, а не для навигации;
- PrinterPicker показывает крупные варианты;
- длинный номер отгрузки не обрезается без доступного полного значения;
- действие даёт немедленную визуальную обратную связь;
- unknown print result отличается от failed визуально, текстово и семантически.

## 16. Проверки и quality pipeline

### 16.1. Token checks

- schema validation;
- сборка CSS, JSON и TypeScript;
- отсутствие битых aliases;
- отсутствие незаписанного generated diff.

### 16.2. Component and interaction tests

Storybook stories запускаются через Vitest browser mode в Playwright Chromium. Проверяются smoke rendering, play functions, keyboard navigation, validation, focus management и controlled/uncontrolled API.

### 16.3. Accessibility

- axe для каждой Stable story;
- accessible names и descriptions;
- focus order;
- состояния ошибок;
- status не передаётся одним цветом;
- reduced motion;
- ручной screen-reader review для сложных patterns.

### 16.4. Local visual regression

Playwright `toHaveScreenshot()` хранит baseline в репозитории. Browser version, fonts, locale и viewport фиксируются. Ключевые stories имеют отдельные RU/EN и touch/standard baselines. Платная облачная visual testing dependency отсутствует.

### 16.5. Kiosk invariants

Для каждой будущей kiosk-composition автоматически проверяются:

- `scrollWidth <= clientWidth`;
- `scrollHeight <= clientHeight`;
- touch targets не меньше 64 px;
- primary action видима;
- длинный номер не ломает layout;
- состояние не зависит от hover;
- RU и EN не создают overflow.

### 16.6. Ручные ворота

Отдельно от автоматических проверок фиксируются:

- Windows при 1280 × 800;
- реальный touch display;
- работа в перчатках;
- визуальная оценка motion;
- на следующих этапах — реальные Zebra/TSC/Bixolon.

Автоматические результаты никогда не выдаются за прохождение ручной hardware-проверки.

## 17. Критерии готовности первой версии

Первая версия готова к ревью, когда:

1. workspace собирается одной документированной командой;
2. Storybook открывается как брендированная Puntiro-платформа;
3. навигация соответствует информационной архитектуре;
4. RU/EN switcher меняет контролируемый нами docs и story content;
5. токены имеют один DTCG source и воспроизводимо генерируют все exports;
6. все перечисленные core components и kiosk patterns реализованы и документированы;
7. Draft/Beta/Stable/Deprecated отражаются в платформе;
8. Stable stories проходят component, interaction и axe checks;
9. локальные visual baselines воспроизводимы;
10. раздел Rules содержит утверждённые composition constraints;
11. раздел Compositions готов принять будущие рабочие композиции, но не подменяет отдельный этап дизайна экранов;
12. подтверждена граница: библиотека не выполняет API, persistence или hardware I/O.

## 18. Следующий этап

После ревью и принятия этой спецификации создаётся отдельный implementation plan с небольшими проверяемыми задачами. Только после согласования плана начинается scaffold workspace, TDD-реализация tokens, platform shell, components и patterns.
