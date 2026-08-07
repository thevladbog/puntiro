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
    const wheelEvents: WheelEvent[] = [];
    const observeWheel = (event: WheelEvent) => wheelEvents.push(event);

    canvasElement.addEventListener('wheel', observeWheel);
    await expect(input).toHaveAttribute('aria-roledescription', 'Числовое поле');
    await expect(input).toHaveAttribute('aria-valuetext', '1');

    await userEvent.clear(input);
    await userEvent.type(input, '100');
    await userEvent.keyboard('{ArrowUp}');
    await expect(input).toHaveValue('100');
    await expect(input).toHaveAttribute('aria-valuenow', '100');
    await expect(input).toHaveAttribute('aria-valuetext', '100');
    await userEvent.keyboard('{ArrowDown}');
    await expect(input).toHaveValue('99');
    await expect(input).toHaveAttribute('aria-valuenow', '99');
    await fireEvent.wheel(input, { deltaY: -100 });
    await expect(input).toHaveValue('99');
    await expect(wheelEvents).toHaveLength(1);
    await expect(wheelEvents[0]!.defaultPrevented).toBe(false);
    canvasElement.removeEventListener('wheel', observeWheel);
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

export const Invalid: Story = {
  args: {
    description: 'Введите количество мест для печати.',
    isInvalid: true,
    errorMessage: 'Укажите количество от 1 до 100.'
  },
  play: async ({ canvasElement }) => {
    const input = within(canvasElement).getByRole('spinbutton', { name: 'Количество мест' });
    await expect(input).toHaveAttribute('aria-invalid', 'true');
    await expect(input).toHaveAccessibleDescription('Введите количество мест для печати. Укажите количество от 1 до 100.');
  }
};
export const Disabled: Story = {
  args: { isDisabled: true, description: 'Количество зафиксировано.' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const label = canvas.getByText('Количество мест');
    const input = canvas.getByRole('spinbutton', { name: 'Количество мест' });
    const controls = [input, ...canvas.getAllByRole('button')];

    await expect(label).toBeVisible();
    for (const control of controls) await expect(control).toBeDisabled();
    const [red, green, blue] = getComputedStyle(label).color.match(/\d+/g)!.map(Number);
    const [canvasRed, canvasGreen, canvasBlue] = getComputedStyle(document.documentElement).backgroundColor.match(/\d+/g)!.map(Number);
    const luminance = ([r, g, b]: number[]) => {
      const channels = [r, g, b].map((channel) => {
        const normalized = channel / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * channels[0]! + 0.7152 * channels[1]! + 0.0722 * channels[2]!;
    };
    const contrast = (Math.max(luminance([red!, green!, blue!]), luminance([canvasRed!, canvasGreen!, canvasBlue!])) + 0.05)
      / (Math.min(luminance([red!, green!, blue!]), luminance([canvasRed!, canvasGreen!, canvasBlue!])) + 0.05);
    await expect(contrast).toBeGreaterThanOrEqual(4.5);
  }
};
export const Touch: Story = {
  globals: { interactionMode: 'touch' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    for (const control of [canvas.getByRole('spinbutton'), ...canvas.getAllByRole('button')]) {
      await expect(control.getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
    }
  }
};
export const Standard: Story = {
  globals: { interactionMode: 'standard' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    for (const control of [canvas.getByRole('spinbutton'), ...canvas.getAllByRole('button')]) {
      await expect(control.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
    }
  }
};
export const RU: Story = {
  globals: { locale: 'ru' },
  args: { defaultValue: 1.5, minValue: 1, maxValue: 2, step: 0.5 },
  play: async ({ canvasElement }) => {
    const input = within(canvasElement).getByRole('spinbutton', { name: 'Количество мест' });
    await expect(input).toHaveAttribute('aria-valuetext', '1,5');
  }
};
export const EN: Story = {
  globals: { locale: 'en' },
  args: { label: 'Number of packages', defaultValue: 1.5, minValue: 1, maxValue: 2, step: 0.5 },
  play: async ({ canvasElement }) => {
    const input = within(canvasElement).getByRole('spinbutton', { name: 'Number of packages' });
    await expect(input).toHaveAttribute('aria-valuetext', '1.5');
  }
};
export const CustomRange: Story = {
  args: { defaultValue: 5, minValue: 5, maxValue: 10, step: 2 },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const input = canvas.getByRole('spinbutton', { name: 'Количество мест' });
    const increment = canvas.getByRole('button', { name: /^Увеличить/ });

    await userEvent.click(increment);
    await expect(input).toHaveValue('7');
    await userEvent.click(increment);
    await expect(input).toHaveValue('9');
    await expect(increment).not.toBeDisabled();
    await userEvent.click(increment);
    await expect(input).toHaveValue('10');
    await expect(increment).toBeDisabled();
  }
};
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
