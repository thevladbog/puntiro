import { defineDocumentation } from '../../docs/defineDocumentation';

export const iconButtonDocumentation = defineDocumentation({
  ru: {
    overview: 'IconButton предоставляет компактное действие с обязательным доступным именем.',
    anatomy: ['Квадратная область нажатия', 'Иконка Puntiro', 'Обязательная текстовая метка для технологий доступности'],
    variants: ['secondary, danger и ghost.'],
    states: ['Обычное, фокус, нажатие и отключено.'],
    behavior: ['onPress вызывается только для доступной кнопки.'],
    content: ['label описывает действие, а не форму иконки.'],
    accessibility: ['label формирует доступное имя, даже если видна только иконка.', 'Иконка декоративна для читателя экрана.'],
    usage: ['Используйте рядом с объектом, когда действие известно из контекста.'],
    do: ['Передавать конкретную метку, например Закрыть.'],
    dont: ['Не использовать IconButton как единственное главное действие.'],
    changelog: ['Beta: первая публичная версия IconButton.']
  },
  en: {
    overview: 'IconButton provides a compact action with a required accessible name.',
    anatomy: ['Square press target', 'Puntiro icon', 'Required text label for assistive technology'],
    variants: ['secondary, danger, and ghost.'],
    states: ['Default, focused, pressed, and disabled.'],
    behavior: ['onPress runs only for an available button.'],
    content: ['label describes the action, not the icon shape.'],
    accessibility: ['label supplies the accessible name even when only the icon is visible.', 'The icon is decorative to screen readers.'],
    usage: ['Use near an object when the action is clear from context.'],
    do: ['Provide a specific label, such as Close.'],
    dont: ['Do not use IconButton as the only primary action.'],
    changelog: ['Beta: first public IconButton release.']
  }
});
