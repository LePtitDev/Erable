import { useRef, type KeyboardEvent, type PointerEvent } from "react";
import styles from "./Splitter.module.css";

interface SplitterProps {
  readonly label: string;
  /** ID of the panel it resizes. */
  readonly controls: string;
  readonly value: number;
  readonly min: number;
  readonly max: number;
  readonly onChange: (value: number) => void;
}

const step = 16;

/** Resize handle of a panel: drag, or ← → (Home / End) once focused (WAI-ARIA window splitter). */
export function Splitter({ label, controls, value, min, max, onChange }: SplitterProps) {
  const start = useRef<{ x: number; value: number } | null>(null);
  const clamp = (v: number) => Math.round(Math.min(max, Math.max(min, v)));

  const onKeyDown = (event: KeyboardEvent) => {
    const next =
      event.key === "ArrowLeft"
        ? value - step
        : event.key === "ArrowRight"
          ? value + step
          : event.key === "Home"
            ? min
            : event.key === "End"
              ? max
              : null;
    if (next !== null) {
      event.preventDefault();
      onChange(clamp(next));
    }
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    start.current = { x: event.clientX, value };
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const onPointerMove = (event: PointerEvent) => {
    if (start.current !== null) {
      onChange(clamp(start.current.value + event.clientX - start.current.x));
    }
  };

  return (
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label={label}
      aria-controls={controls}
      aria-valuenow={value}
      aria-valuemin={min}
      aria-valuemax={max}
      tabIndex={0}
      className={styles["splitter"]}
      onKeyDown={onKeyDown}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={() => {
        start.current = null;
      }}
    />
  );
}
