import {
  Button as AriaButton,
  FieldError,
  Group,
  Input,
  Label,
  NumberField,
  NumberFieldStateContext,
  Text
} from 'react-aria-components';
import { useContext } from 'react';
import { PuntiroIcon } from '../../icons/PuntiroIcon';
import { usePuntiro } from '../../provider/usePuntiro';
import type { NumberInputProps } from './NumberInput.types';
import styles from './NumberInput.module.css';

const classes = {
  root: styles.root!,
  label: styles.label!,
  group: styles.group!,
  stepper: styles.stepper!,
  input: styles.input!,
  description: styles.description!,
  error: styles.error!
};

function NumericInput({ minValue, maxValue }: Pick<Required<NumberInputProps>, 'minValue' | 'maxValue'>) {
  const state = useContext(NumberFieldStateContext);
  const numberValue = state?.numberValue;
  const inputValue = state?.inputValue;
  const currentValueProps = numberValue === undefined || Number.isNaN(numberValue)
    ? {}
    : { 'aria-valuenow': numberValue, 'aria-valuetext': inputValue };

  return <Input
    role="spinbutton"
    aria-valuemin={minValue}
    aria-valuemax={maxValue}
    {...currentValueProps}
    className={classes.input}
  />;
}

function Stepper({
  direction,
  minValue,
  maxValue,
  step,
  isDisabled,
  label
}: {
  direction: 'decrement' | 'increment';
  minValue: number;
  maxValue: number;
  step: number;
  isDisabled: boolean;
  label: string;
}) {
  const state = useContext(NumberFieldStateContext);
  const value = state?.numberValue;
  const atBound = value !== undefined && (direction === 'increment' ? value >= maxValue : value <= minValue);
  const isStepperDisabled = isDisabled || atBound;
  const icon = direction === 'increment' ? 'plus' : 'minus';

  const clampToBound = () => {
    const currentValue = state?.numberValue;
    const crossesBound = currentValue !== undefined && Number.isFinite(currentValue)
      && (direction === 'increment' ? currentValue + step > maxValue : currentValue - step < minValue);
    if (!crossesBound) return;
    state?.setNumberValue(direction === 'increment' ? maxValue : minValue);
  };

  return <AriaButton slot={direction} aria-label={label} isDisabled={isStepperDisabled} onPress={clampToBound} className={classes.stepper}>
    <PuntiroIcon name={icon} />
  </AriaButton>;
}

function assertFiniteValue(name: string, value: number): void {
  if (!Number.isFinite(value)) throw new RangeError(`NumberInput ${name} must be finite.`);
}

export function NumberInput({
  label,
  value,
  defaultValue,
  onChange,
  minValue = 1,
  maxValue = 100,
  step = 1,
  description,
  errorMessage,
  isInvalid = false,
  isDisabled = false
}: NumberInputProps) {
  const { t } = usePuntiro();

  assertFiniteValue('minValue', minValue);
  assertFiniteValue('maxValue', maxValue);
  if (value !== undefined) assertFiniteValue('value', value);
  if (defaultValue !== undefined) assertFiniteValue('defaultValue', defaultValue);
  if (!Number.isFinite(step) || step <= 0) throw new RangeError('NumberInput step must be finite and greater than zero.');
  if (minValue > maxValue) {
    throw new RangeError('NumberInput minValue must be less than or equal to maxValue.');
  }

  const valueProps = value === undefined ? {} : { value };
  const defaultValueProps = value === undefined && defaultValue !== undefined ? { defaultValue } : {};
  const changeProps = onChange === undefined ? {} : { onChange };
  const fieldIsInvalid = isInvalid || errorMessage !== undefined;

  return <NumberField
    {...valueProps}
    {...defaultValueProps}
    {...changeProps}
    minValue={minValue}
    maxValue={maxValue}
    step={step}
    isWheelDisabled
    isInvalid={fieldIsInvalid}
    isDisabled={isDisabled}
    className={classes.root}
  >
    {() => <>
      <Label className={classes.label}>{label}</Label>
      <Group className={classes.group}>
        <Stepper direction="decrement" minValue={minValue} maxValue={maxValue} step={step} isDisabled={isDisabled} label={t('action.decrement')} />
        <NumericInput minValue={minValue} maxValue={maxValue} />
        <Stepper direction="increment" minValue={minValue} maxValue={maxValue} step={step} isDisabled={isDisabled} label={t('action.increment')} />
      </Group>
      {description === undefined ? null : <Text slot="description" className={classes.description}>{description}</Text>}
      {errorMessage === undefined ? null : <FieldError className={classes.error}>{errorMessage}</FieldError>}
    </>}
  </NumberField>;
}
