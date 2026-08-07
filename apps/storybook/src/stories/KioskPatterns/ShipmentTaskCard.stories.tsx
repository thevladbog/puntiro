import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { ShipmentTaskCard } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { longShipmentNumber, shipmentNumber, shipmentPlannedShipAt, shipmentReceivedAt } from '../fixtures';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { shipmentTaskCardDocumentation } from './shipmentTaskCard.docs';

const meta = {
  title: 'Kiosk Patterns/ShipmentTaskCard',
  component: ShipmentTaskCard,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'ShipmentTaskCard', maturity: 'beta', documentation: shipmentTaskCardDocumentation }) } },
  args: {
    shipmentNumber,
    receivedAt: shipmentReceivedAt,
    plannedShipAt: shipmentPlannedShipAt,
    status: 'ready',
    onOpen: fn()
  }
} satisfies Meta<typeof ShipmentTaskCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Ready: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const card = canvas.getByRole('button', { name: /Отгрузка SO-2026-000184/ });

    await expect(card).toHaveAttribute('data-kiosk-working-region', 'true');
    await expect(card).toHaveTextContent('SO-2026-000184');
    await expect(card).toHaveTextContent('Готово к печати');
    await userEvent.click(card);
    await expect(args.onOpen).toHaveBeenCalledOnce();
  }
};

export const UpdatedAfterPrint: Story = { args: { status: 'updated' } };
export const Attention: Story = { args: { status: 'attention' } };
export const LongNumber: Story = {
  args: { shipmentNumber: longShipmentNumber },
  play: async ({ canvasElement }) => {
    const card = within(canvasElement).getByRole('button', { name: new RegExp(longShipmentNumber) });
    await expect(card).toHaveTextContent(longShipmentNumber);
    await expect(card.scrollWidth).toBeLessThanOrEqual(card.clientWidth);
  }
};
export const Selected: Story = {
  args: { isSelected: true },
  play: async ({ canvasElement }) => {
    const card = within(canvasElement).getByRole('button', { name: /SO-2026-000184/ });
    await expect(card).toHaveAttribute('aria-pressed', 'true');
    await expect(card).toHaveTextContent('Выбрано');
  }
};
export const RU: Story = { globals: { locale: 'ru' } };
export const EN: Story = {
  globals: { locale: 'en' },
  play: async ({ canvasElement }) => {
    const card = within(canvasElement).getByRole('button', { name: /Shipment SO-2026-000184/ });
    await expect(card).toHaveTextContent('Ready to print');
  }
};
export const Touch: Story = {
  globals: { interactionMode: 'touch' },
  play: async ({ canvasElement }) => {
    const card = within(canvasElement).getByRole('button', { name: /SO-2026-000184/ });
    await expect(card.getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
  }
};
export const Standard: Story = {
  globals: { interactionMode: 'standard' },
  play: async ({ canvasElement }) => {
    const card = within(canvasElement).getByRole('button', { name: /SO-2026-000184/ });
    await expect(card.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
