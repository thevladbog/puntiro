import {
  Button,
  FieldError,
  Label,
  ListBox,
  ListBoxItem,
  Popover,
  Select as AriaSelect,
  SelectStateContext,
  Text
} from 'react-aria-components';
import { useContext } from 'react';
import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { SelectProps } from './Select.types';
import styles from './Select.module.css';

const classes = {
  root: styles.root!,
  label: styles.label!,
  trigger: styles.trigger!,
  value: styles.value!,
  description: styles.description!,
  error: styles.error!,
  popover: styles.popover!,
  listBox: styles.listBox!,
  option: styles.option!,
  optionLabel: styles.optionLabel!,
  optionDescription: styles.optionDescription!
};

function SelectionValue({ items, placeholder }: { items: SelectProps['items']; placeholder: string | undefined }) {
  const state = useContext(SelectStateContext);
  const selectedItem = items.find((item) => item.id === state?.selectedKey);
  const value = selectedItem?.label ?? placeholder;

  return <span className={classes.value} data-placeholder={selectedItem === undefined ? true : undefined}>{value}</span>;
}

export function Select({
  label,
  items,
  selectedId,
  defaultSelectedId,
  onSelectionChange,
  placeholder,
  description,
  errorMessage,
  isInvalid = false,
  isDisabled = false
}: SelectProps) {
  const fieldIsInvalid = isInvalid || errorMessage !== undefined;
  const selectionProps = selectedId === undefined
    ? defaultSelectedId === undefined ? {} : { defaultSelectedKey: defaultSelectedId }
    : { selectedKey: selectedId };

  return <AriaSelect
    {...selectionProps}
    isDisabled={isDisabled}
    isInvalid={fieldIsInvalid}
    onSelectionChange={(id) => {
      if (id !== null) onSelectionChange?.(String(id));
    }}
    className={classes.root}
  >
    <Label className={classes.label}>{label}</Label>
    <Button className={classes.trigger}>
      <SelectionValue items={items} placeholder={placeholder} />
      <PuntiroIcon name="chevronDown" />
    </Button>
    {description === undefined ? null : <Text slot="description" className={classes.description}>{description}</Text>}
    {errorMessage === undefined ? null : <FieldError className={classes.error}>{errorMessage}</FieldError>}
    <Popover className={classes.popover} offset={8}>
      <ListBox className={classes.listBox}>
        {items.map((item) => <ListBoxItem key={item.id} id={item.id} textValue={item.label} aria-label={item.label} isDisabled={item.isDisabled ?? false} className={classes.option}>
          <span className={classes.optionLabel}>{item.label}</span>
          {item.description === undefined ? null : <span className={classes.optionDescription}>{item.description}</span>}
        </ListBoxItem>)}
      </ListBox>
    </Popover>
  </AriaSelect>;
}
