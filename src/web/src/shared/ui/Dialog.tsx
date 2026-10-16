import { Dialog as Radix } from "radix-ui";
import { useState, type ReactNode } from "react";
import styles from "./Dialog.module.css";

interface DialogProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly title: string;
  readonly description?: string;
  readonly children?: ReactNode;
  /** Actions, right-aligned under the content. */
  readonly footer?: ReactNode;
  readonly size?: "sm" | "md" | "lg";
  /** Role `alertdialog` for confirmations (E2E-17-03): Escape still closes it. */
  readonly alert?: boolean;
}

/**
 * The shared modal dialog (E2E-17-03): focus trapped inside, Escape closes, focus returns to the element that opened
 * it. Built on Radix, which handles all three plus `aria-modal` and scroll locking.
 */
export function Dialog({
  open,
  onOpenChange,
  title,
  description,
  children,
  footer,
  size = "md",
  alert = false,
}: DialogProps) {
  // Controlled dialogs have no Radix trigger to return to: remember what had the focus when it opened. Read during
  // the render that opens it, before `autoFocus` or Radix move the focus inside.
  const [opener, setOpener] = useState<HTMLElement | null>(null);
  const [wasOpen, setWasOpen] = useState(false);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open)
      setOpener(document.activeElement instanceof HTMLElement ? document.activeElement : null);
  }

  return (
    <Radix.Root open={open} onOpenChange={onOpenChange}>
      <Radix.Portal>
        <Radix.Overlay className={styles["overlay"]} />
        <Radix.Content
          className={`${styles["content"] ?? ""} ${styles[size] ?? ""}`}
          role={alert ? "alertdialog" : "dialog"}
          onCloseAutoFocus={(event) => {
            if (opener?.isConnected) {
              event.preventDefault();
              opener.focus();
            }
          }}
          {...(description === undefined ? { "aria-describedby": undefined } : {})}
        >
          <Radix.Title className={styles["title"]}>{title}</Radix.Title>
          {description !== undefined && (
            <Radix.Description className={styles["description"]}>{description}</Radix.Description>
          )}
          {children}
          {footer !== undefined && <div className={styles["footer"]}>{footer}</div>}
        </Radix.Content>
      </Radix.Portal>
    </Radix.Root>
  );
}
