# CI

| Workflow | Trigger | Content |
|---|---|---|
| `pr.yml` | every branch push (not tags), every pull request, manual (`workflow_dispatch`) | see below |
| `release.yml` | `vX.Y.Z` or `X.Y.Z` tag (pushed, or created by a GitHub release), manual (build only, or catch-up of a release with *attach*) | 4 Velopack installers and erable-cli, Docker image + scan, publication (docs/release.md); no tests: the tagged commit has already gone through `pr.yml` |
| `nightly.yml` | every night, manual | full e2e on 3 browsers + a11y, visual (fixed container), budgets and benchmarks, desktop smoke test on 3 OSes; quarantine |
| `nightly-robustness.yml` | every night | crash tests (100 kills) and NBomber load |
| `weekly-quality.yml` | Sunday | Stryker.NET, StrykerJS, C# and TS fuzzing (10 min each) |
| `nightly-issue.yml` | failure of a scheduled workflow | opens (or adds to) a `ci-failure` issue |

`pr.yml` (target < 12 min, docs/testing.md §13), jobs in parallel:

1. **Workflow lint** — `actionlint` on `.github/workflows/`.
2. **Checks** (Linux, Debug) — `dotnet format --verify-no-changes`, build (analyzers, warnings = errors), .NET tests (Core, Storage, API); `typecheck`, `lint` (ESLint + stylelint), `format:check`, Vitest, API contract (`api:check`), SPA build + bundle budget (500 KB gzip), E2E traceability.
3. **Unit** (Windows, Linux, macOS, Release) — .NET build and tests in Release (proves that `/_test` is absent), Vitest.
4. **E2E** (Linux) — Playwright Chromium + WebKit, `@pr` tests, against the real backend.
5. **Desktop** (win-x64, linux-x64, osx-arm64, osx-x64) — self-contained publication + archive (`erable-desktop-<rid>` artifact), then a smoke test on the published binary: Playwright through CDP + `--self-test` on Windows, `--self-test` on Linux (`xvfb`) and macOS arm64; checks that no process remains. `osx-x64` is published without a smoke test.

Artifacts: TRX reports of each job; Playwright report and traces on failure (14 days).
Third-party actions are pinned by SHA (tag in a comment).

To run on a test branch: `git push -u origin <branch>`, or the *Actions* tab → *PR* → *Run workflow*.
