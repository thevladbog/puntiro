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

const englishItems = {
  zpl: { label: 'ZPL', description: 'For compatible thermal printers.' },
  tspl: { label: 'TSPL', description: 'For TSPL label printers.' },
  epl: { label: 'EPL', description: 'Not supported by the current task.' }
} as const;

function tokenColor(canvasElement: HTMLElement, token: string) {
  const probe = document.createElement('span');
  probe.style.color = `var(${token})`;
  canvasElement.append(probe);
  const color = getComputedStyle(probe).color;
  probe.remove();
  return color;
}

const meta = {
  title: 'Components/Select',
  component: Select,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'Select', maturity: 'beta', documentation: selectDocumentation }) } },
  args: { label: 'Язык принтера', items, placeholder: 'Выберите язык' },
  render: (args, context) => {
    const isEnglish = context.globals.locale === 'en';
    const localizedItems = isEnglish
      ? args.items.map((item) => {
        const copy = englishItems[item.id as keyof typeof englishItems];
        return copy ? { ...item, ...copy } : item;
      })
      : args.items;

    return <Select
      {...args}
      label={isEnglish && args.label === 'Язык принтера' ? 'Printer language' : args.label}
      placeholder={isEnglish && args.placeholder === 'Выберите язык' ? 'Choose a language' : args.placeholder}
      description={isEnglish && args.description === 'Выберите поддерживаемый язык.' ? 'Choose a supported language.' : args.description}
      items={localizedItems}
    />;
  }
} satisfies Meta<typeof Select>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Placeholder: Story = {
  args: { description: 'Выберите поддерживаемый язык.' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const isEnglish = canvasElement.querySelector('[data-locale]')?.getAttribute('data-locale') === 'en';
    const trigger = canvas.getByRole('button', { name: isEnglish ? 'Printer language' : 'Язык принтера' });
    await expect(trigger).toHaveTextContent(isEnglish ? 'Choose a language' : 'Выберите язык');
    await expect(getComputedStyle(trigger.querySelector('[data-placeholder]')!).color).toBe(tokenColor(canvasElement, '--puntiro-semantic-color-text-secondary'));
  }
};

export const Selection: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Язык принтера' }));
    await userEvent.click(within(document.body).getByRole('option', { name: 'ZPL' }));
    const trigger = canvas.getByRole('button', { name: 'Язык принтера' });
    await expect(trigger).toHaveTextContent('ZPL');
    await expect(getComputedStyle(trigger.querySelector('span')!).color).toBe(tokenColor(canvasElement, '--puntiro-semantic-color-text-primary'));
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
    await expect(option).toHaveAccessibleDescription('Не поддерживается текущим заданием.');
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
  args: { items: [{ id: 'zpl', label: 'ZPL' }] },
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button');
    await expect(trigger.getBoundingClientRect().height).toBe(64);
    await userEvent.click(trigger);
    const option = within(document.body).getByRole('option', { name: 'ZPL' });
    await expect(option.getBoundingClientRect().height).toBe(64);
    await expect(option.closest('[data-interaction-mode]')).toHaveAttribute('data-interaction-mode', 'touch');
  }
};
export const Standard: Story = {
  globals: { interactionMode: 'standard' },
  args: { items: [{ id: 'zpl', label: 'ZPL' }] },
  play: async ({ canvasElement }) => {
    const trigger = within(canvasElement).getByRole('button');
    await expect(trigger.getBoundingClientRect().height).toBe(44);
    await userEvent.click(trigger);
    const option = within(document.body).getByRole('option', { name: 'ZPL' });
    await expect(option.getBoundingClientRect().height).toBe(44);
    await expect(option.closest('[data-interaction-mode]')).toHaveAttribute('data-interaction-mode', 'standard');
  }
};
export const EN: Story = { globals: { locale: 'en' }, args: { label: 'Label language', placeholder: 'Choose a language', items: [{ id: 'zpl', label: 'ZPL' }, { id: 'tspl', label: 'TSPL' }] } };
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
