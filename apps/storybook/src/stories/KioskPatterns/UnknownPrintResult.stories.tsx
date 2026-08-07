import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { UnknownPrintResult } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { longShipmentNumber, shipmentNumber } from '../fixtures';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { unknownPrintResultDocumentation } from './unknownPrintResult.docs';

const meta = {
  title: 'Kiosk Patterns/UnknownPrintResult',
  component: UnknownPrintResult,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'UnknownPrintResult', maturity: 'beta', documentation: unknownPrintResultDocumentation }) } },
  args: {
    shipmentNumber,
    placeCount: 5,
    onRetryAll: fn(),
    onSelectPlaces: fn(),
    onResolveWithoutReprint: fn()
  }
} satisfies Meta<typeof UnknownPrintResult>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const AwaitingDecision: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const root = canvasElement.querySelector('[data-kiosk-working-region="true"]')!;
    await expect(root).toBeInTheDocument();
    await expect(canvas.getByRole('heading', { name: 'Результат печати неизвестен' })).toBeVisible();
    await expect(root).not.toHaveAttribute('role', 'alert');
    await expect(canvas.queryByRole('alert')).toBeNull();
    await expect(canvas.getAllByRole('button')).toHaveLength(3);
    await expect(args.onRetryAll).not.toHaveBeenCalled();
    await expect(args.onSelectPlaces).not.toHaveBeenCalled();
    await expect(args.onResolveWithoutReprint).not.toHaveBeenCalled();
    const retry = canvas.getByRole('button', { name: 'Повторить весь комплект' });
    await expect(retry).toHaveAttribute('data-primary-action', 'true');
    await userEvent.click(retry);
    await expect(args.onRetryAll).toHaveBeenCalledOnce();
    await expect(args.onSelectPlaces).not.toHaveBeenCalled();
    await expect(args.onResolveWithoutReprint).not.toHaveBeenCalled();
  }
};
export const OnePlace: Story = { args: { placeCount: 1 } };
export const OneHundredPlaces: Story = { args: { placeCount: 100 } };
export const LongNumber: Story = {
  args: { shipmentNumber: longShipmentNumber },
  play: async ({ canvasElement }) => {
    const number = within(canvasElement).getByText(longShipmentNumber);
    await expect(number).toBeVisible();
    await expect(number.parentElement!.scrollWidth).toBeLessThanOrEqual(number.parentElement!.clientWidth);
  }
};
export const DisabledRepeatedActions: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const retry = canvas.getByRole('button', { name: 'Повторить весь комплект' });
    await userEvent.click(retry);
    for (const button of canvas.getAllByRole('button')) await expect(button).toBeDisabled();
    await userEvent.click(canvas.getByRole('button', { name: 'Выбрать места' }));
    await expect(args.onRetryAll).toHaveBeenCalledOnce();
    await expect(args.onSelectPlaces).not.toHaveBeenCalled();
  }
};
export const SelectPlaces: Story = {
  play: async ({ canvasElement, args }) => {
    await userEvent.click(within(canvasElement).getByRole('button', { name: 'Выбрать места' }));
    await expect(args.onSelectPlaces).toHaveBeenCalledOnce();
    await expect(args.onRetryAll).not.toHaveBeenCalled();
    await expect(args.onResolveWithoutReprint).not.toHaveBeenCalled();
  }
};
export const ResolveWithoutReprint: Story = {
  play: async ({ canvasElement, args }) => {
    await userEvent.click(within(canvasElement).getByRole('button', { name: 'Продолжить без повторной печати' }));
    await expect(args.onResolveWithoutReprint).toHaveBeenCalledOnce();
    await expect(args.onRetryAll).not.toHaveBeenCalled();
    await expect(args.onSelectPlaces).not.toHaveBeenCalled();
  }
};
export const TouchChoices: Story = {
  globals: { interactionMode: 'touch' },
  play: async ({ canvasElement }) => {
    for (const button of within(canvasElement).getAllByRole('button')) {
      await expect(button.getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
    }
  }
};
export const RU: Story = { globals: { locale: 'ru' } };
export const EN: Story = {
  globals: { locale: 'en' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('heading', { name: 'Print result unknown' })).toBeVisible();
    await expect(canvas.getByRole('button', { name: 'Retry the entire set' })).toBeVisible();
    await expect(canvas.getByRole('button', { name: 'Select places' })).toBeVisible();
    await expect(canvas.getByRole('button', { name: 'Continue without reprinting' })).toBeVisible();
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
