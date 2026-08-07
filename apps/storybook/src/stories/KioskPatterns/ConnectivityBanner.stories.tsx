import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, within } from 'storybook/test';
import { ConnectivityBanner } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { connectivityLastSyncedAt } from '../fixtures';
import { connectivityBannerDocumentation } from './connectivityBanner.docs';

const meta = {
  title: 'Kiosk Patterns/ConnectivityBanner',
  component: ConnectivityBanner,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'ConnectivityBanner', maturity: 'beta', documentation: connectivityBannerDocumentation }) } },
  args: { state: 'online', lastSyncedAt: connectivityLastSyncedAt }
} satisfies Meta<typeof ConnectivityBanner>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Online: Story = {
  play: async ({ canvasElement }) => {
    const banner = within(canvasElement).getByRole('status');
    await expect(banner).toHaveAttribute('data-kiosk-working-region', 'true');
    await expect(banner).toHaveTextContent('Подключение активно');
    await expect(banner).toHaveTextContent('Последняя синхронизация');
  }
};

export const OfflineAllowed: Story = {
  args: { state: 'offlineAllowed', offlineRemainingSeconds: 61 },
  play: async ({ canvasElement }) => {
    const banner = within(canvasElement).getByRole('status');
    await expect(banner).toHaveTextContent('Нет подключения');
    await expect(banner).toHaveTextContent('Можно работать офлайн ещё 2 мин.');
  }
};

export const OfflineExpired: Story = {
  args: { state: 'offlineExpired', offlineRemainingSeconds: 61 },
  play: async ({ canvasElement }) => {
    const banner = within(canvasElement).getByRole('status');
    await expect(banner).toHaveTextContent('Офлайн-лимит истёк');
  }
};

export const RU: Story = { globals: { locale: 'ru' } };
export const EN: Story = {
  globals: { locale: 'en' },
  args: { state: 'offlineAllowed', offlineRemainingSeconds: 61 },
  play: async ({ canvasElement }) => {
    const banner = within(canvasElement).getByRole('status');
    await expect(banner).toHaveTextContent('Offline: 2 min remaining');
    await expect(banner).toHaveTextContent('Last sync');
  }
};
