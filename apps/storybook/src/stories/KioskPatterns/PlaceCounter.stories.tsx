import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, fn, userEvent, within } from 'storybook/test';
import { PlaceCounter } from '@puntiro/ui';
import { createDocsPage } from '../../docs/ComponentDocsPage';
import { expectReducedMotionEnvironment, reducedMotionParameters } from '../reducedMotion';
import { placeCounterDocumentation } from './placeCounter.docs';

const meta = {
  title: 'Kiosk Patterns/PlaceCounter',
  component: PlaceCounter,
  tags: ['test'],
  parameters: { docs: { page: createDocsPage({ title: 'PlaceCounter', maturity: 'beta', documentation: placeCounterDocumentation }) } },
  args: {
    value: 1,
    minValue: 1,
    maxValue: 100,
    onChange: fn()
  },
  render: (args) => {
    const [value, setValue] = useState(args.value);
    return <PlaceCounter
      {...args}
      value={value}
      onChange={(nextValue) => {
        setValue(nextValue);
        args.onChange(nextValue);
      }}
    />;
  }
} satisfies Meta<typeof PlaceCounter>;

export default meta;
type Story = StoryObj<typeof meta>;

export const TouchInteraction: Story = {
  args: { maxValue: 2 },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const root = canvasElement.querySelector('[data-kiosk-working-region="true"]');

    await expect(root).toBeInTheDocument();
    await userEvent.click(canvas.getByRole('button', { name: /^Увеличить/ }));
    await expect(canvas.getByText('2 из 2')).toBeVisible();
    await expect(args.onChange).toHaveBeenLastCalledWith(2);
  }
};

export const One: Story = { args: { value: 1, maxValue: 100 } };
export const Ten: Story = { args: { value: 10, maxValue: 100 } };
export const OneHundred: Story = {
  args: { value: 100, maxValue: 100 },
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByRole('button', { name: /^Увеличить/ })).toBeDisabled();
  }
};
export const Disabled: Story = {
  args: { value: 5, maxValue: 10, isDisabled: true },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('spinbutton')).toBeDisabled();
    for (const button of canvas.getAllByRole('button')) await expect(button).toBeDisabled();
  }
};
export const RU: Story = { globals: { locale: 'ru' }, args: { value: 5, maxValue: 10 } };
export const EN: Story = {
  globals: { locale: 'en' },
  args: { value: 5, maxValue: 10 },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByText('5 of 10')).toBeVisible();
    await expect(canvas.getByRole('spinbutton', { name: 'Number of places' })).toBeVisible();
  }
};
export const Touch: Story = {
  globals: { interactionMode: 'touch' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    for (const control of [canvas.getByRole('spinbutton'), ...canvas.getAllByRole('button')]) {
      await expect(control.getBoundingClientRect().height).toBeGreaterThanOrEqual(64);
    }
  }
};
export const Standard: Story = {
  globals: { interactionMode: 'standard' },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    for (const control of [canvas.getByRole('spinbutton'), ...canvas.getAllByRole('button')]) {
      await expect(control.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
    }
  }
};
export const ReducedMotion: Story = {
  parameters: reducedMotionParameters,
  play: ({ canvasElement }) => expectReducedMotionEnvironment(canvasElement)
};
