import type { Meta, StoryObj } from '@storybook/react-vite';
import { Surface } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { surfaceDocumentation } from './feedback.docs';

const meta = {
  title: 'Components/Surface',
  component: Surface,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'Surface', maturity: 'beta', documentation: surfaceDocumentation }) } },
  args: { children: 'Место готово к следующему действию' }
} satisfies Meta<typeof Surface>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const Plain: Story = { args: { variant: 'plain' } };
export const Raised: Story = { args: { variant: 'raised' } };
export const Outlined: Story = { args: { variant: 'outlined' } };
export const Compact: Story = { args: { padding: 'compact' } };
export const Comfortable: Story = { args: { padding: 'comfortable' } };
export const Article: Story = { args: { as: 'article' } };
export const LongContent: Story = { args: { children: 'Проверьте маркировочные коды перед печатью: длинное сообщение остается полностью доступным и не требует скрытого жеста для чтения.' } };
export const ReducedMotion: Story = { parameters: { chromatic: { disableSnapshot: false } } };
