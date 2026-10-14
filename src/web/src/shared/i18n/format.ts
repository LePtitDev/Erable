import { createContext, useContext, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { pseudoLanguage, referenceLanguage } from "./languages";

/** Injectable clock: tests and e2e freeze it (docs/features.md §1.4). */
export type Clock = () => Date;

export const ClockContext = createContext<Clock>(() => new Date());

const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 365 * 24 * 3600],
  ["month", 30 * 24 * 3600],
  ["week", 7 * 24 * 3600],
  ["day", 24 * 3600],
  ["hour", 3600],
  ["minute", 60],
  ["second", 1],
];

/** Date and number formatting in the interface language, through `Intl`. */
export function createFormatters(language: string, clock: Clock) {
  const locale = language === pseudoLanguage ? referenceLanguage : language;
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "long" });
  const dateTime = new Intl.DateTimeFormat(locale, { dateStyle: "long", timeStyle: "short" });
  const relative = new Intl.RelativeTimeFormat(locale, { numeric: "auto", style: "short" });
  const number = new Intl.NumberFormat(locale);

  return {
    /** « 12 mars 2026 » */
    date: (value: Date) => date.format(value),
    /** « 12 mars 2026 à 10:00 » */
    dateTime: (value: Date) => dateTime.format(value),
    /** « il y a 2 h », « hier », « dans 3 j » */
    relative: (value: Date) => {
      const seconds = Math.round((value.getTime() - clock().getTime()) / 1000);
      const [unit, size] = units.find(([, s]) => Math.abs(seconds) >= s) ?? ["second", 1];
      return relative.format(Math.round(seconds / size), unit);
    },
    /** « 840,5 » */
    number: (value: number, options?: Intl.NumberFormatOptions) =>
      options === undefined
        ? number.format(value)
        : new Intl.NumberFormat(locale, options).format(value),
  };
}

export type Formatters = ReturnType<typeof createFormatters>;

export function useFormatters(): Formatters {
  const { i18n } = useTranslation();
  const clock = useContext(ClockContext);
  return useMemo(() => createFormatters(i18n.language, clock), [i18n.language, clock]);
}
