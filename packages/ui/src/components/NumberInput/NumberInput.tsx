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
  const currentValueProps = numberValue === undefined || Number.isNaN(numberValue) ? {} : { 'aria-valuenow': numberValue };

  return <Input
    role="spinbutton"
    aria-valuemin={minValue}
    aria-valuemax={maxValue}
    {...currentValueProps}
    className={classes.input}
    onWheelCapture={(event) => {
      event.preventDefault();
      event.stopPropagation();
    }}
  />;
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
    isInvalid={fieldIsInvalid}
    isDisabled={isDisabled}
    className={classes.root}
  >
    <Label className={classes.label}>{label}</Label>
    <Group className={classes.group}>
      <AriaButton slot="decrement" aria-label={t('action.decrement')} className={classes.stepper}>
        <PuntiroIcon name="minus" />
      </AriaButton>
      <NumericInput minValue={minValue} maxValue={maxValue} />
      <AriaButton slot="increment" aria-label={t('action.increment')} className={classes.stepper}>
        <PuntiroIcon name="plus" />
      </AriaButton>
    </Group>
    {description === undefined ? null : <Text slot="description" className={classes.description}>{description}</Text>}
    {errorMessage === undefined ? null : <FieldError className={classes.error}>{errorMessage}</FieldError>}
  </NumberField>;
}
