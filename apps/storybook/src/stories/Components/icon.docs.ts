import { defineDocumentation } from '../../docs/defineDocumentation';

export const iconDocumentation = defineDocumentation({
  ru: {
    overview: 'PuntiroIcon выводит только утвержденный закрытый набор иконок.',
    anatomy: ['Имя из закрытого реестра', 'Векторный глиф'],
    variants: ['Каждое имя соответствует одной утвержденной иконке.'],
    states: ['Иконка наследует цвет и не имеет интерактивного состояния.'],
    behavior: ['Реестр не принимает произвольные Lucide-имена.'],
    content: ['Выбирайте имя по действию или статусу.'],
    accessibility: ['PuntiroIcon декоративна внутри подписанного элемента.', 'Критическое действие получает текст или доступное имя от родительского контрола.'],
    usage: ['Используйте archive, check, chevronDown, close, connection, error, minus, plus, printer, refresh, search или warning.'],
    do: ['Сочетать иконку с понятной меткой действия.'],
    dont: ['Не заменять иконкой обязательный текст или эмодзи.'],
    changelog: ['Beta: закрытый реестр PuntiroIcon.']
  },
  en: {
    overview: 'PuntiroIcon renders only the approved closed icon set.',
    anatomy: ['A name from the closed registry', 'A vector glyph'],
    variants: ['Every name maps to one approved icon.'],
    states: ['The icon inherits color and has no interactive state.'],
    behavior: ['The registry does not accept arbitrary Lucide names.'],
    content: ['Choose a name for the action or status.'],
    accessibility: ['PuntiroIcon is decorative inside a labelled element.', 'A critical action receives text or an accessible name from its parent control.'],
    usage: ['Use archive, check, chevronDown, close, connection, error, minus, plus, printer, refresh, search, or warning.'],
    do: ['Pair an icon with a clear action label.'],
    dont: ['Do not replace required text or emoji with an icon.'],
    changelog: ['Beta: closed PuntiroIcon registry.']
  }
});
