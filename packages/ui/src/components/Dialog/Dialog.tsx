import {
  Dialog as AriaDialog,
  DialogTrigger,
  Heading,
  Modal,
  ModalOverlay
} from 'react-aria-components';
import { useId, useState } from 'react';
import { Button } from '../Button/Button';
import type { DialogProps } from './Dialog.types';
import styles from './Dialog.module.css';

const classes = {
  overlay: styles.overlay!,
  modal: styles.modal!,
  dialog: styles.dialog!,
  title: styles.title!,
  description: styles.description!,
  content: styles.content!,
  actions: styles.actions!
};

export function Dialog({
  trigger,
  title,
  description,
  children,
  actions,
  isDismissible = true
}: DialogProps) {
  const [isOpen, setIsOpen] = useState(false);
  const descriptionId = useId();
  const descriptionProps = description === undefined ? {} : { 'aria-describedby': descriptionId };

  return <DialogTrigger
    isOpen={isOpen}
    onOpenChange={(nextIsOpen) => {
      if (nextIsOpen || isDismissible) setIsOpen(nextIsOpen);
    }}
  >
    {trigger}
    <ModalOverlay
      className={classes.overlay}
      data-testid="dialog-overlay"
      isDismissable={isDismissible}
      isKeyboardDismissDisabled={!isDismissible}
    >
      <Modal className={classes.modal}>
        <AriaDialog {...descriptionProps} className={classes.dialog}>
          {({ close }) => <>
            <Heading slot="title" className={classes.title}>{title}</Heading>
            {description === undefined ? null : <p id={descriptionId} className={classes.description}>{description}</p>}
            <div className={classes.content}>{children}</div>
            {actions.length === 0 ? null : <div className={classes.actions}>
              {actions.map((action) => <Button key={action.id} variant={action.variant} onPress={() => action.onPress(() => {
                setIsOpen(false);
                close();
              })}>
                {action.label}
              </Button>)}
            </div>}
          </>}
        </AriaDialog>
      </Modal>
    </ModalOverlay>
  </DialogTrigger>;
}
