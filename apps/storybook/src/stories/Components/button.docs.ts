import { defineDocumentation } from '../../docs/defineDocumentation';

export const buttonDocumentation = defineDocumentation({
  ru: {
    overview: 'Button запускает понятное действие в киоске и административном интерфейсе.',
    anatomy: ['Текст действия', 'Необязательная иконка перед текстом', 'Индикатор выполнения при загрузке'],
    variants: ['primary для главного следующего действия', 'secondary для равнозначного действия', 'danger для необратимого действия', 'ghost для вспомогательного действия'],
    states: ['Обычное, фокус с клавиатуры, нажатие, отключено и загрузка.'],
    behavior: ['onPress срабатывает один раз на доступное нажатие.', 'Загрузка блокирует повторное нажатие и сообщает о выполнении.'],
    content: ['Используйте короткий глагол и объект: Напечатать, Сохранить, Повторить.'],
    accessibility: ['Текст кнопки остается доступным именем во время загрузки.', 'Фокус виден с клавиатуры, а размер зависит от режима touch или standard.'],
    usage: ['Выбирайте один primary в пределах следующего шага.'],
    do: ['Ставить iconBefore только когда она уточняет текст.'],
    dont: ['Не передавать className или style для переопределения контракта.'],
    changelog: ['Beta: первая публичная версия Button.']
  },
  en: {
    overview: 'Button starts a clear action in kiosk and admin interfaces.',
    anatomy: ['Action label', 'Optional leading icon', 'Progress cue while loading'],
    variants: ['primary for the main next action', 'secondary for a peer action', 'danger for an irreversible action', 'ghost for a supporting action'],
    states: ['Default, keyboard focus, pressed, disabled, and loading.'],
    behavior: ['onPress runs once for an available press.', 'Loading blocks repeated presses and announces work in progress.'],
    content: ['Use a short verb and object: Print, Save, Retry.'],
    accessibility: ['The button label remains its accessible name while loading.', 'Keyboard focus is visible and size follows touch or standard mode.'],
    usage: ['Choose one primary action for the current next step.'],
    do: ['Use iconBefore only when it clarifies the label.'],
    dont: ['Do not pass className or style to override the contract.'],
    changelog: ['Beta: first public Button release.']
  }
});
