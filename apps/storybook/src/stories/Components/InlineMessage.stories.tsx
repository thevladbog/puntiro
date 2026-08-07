import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, within } from 'storybook/test';
import { InlineMessage } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { inlineMessageDocumentation } from './feedback.docs';

const meta = {
  title: 'Components/InlineMessage',
  component: InlineMessage,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'InlineMessage', maturity: 'beta', documentation: inlineMessageDocumentation }) } },
  args: { title: 'Проверьте принтер', children: 'Подключите бумагу и повторите печать.', tone: 'warning' }
} satisfies Meta<typeof InlineMessage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByRole('status')).toHaveTextContent('Проверьте принтер');
  }
};
export const Neutral: Story = { args: { tone: 'neutral', title: 'Ожидает проверки' } };
export const Info: Story = { args: { tone: 'info', title: 'Принтер подключен' } };
export const Success: Story = { args: { tone: 'success', title: 'Печать завершена' } };
export const Warning: Story = { args: { tone: 'warning', title: 'Нужна бумага' } };
export const Danger: Story = {
  args: { tone: 'danger', title: 'Печать остановлена' },
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByRole('alert')).toHaveTextContent('Печать остановлена');
  }
};
export const Monochrome: Story = { args: { tone: 'neutral', title: 'Проверено текстом и иконкой' } };
export const LongContent: Story = { args: { title: 'Проверьте маркировочные коды выбранной отгрузки перед запуском печати', children: 'Сообщение остается читаемым целиком и объясняет следующий шаг без скрытого жеста.' } };
export const English: Story = { globals: { locale: 'en' }, args: { title: 'Check the printer', children: 'Load paper and retry printing.' } };
export const ReducedMotion: Story = { parameters: { chromatic: { disableSnapshot: false } } };
