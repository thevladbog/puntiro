import type { Meta, StoryObj } from '@storybook/react-vite';
import { puntiroUiVersion } from '@puntiro/ui';

const meta = {
  title: 'Workspace/Ready',
  tags: ['test'],
  render: () => <main>Puntiro UI {puntiroUiVersion}</main>
} satisfies Meta;

export default meta;

type Story = StoryObj<typeof meta>;

export const Ready: Story = {};
