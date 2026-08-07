import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { IconButton } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { iconButtonDocumentation } from './iconButton.docs';

const meta = {
  title: 'Components/IconButton',
  component: IconButton,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'IconButton', maturity: 'beta', documentation: iconButtonDocumentation }) } },
  args: { label: 'Закрыть', icon: 'close', onPress: fn() }
} satisfies Meta<typeof IconButton>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: 'Закрыть' });
    await userEvent.click(button);
    await expect(args.onPress).toHaveBeenCalledOnce();
  }
};
export const Danger: Story = { args: { label: 'Удалить', icon: 'error', variant: 'danger' } };
export const Ghost: Story = { args: { variant: 'ghost' } };
export const Disabled: Story = { args: { isDisabled: true } };
export const Hover: Story = {
  play: async ({ canvasElement }) => {
    const button = within(canvasElement).getByRole('button', { name: 'Закрыть' });
    await userEvent.hover(button);
    await expect(button).toHaveAttribute('data-hovered', 'true');
    await userEvent.unhover(button);
  }
};
export const English: Story = { globals: { locale: 'en' }, args: { label: 'Close' } };
export const Touch: Story = { globals: { interactionMode: 'touch' } };
export const Standard: Story = { globals: { interactionMode: 'standard' } };
