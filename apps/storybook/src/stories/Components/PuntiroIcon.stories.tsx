import type { Meta, StoryObj } from '@storybook/react-vite';
import { PuntiroIcon } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { iconDocumentation } from './icon.docs';

const meta = {
  title: 'Components/PuntiroIcon',
  component: PuntiroIcon,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'PuntiroIcon', maturity: 'beta', documentation: iconDocumentation }) } },
  args: { name: 'printer' }
} satisfies Meta<typeof PuntiroIcon>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const AllApprovedIcons: Story = {
  render: () => <div>{(['archive', 'check', 'chevronDown', 'close', 'connection', 'error', 'minus', 'plus', 'printer', 'refresh', 'search', 'warning'] as const).map((name) => <PuntiroIcon key={name} name={name} />)}</div>
};
