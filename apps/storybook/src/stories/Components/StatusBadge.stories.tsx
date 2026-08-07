import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, within } from 'storybook/test';
import { StatusBadge } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { statusBadgeDocumentation } from './feedback.docs';

const meta = {
  title: 'Components/StatusBadge',
  component: StatusBadge,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'StatusBadge', maturity: 'beta', documentation: statusBadgeDocumentation }) } },
  args: { label: 'Готов к печати', tone: 'success' }
} satisfies Meta<typeof StatusBadge>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('status')).toHaveTextContent('Готов к печати');
  }
};
export const Neutral: Story = { args: { tone: 'neutral', label: 'Ожидает проверки' } };
export const Info: Story = { args: { tone: 'info', label: 'Принтер подключен' } };
export const Success: Story = { args: { tone: 'success', label: 'Готов к печати' } };
export const Warning: Story = { args: { tone: 'warning', label: 'Нужна бумага' } };
export const Danger: Story = { args: { tone: 'danger', label: 'Печать остановлена' } };
export const Monochrome: Story = { args: { tone: 'neutral', label: 'Подтверждено текстом и иконкой' } };
export const LongContent: Story = { args: { label: 'Принтер на участке упаковки готов к печати маркировочных кодов для выбранной отгрузки' } };
export const English: Story = { globals: { locale: 'en' }, args: { label: 'Ready to print' } };
export const ReducedMotion: Story = { parameters: { chromatic: { disableSnapshot: false } } };
