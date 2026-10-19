import { MoreHorizontal } from "lucide-react";
import { DropdownMenu } from "radix-ui";
import styles from "./RecentProjects.module.css";

export interface RowMenuItem {
  readonly label: string;
  readonly onSelect: () => void;
  readonly disabled?: boolean;
}

/** The « ⋯ » menu at the end of a row of the home screen (a recent project, a server). */
export function RowMenu({
  label,
  items,
}: {
  readonly label: string;
  readonly items: readonly RowMenuItem[];
}) {
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button type="button" className={styles["menuTrigger"]} aria-label={label} title={label}>
          <MoreHorizontal aria-hidden size={18} />
        </button>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content className={styles["menu"]} align="end" sideOffset={6}>
          {items.map((item) => (
            <DropdownMenu.Item
              key={item.label}
              className={styles["menuItem"]}
              disabled={item.disabled ?? false}
              onSelect={item.onSelect}
            >
              {item.label}
            </DropdownMenu.Item>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
