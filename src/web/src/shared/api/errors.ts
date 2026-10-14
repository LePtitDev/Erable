import type { TFunction } from "i18next";
import { ApiError } from "./client";

/**
 * User-facing message of a failed call: the translation of the stable business code (`errors.<code>`) when there is
 * one, a generic message otherwise. Technical details never reach the screen (E2E-03-03).
 */
export function errorMessage(t: TFunction, error: unknown): string {
  if (error instanceof ApiError) {
    const code = error.problem?.["code"];
    if (typeof code === "string") {
      // Codes are data from the backend: the key cannot be checked statically, its existence is checked at runtime.
      const key = `errors.${code}` as "errors.generic";
      if (t(key, { defaultValue: "" }) !== "") {
        return t(key);
      }
    }
    return t("errors.generic");
  }
  return t("errors.network");
}

/** The stable business code of a failed call, if any. */
export function errorCode(error: unknown): string | undefined {
  const code = error instanceof ApiError ? error.problem?.["code"] : undefined;
  return typeof code === "string" ? code : undefined;
}
