import { defineDocumentation } from '../../docs/defineDocumentation';

export const numberInputDocumentation = defineDocumentation({
  ru: {
    overview: 'NumberInput задаёт количество мест в диапазоне и сохраняет доступные клавиатурные и удерживаемые stepper-действия React Aria.',
    anatomy: ['Текстовая label', 'Крупная кнопка уменьшения', 'Числовое поле', 'Крупная кнопка увеличения', 'Description или сообщение об ошибке'],
    variants: ['Вариантов нет: плотность задаёт PuntiroProvider в режимах touch и standard.'],
    states: ['Обычное, минимальное, максимальное, invalid и disabled.'],
    behavior: ['По умолчанию диапазон 1–100, шаг 1.', 'Кнопки и стрелки клавиатуры не выходят за границы.', 'Нажатие и удержание stepper-кнопок сохраняет нативное поведение React Aria.', 'Колесо мыши не изменяет значение.', 'Если minValue больше maxValue, компонент выбрасывает RangeError вместо неявной корректировки конфигурации.'],
    content: ['label описывает считаемую сущность.', 'description объясняет допустимое значение.', 'errorMessage объясняет, как исправить ввод.'],
    accessibility: ['Поле имеет spinbutton-семантику, связанную label, description и ошибкой.', 'Кнопки имеют локализованные имена Уменьшить и Увеличить.', 'Фокус остаётся заметным на поле и обеих кнопках.', 'В touch каждая интерактивная область не меньше 64 px; в standard — не меньше 44 px.'],
    usage: ['Используйте для небольшого ограниченного числового диапазона.', 'value делает компонент controlled, defaultValue — uncontrolled; не передавайте оба значения одновременно.', 'Подтверждение больших количеств — будущая composition policy, а не обязанность NumberInput.'],
    do: ['Передавать конкретную label, например Количество мест.', 'Показывать понятную errorMessage вместе с invalid-состоянием.'],
    dont: ['Не заменять label плейсхолдером.', 'Не использовать NumberInput для подтверждения большого количества без composition policy.'],
    changelog: ['Beta: первая публичная версия; требуется ручная проверка touch-дисплея и работы в перчатках.']
  },
  en: {
    overview: 'NumberInput sets a bounded item count while preserving React Aria keyboard and press-and-hold stepper behavior.',
    anatomy: ['Text label', 'Large decrement button', 'Number input', 'Large increment button', 'Description or error message'],
    variants: ['There are no visual variants: PuntiroProvider sets touch or standard density.'],
    states: ['Default, minimum, maximum, invalid, and disabled.'],
    behavior: ['The default range is 1–100 with a step of 1.', 'Stepper buttons and keyboard arrows never exceed the bounds.', 'Press-and-hold on stepper buttons retains React Aria behavior.', 'Mouse wheel does not change the value.', 'When minValue exceeds maxValue, the component throws RangeError rather than silently changing the explicit configuration.'],
    content: ['label names the counted entity.', 'description explains the acceptable value.', 'errorMessage explains how to correct input.'],
    accessibility: ['The field has spinbutton semantics associated with its label, description, and error.', 'Buttons have localized Decrease and Increase names.', 'Focus remains visible on the input and both buttons.', 'Every interactive area is at least 64 px in touch and 44 px in standard mode.'],
    usage: ['Use for a small, bounded numeric range.', 'value makes the component controlled and defaultValue makes it uncontrolled; do not pass both.', 'High-count confirmation is a future composition policy, not a NumberInput responsibility.'],
    do: ['Provide a specific label, such as Number of packages.', 'Show a clear errorMessage with the invalid state.'],
    dont: ['Do not replace the label with a placeholder.', 'Do not use NumberInput as high-count confirmation without a composition policy.'],
    changelog: ['Beta: first public version; manual touch-display and gloved-operation review is still required.']
  }
});
