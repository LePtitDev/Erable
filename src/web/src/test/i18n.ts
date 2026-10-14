import { createI18n } from "../shared/i18n/i18n";

/** Shared French i18n instance for component tests (the e2e default locale is also `fr`). */
export const testI18n = await createI18n("fr");
