import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { Button } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { buttonDocumentation } from './button.docs';

const meta = {
  title: 'Components/Button',
  component: Button,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'Button', maturity: 'beta', documentation: buttonDocumentation }) } },
  args: { children: 'Напечатать', onPress: fn() }
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const Primary: Story = {
  args: { variant: 'primary' },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: 'Напечатать' });
    await userEvent.click(button);
    await expect(args.onPress).toHaveBeenCalledOnce();
    await expect(button).toHaveAttribute('data-primary-action', 'true');
  }
};

export const Secondary: Story = { args: { variant: 'secondary' } };
export const Danger: Story = { args: { variant: 'danger', children: 'Удалить' } };
export const Ghost: Story = { args: { variant: 'ghost', children: 'Отменить' } };
export const FocusVisible: Story = {
  play: async ({ canvasElement }) => {
    await userEvent.tab();
    await expect(within(canvasElement).getByRole('button', { name: 'Напечатать' })).toHaveFocus();
  }
};
export const Pressed: Story = { args: { children: 'Удерживать' } };
export const Disabled: Story = { args: { isDisabled: true } };
export const Loading: Story = {
  args: { isLoading: true, loadingLabel: 'Печать выполняется' },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: 'Напечатать' });
    await expect(canvas.getByRole('progressbar', { name: 'Печать выполняется' })).toBeVisible();
    await userEvent.click(button);
    await expect(args.onPress).not.toHaveBeenCalled();
  }
};
export const LongRussianText: Story = { args: { children: 'Напечатать маркировочные коды для выбранных мест' } };
export const English: Story = { globals: { locale: 'en' }, args: { children: 'Print labels' } };
export const Touch: Story = { globals: { interactionMode: 'touch' } };
export const Standard: Story = { globals: { interactionMode: 'standard' } };
