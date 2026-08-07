import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fireEvent, fn, userEvent, within } from 'storybook/test';
import { NumberInput } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { numberInputDocumentation } from './numberInput.docs';

const meta = {
  title: 'Components/NumberInput',
  component: NumberInput,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'NumberInput', maturity: 'beta', documentation: numberInputDocumentation }) } },
  args: {
    label: 'Количество мест',
    defaultValue: 1
  }
} satisfies Meta<typeof NumberInput>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const input = canvas.getByRole('spinbutton', { name: 'Количество мест' });

    await userEvent.clear(input);
    await userEvent.type(input, '100');
    await userEvent.keyboard('{ArrowUp}');
    await expect(input).toHaveValue('100');
    await expect(input).toHaveAttribute('aria-valuenow', '100');
    await userEvent.keyboard('{ArrowDown}');
    await expect(input).toHaveValue('99');
    await expect(input).toHaveAttribute('aria-valuenow', '99');
    await fireEvent.wheel(input, { deltaY: -100 });
    await expect(input).toHaveValue('99');
  }
};

export const Minimum: Story = {
  args: { defaultValue: 1 },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const decrement = canvas.getByRole('button', { name: /^Уменьшить/ });
    await expect(decrement).toHaveAttribute('aria-label', 'Уменьшить');
    await expect(decrement).toBeDisabled();
  }
};

export const Maximum: Story = {
  args: { defaultValue: 100 },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const increment = canvas.getByRole('button', { name: /^Увеличить/ });
    await expect(increment).toHaveAttribute('aria-label', 'Увеличить');
    await expect(increment).toBeDisabled();
  }
};

export const Controlled: Story = {
  args: {
    value: 1,
    onChange: fn()
  },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: /^Увеличить/ }));
    await expect(args.onChange).toHaveBeenCalledWith(2);
  }
};

export const Invalid: Story = { args: { isInvalid: true, errorMessage: 'Укажите количество от 1 до 100.' } };
export const Disabled: Story = { args: { isDisabled: true } };
export const Touch: Story = { globals: { interactionMode: 'touch' } };
export const Standard: Story = { globals: { interactionMode: 'standard' } };
export const RU: Story = { globals: { locale: 'ru' } };
export const EN: Story = { globals: { locale: 'en' }, args: { label: 'Number of packages' } };
export const LongError: Story = {
  args: {
    isInvalid: true,
    errorMessage: 'Количество мест должно быть от 1 до 100. Проверьте данные задания и повторите ввод.'
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
