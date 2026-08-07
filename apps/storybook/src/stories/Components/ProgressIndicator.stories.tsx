import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, within } from 'storybook/test';
import { ProgressIndicator } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { progressIndicatorDocumentation } from './feedback.docs';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';

const meta = {
  title: 'Components/ProgressIndicator',
  component: ProgressIndicator,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'ProgressIndicator', maturity: 'beta', documentation: progressIndicatorDocumentation }) } },
  args: { value: 2, max: 5, label: 'Печать этикеток' }
} satisfies Meta<typeof ProgressIndicator>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Partial: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const progress = canvas.getByRole('progressbar');
    await expect(progress).toHaveAttribute('aria-valuenow', '2');
    await expect(progress).toHaveAttribute('aria-valuemax', '5');
  }
};
export const Zero: Story = { args: { value: 0, max: 5, label: 'Подготовка к печати' } };
export const Complete: Story = { args: { value: 5, max: 5, label: 'Печать завершена' } };
export const CustomCounter: Story = { args: { counterText: '2 из 5 этикеток' } };
export const InvalidRange: Story = { args: { value: 8, max: 0, label: 'Печать этикеток' } };
export const LongRussianContent: Story = { args: { value: 2, max: 5, label: 'Печать маркировочных этикеток для выбранной отгрузки на участке упаковки', counterText: '2 из 5 этикеток для отгрузки' } };
export const LongEnglishContent: Story = { globals: { locale: 'en' }, args: { value: 2, max: 5, label: 'Printing marking labels for the selected shipment in the packing area', counterText: '2 of 5 shipment labels' } };
export const English: Story = { globals: { locale: 'en' }, args: { value: 2, max: 5, label: 'Printing labels', counterText: '2 of 5 labels' } };
export const ReducedMotion: Story = { parameters: reducedMotionParameters, play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement) };
