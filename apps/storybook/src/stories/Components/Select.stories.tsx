import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { Select } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { selectDocumentation } from './select.docs';

const items = [
  { id: 'zpl', label: 'ZPL', description: 'Для совместимых термопринтеров.' },
  { id: 'tspl', label: 'TSPL', description: 'Для принтеров этикеток TSPL.' },
  { id: 'epl', label: 'EPL', isDisabled: true, description: 'Не поддерживается текущим заданием.' }
] as const;

const meta = {
  title: 'Components/Select',
  component: Select,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'Select', maturity: 'beta', documentation: selectDocumentation }) } },
  args: { label: 'Язык принтера', items, placeholder: 'Выберите язык' }
} satisfies Meta<typeof Select>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Placeholder: Story = {
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button', { name: 'Язык принтера' });
    await expect(trigger).toHaveTextContent('Выберите язык');
  }
};

export const Selection: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Язык принтера' }));
    await userEvent.click(within(document.body).getByRole('option', { name: 'ZPL' }));
    await expect(canvas.getByRole('button', { name: 'Язык принтера' })).toHaveTextContent('ZPL');
  }
};

export const Controlled: Story = {
  args: { selectedId: 'zpl', onSelectionChange: fn() },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Язык принтера' }));
    await userEvent.click(within(document.body).getByRole('option', { name: 'TSPL' }));
    await expect(args.onSelectionChange).toHaveBeenCalledWith('tspl');
    await expect(canvas.getByRole('button', { name: 'Язык принтера' })).toHaveTextContent('ZPL');
  }
};

export const Keyboard: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const trigger = canvas.getByRole('button', { name: 'Язык принтера' });
    trigger.focus();
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{Enter}');
    await expect(trigger).toHaveTextContent('TSPL');
  }
};

export const DisabledOption: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Язык принтера' }));
    const option = within(document.body).getByRole('option', { name: 'EPL' });
    await expect(option).toHaveAttribute('aria-disabled', 'true');
    await userEvent.click(option);
    await expect(canvas.getByRole('button', { name: 'Язык принтера' })).toHaveTextContent('Выберите язык');
  }
};

export const Invalid: Story = {
  args: { description: 'Выберите язык для тестовой этикетки.', isInvalid: true, errorMessage: 'Выберите поддерживаемый язык.' },
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button', { name: 'Язык принтера' });
    await expect(canvasElement.querySelector('[data-invalid="true"]')).not.toBeNull();
    await expect(trigger).toHaveAccessibleDescription('Выберите язык для тестовой этикетки. Выберите поддерживаемый язык.');
  }
};

export const Disabled: Story = { args: { isDisabled: true, defaultSelectedId: 'zpl' } };
export const LongOption: Story = {
  args: { items: [{ id: 'long', label: 'Расширенный язык печати для маркировочных этикеток на совместимом термопринтере', description: 'Полное описание остаётся доступным.' }] }
};
export const Touch: Story = {
  globals: { interactionMode: 'touch' },
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button');
    await expect(trigger.getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
    await userEvent.click(trigger);
    await expect(within(document.body).getByRole('option', { name: 'ZPL' }).getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
  }
};
export const Standard: Story = {
  globals: { interactionMode: 'standard' },
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button');
    await expect(trigger.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
    await userEvent.click(trigger);
    await expect(within(document.body).getByRole('option', { name: 'ZPL' }).getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
  }
};
export const EN: Story = { globals: { locale: 'en' }, args: { label: 'Label language', placeholder: 'Choose a language', items: [{ id: 'zpl', label: 'ZPL' }, { id: 'tspl', label: 'TSPL' }] } };
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
