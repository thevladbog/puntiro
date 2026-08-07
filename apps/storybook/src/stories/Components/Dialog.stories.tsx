import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, userEvent, within } from 'storybook/test';
import { Button, Dialog } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { dialogDocumentation } from './dialog.docs';

const meta = {
  title: 'Components/Dialog',
  component: Dialog,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'Dialog', maturity: 'beta', documentation: dialogDocumentation }) } },
  args: {
    trigger: <Button variant="secondary">Отменить печать</Button>,
    title: 'Отменить печать?',
    description: 'Уже переданные задания могут быть напечатаны.',
    children: <p>Проверьте очередь принтера перед повторной печатью.</p>,
    actions: [{ id: 'back', label: 'Продолжить печать', variant: 'secondary', onPress: (close) => close() }]
  }
} satisfies Meta<typeof Dialog>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Confirmation: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const trigger = canvas.getByRole('button', { name: 'Отменить печать' });
    await userEvent.click(trigger);
    const dialog = within(document.body).getByRole('dialog', { name: 'Отменить печать?' });
    await expect(dialog).toHaveAccessibleDescription('Уже переданные задания могут быть напечатаны.');
    await userEvent.keyboard('{Escape}');
    await expect(trigger).toHaveFocus();
  }
};

export const Destructive: Story = {
  args: {
    title: 'Удалить задание?',
    description: 'Удалённое задание нельзя восстановить.',
    trigger: <Button variant="danger">Удалить задание</Button>,
    actions: [{ id: 'delete', label: 'Удалить', variant: 'danger', onPress: (close) => close() }]
  }
};

export const Busy: Story = {
  args: {
    title: 'Печать выполняется',
    description: 'Дождитесь подтверждения от принтера.',
    children: <p>Закрыть это окно пока нельзя.</p>,
    actions: [],
    isDismissible: false
  },
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button', { name: 'Отменить печать' });
    await userEvent.click(trigger);
    const dialog = within(document.body).getByRole('dialog', { name: 'Печать выполняется' });
    await userEvent.keyboard('{Escape}');
    await expect(dialog).toBeVisible();
    await userEvent.click(within(document.body).getByTestId('dialog-overlay'));
    await expect(dialog).toBeVisible();
  }
};

export const FocusTrap: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Отменить печать' }));
    const action = within(document.body).getByRole('button', { name: 'Продолжить печать' });
    action.focus();
    await userEvent.tab();
    await expect(within(document.body).getByRole('dialog')).toContainElement(document.activeElement);
  }
};

export const LongRussian: Story = {
  args: {
    title: 'Подтвердите отмену печати маркировочных этикеток для отгрузки с большим количеством мест',
    description: 'Задание уже передано в очередь печати. Проверьте физический принтер и убедитесь, что на нём не продолжается выпуск этикеток.',
    children: <p>Это окно предназначено для решения, а не для навигации. После подтверждения отобразите понятный результат следующего шага.</p>
  }
};
export const LongEnglish: Story = {
  globals: { locale: 'en' },
  args: {
    trigger: <Button variant="secondary">Cancel printing</Button>,
    title: 'Confirm cancellation of label printing for a shipment with many packages',
    description: 'The task may already be in the print queue. Check the physical printer before repeating the operation.',
    children: <p>This dialog supports a decision rather than navigation and keeps the full instruction available.</p>,
    actions: [{ id: 'back', label: 'Continue printing', variant: 'secondary', onPress: (close) => close() }]
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
