import type { ButtonHTMLAttributes } from "react";
import styles from "./Button.module.css";

type Variant = "primary" | "secondary" | "ghost" | "danger";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  readonly variant?: Variant;
  readonly size?: "sm" | "md";
}

/** The button of the design system; at least 32 px high (docs/architecture.md §3.1). */
export function Button({
  variant = "secondary",
  size = "md",
  className,
  type = "button",
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={[styles["button"], styles[variant], styles[size], className]
        .filter(Boolean)
        .join(" ")}
      {...rest}
    />
  );
}
