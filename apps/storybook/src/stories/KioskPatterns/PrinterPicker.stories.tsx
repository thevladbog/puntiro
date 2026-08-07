import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { PrinterPicker } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { printerPickerDocumentation } from './printerPicker.docs';

const mixedPrinters = [
  { id: 'zebra-zd421', name: 'Zebra ZD421', language: 'zpl', state: 'ready' },
  { id: 'tsc-te200', name: 'TSC TE200', language: 'tspl', state: 'busy' },
  { id: 'bixolon-xd5', name: 'Bixolon XD5-40d', language: 'zpl', state: 'offline' },
  { id: 'tsc-mh241', name: 'TSC MH241', language: 'tspl', state: 'error' }
] as const;

const readyPrinters = [
  { id: 'zebra-zd421', name: 'Zebra ZD421', language: 'zpl', state: 'ready' },
  { id: 'tsc-te200', name: 'TSC TE200', language: 'tspl', state: 'ready' }
] as const;

const longWindowsQueueName = 'WAREHOUSE-WINDOWS-PRINT-SERVER \\ Shipping labels \\ Zebra ZD421 receiving dock number twelve';

const meta = {
  title: 'Kiosk Patterns/PrinterPicker',
  component: PrinterPicker,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'PrinterPicker', maturity: 'beta', documentation: printerPickerDocumentation }) } },
  args: {
    printers: mixedPrinters,
    onSelectionChange: fn()
  },
  render: (args) => {
    const [selectedId, setSelectedId] = useState(args.selectedId);
    return <PrinterPicker
      {...args}
      selectedId={selectedId}
      onSelectionChange={(id) => {
        setSelectedId(id);
        args.onSelectionChange(id);
      }}
    />;
  }
} satisfies Meta<typeof PrinterPicker>;

export default meta;
type Story = StoryObj<typeof meta>;

export const TouchSelection: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const zebra = canvas.getByRole('radio', { name: 'Zebra ZD421 — ZPL' });
    const busy = canvas.getByRole('radio', { name: 'TSC TE200 — TSPL' });
    const offline = canvas.getByRole('radio', { name: 'Bixolon XD5-40d — ZPL' });
    const error = canvas.getByRole('radio', { name: 'TSC MH241 — TSPL' });

    await expect(canvas.getByRole('radiogroup')).toHaveAttribute('data-kiosk-working-region', 'true');
    await expect(busy).toBeDisabled();
    await expect(offline).toBeDisabled();
    await expect(error).toBeDisabled();
    await expect(zebra).toHaveAccessibleDescription('Готов');
    await expect(offline).toHaveAccessibleDescription('Не в сети');
    await userEvent.click(zebra);
    await expect(zebra).toBeChecked();
    await expect(args.onSelectionChange).toHaveBeenLastCalledWith('zebra-zd421');
  }
};

export const OnePrinter: Story = { args: { printers: readyPrinters.slice(0, 1) } };
export const MultiplePrinters: Story = { args: { printers: readyPrinters } };
export const MultipleReady: Story = { args: { printers: readyPrinters } };
export const MixedZplTspl: Story = { args: { printers: mixedPrinters } };
export const Busy: Story = { args: { printers: mixedPrinters.slice(1, 2) } };
export const Offline: Story = { args: { printers: mixedPrinters.slice(2, 3) } };
export const Error: Story = { args: { printers: mixedPrinters.slice(3, 4) } };
export const NoPrinters: Story = {
  args: { printers: [] },
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByText('Принтеры не найдены')).toBeVisible();
  }
};
export const LongWindowsQueueName: Story = {
  args: {
    printers: [{
      id: 'warehouse-long-queue',
      name: longWindowsQueueName,
      language: 'zpl',
      state: 'ready'
    }]
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const option = canvas.getByRole('radio', { name: `${longWindowsQueueName} — ZPL` });
    const target = option.closest('label')!;
    await expect(canvas.getByText(longWindowsQueueName)).toBeVisible();
    await expect(target.scrollWidth).toBeLessThanOrEqual(target.clientWidth);
  }
};
export const KeyboardArrows: Story = {
  args: { printers: readyPrinters },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const zebra = canvas.getByRole('radio', { name: 'Zebra ZD421 — ZPL' });
    const tsc = canvas.getByRole('radio', { name: 'TSC TE200 — TSPL' });

    await userEvent.click(zebra);
    await userEvent.keyboard('{ArrowDown}');
    await expect(tsc).toBeChecked();
    await expect(args.onSelectionChange).toHaveBeenLastCalledWith('tsc-te200');
  }
};
export const RU: Story = { globals: { locale: 'ru' } };
export const EN: Story = {
  globals: { locale: 'en' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('radiogroup', { name: 'Select a printer' })).toBeVisible();
    await expect(canvas.getByText('Ready')).toBeVisible();
  }
};
export const Touch: Story = {
  globals: { interactionMode: 'touch' },
  args: { printers: readyPrinters },
  play: async ({ canvasElement }) => {
    for (const option of within(canvasElement).getAllByRole('radio')) {
      await expect(option.closest('label')!.getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
    }
  }
};
export const Standard: Story = {
  globals: { interactionMode: 'standard' },
  args: { printers: readyPrinters },
  play: async ({ canvasElement }) => {
    for (const option of within(canvasElement).getAllByRole('radio')) {
      await expect(option.closest('label')!.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
    }
  }
};
export const FocusVisible: Story = {
  args: { printers: readyPrinters },
  play: async ({ canvasElement }) => {
    const radio = within(canvasElement).getByRole('radio', { name: 'Zebra ZD421 — ZPL' });
    await userEvent.tab();
    await expect(radio).toHaveFocus();
    await expect(getComputedStyle(radio.closest('label')!).outlineStyle).toBe('solid');
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
