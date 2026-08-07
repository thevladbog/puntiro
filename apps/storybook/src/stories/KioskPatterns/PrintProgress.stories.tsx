import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, within } from 'storybook/test';
import { PrintProgress } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { longShipmentNumber, shipmentNumber } from '../fixtures';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { printProgressDocumentation } from './printProgress.docs';

const meta = {
  title: 'Kiosk Patterns/PrintProgress',
  component: PrintProgress,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'PrintProgress', maturity: 'beta', documentation: printProgressDocumentation }) } },
  args: { shipmentNumber, completed: 3, total: 5, printerName: 'Zebra ZD421' }
} satisfies Meta<typeof PrintProgress>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Printing: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const root = canvasElement.querySelector('[data-kiosk-working-region="true"]');
    await expect(root).toBeInTheDocument();
    await expect(canvas.getByText('3 из 5')).toBeVisible();
    await expect(canvas.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '3');
    await expect(canvas.getByRole('progressbar')).toHaveAttribute('aria-valuemax', '5');
    await expect(canvas.getByText('SO-2026-000184')).toBeVisible();
    await expect(canvas.getByText('Zebra ZD421')).toBeVisible();
  }
};
export const Partial: Story = {};
export const Completed: Story = {
  args: { completed: 5, total: 5 },
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByRole('status')).toHaveTextContent('Комплект напечатан');
  }
};
export const LongNumber: Story = {
  args: { shipmentNumber: longShipmentNumber },
  play: async ({ canvasElement }) => {
    const number = within(canvasElement).getByText(longShipmentNumber);
    await expect(number).toBeVisible();
    await expect(number.parentElement!.scrollWidth).toBeLessThanOrEqual(number.parentElement!.clientWidth);
  }
};
export const RU: Story = { globals: { locale: 'ru' } };
export const EN: Story = {
  globals: { locale: 'en' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByText('3 of 5')).toBeVisible();
    await expect(canvas.getByRole('heading', { name: 'Printing labels' })).toBeVisible();
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
