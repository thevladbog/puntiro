import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { EmptyState, ErrorState, LoadingState } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { systemStatesDocumentation } from './systemStates.docs';

const onClearSearch = fn();

const meta = {
  title: 'Kiosk Patterns/System states',
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'System states', maturity: 'beta', documentation: systemStatesDocumentation }) } }
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const NoJobs: Story = {
  render: () => <EmptyState title="Нет заданий на отгрузку" description="Новые задания появятся здесь после получения из внешней системы." />,
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('heading', { name: 'Нет заданий на отгрузку' })).toBeVisible();
    await expect(canvasElement.querySelector('[data-kiosk-working-region="true"]')).toBeInTheDocument();
  }
};
export const NoArchiveResults: Story = {
  render: () => <EmptyState title="Ничего не найдено" description="Измените номер отгрузки или очистите фильтр." action={{ label: 'Очистить поиск', onPress: onClearSearch }} />,
  play: async ({ canvasElement }) => {
    const action = within(canvasElement).getByRole('button', { name: 'Очистить поиск' });
    await expect(action).toHaveAttribute('data-primary-action', 'true');
    await userEvent.click(action);
    await expect(onClearSearch).toHaveBeenCalledOnce();
  }
};
export const Loading: Story = {
  render: () => <LoadingState label="Загружаем задания" />,
  play: async ({ canvasElement }) => {
    const status = within(canvasElement).getByRole('status', { name: 'Загружаем задания' });
    await expect(status).toBeVisible();
    await expect(status).toHaveAttribute('data-kiosk-working-region', 'true');
  }
};
export const RecoverableError: Story = {
  render: (_args, context) => <ErrorState
    title="Не удалось загрузить задания"
    description="Проверьте подключение и повторите попытку."
    recoveryAction={{ label: 'Повторить', onPress: context.args.onRecovery as () => void }}
  />,
  args: { onRecovery: fn() },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('alert')).toBeVisible();
    await expect(canvasElement.querySelector('[data-kiosk-working-region="true"]')).toBeInTheDocument();
    const recovery = canvas.getByRole('button', { name: 'Повторить' });
    await expect(recovery).toHaveAttribute('data-primary-action', 'true');
    await userEvent.click(recovery);
    await expect(args.onRecovery).toHaveBeenCalledOnce();
  }
};
export const PrinterUnavailable: Story = {
  render: () => <ErrorState title="Принтер недоступен" description="Проверьте питание и подключение выбранного принтера." />
};
export const EN: Story = {
  globals: { locale: 'en' },
  render: () => <EmptyState title="No shipments" description="New tasks will appear here when received from the external system." action={{ label: 'Refresh', onPress: fn() }} />,
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByRole('button', { name: 'Refresh' })).toBeVisible();
  }
};
export const TouchRecovery: Story = {
  globals: { interactionMode: 'touch' },
  render: () => <ErrorState
    title="Не удалось загрузить задания"
    description="Проверьте подключение и повторите попытку."
    recoveryAction={{ label: 'Повторить', onPress: fn() }}
  />,
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByRole('button', { name: 'Повторить' }).getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
  }
};
