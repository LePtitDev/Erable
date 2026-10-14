import type { i18n as I18n } from "i18next";
import type { components } from "../api/schema";
import { api, unwrap } from "../shared/api/client";
import { selfTestSteps, type SelfTestContext, type SelfTestStepDefinition } from "./steps";

type SelfTestStep = components["schemas"]["SelfTestStep"];

/** Runs every step (stopping at the first failure) and reports to the shell, which exits with 0 or 1. */
export async function runSelfTest(
  i18n: I18n,
  steps: readonly SelfTestStepDefinition[] = selfTestSteps,
): Promise<void> {
  const context: SelfTestContext = { i18n, document };
  const results: SelfTestStep[] = [];
  for (const step of steps) {
    const start = performance.now();
    try {
      const detail = await step.run(context);
      results.push({
        name: step.name,
        ok: true,
        durationMs: performance.now() - start,
        detail: detail ?? null,
      });
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      results.push({ name: step.name, ok: false, durationMs: performance.now() - start, detail });
      break;
    }
  }
  await unwrap(
    api.POST("/api/self-test/report", {
      body: { steps: results, readyAtMs: context.readyAtMs ?? performance.now() },
    }),
  );
}
