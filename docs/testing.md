# Erable — Quality and testing strategy

## 1. Principles

1. **Test where a bug is expensive.** What is irreversible comes first: data loss, broken reference, a merge that overwrites text, data leaking to the wrong user. A misaligned button is fixed in two minutes; those bugs are not.
2. **Logic goes into `Erable.Core`, pure and exhaustively tested.** The rest (storage, API, UI) stays as thin as possible and is tested through integration. The architecture was designed for this: Core has no IO dependency (§A-3).
3. **No mocks of what we own.** Tests run against a real SQLite (temporary file), a real in-memory Kestrel server, a real browser. Only what is external or slow is simulated.
4. **One specification for two implementations.** Everything that exists in C# *and* in TypeScript (Markdown syntax, validation rules, ID format) is driven by the same case files in `spec/fixtures/`.
5. **Every fixed bug leaves a test.** The failing test is written first, then the fix.
6. **Deterministic tests or no tests.** A flaky test is quarantined within the day, then fixed or deleted. It is never left "to be rerun".

---

## 2. Overview

| Level | Tools | What it covers | Volume | Target duration |
|---|---|---|---|---|
| Static analysis | C# compiler (nullable, warnings = errors), .NET analyzers, `tsc --strict`, ESLint | Whole classes of bugs, before running | — | < 1 min |
| Core unit tests | xUnit v3, Shouldly, **CsCheck** (property-based) | Domain: IDs, parser, rules, inheritance, merge | many | < 30 s |
| Shared specification | `spec/fixtures/*.json` run by xUnit **and** Vitest | C# ↔ TS parity | medium | included |
| Storage | xUnit + real SQLite in a temporary folder | Migrations, queries, FTS, blobs, integrity | medium | < 1 min |
| API / integration | `WebApplicationFactory`, real HTTP and SignalR clients | Endpoints, auth, concurrency, events | medium | < 2 min |
| Frontend unit tests | Vitest, Testing Library, **fast-check**, MSW | Components, CodeMirror extensions, stores | many | < 1 min |
| End-to-end | **Playwright** (TypeScript) against a real backend | Complete user journeys, multi-user | few, targeted | < 8 min (PR) |
| Visual and accessibility | Playwright `toHaveScreenshot`, `@axe-core/playwright` | Light/dark themes, languages, contrast, keyboard | few | nightly |
| Desktop | Smoke test on the packaged binary, 3 OSes | Startup, WebView, loopback, native files | very few | < 3 min / OS |
| Performance | BenchmarkDotNet, Playwright budgets, NBomber | Response and rendering times on a large project | targeted | nightly |
| Robustness | crash tests, mutation testing (Stryker), fuzzing | What the other levels do not see | targeted | nightly / weekly |

---

## 3. Static analysis and typing

The cheapest way to avoid a bug is for it not to compile.

**C#**
- `Nullable` enabled, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, shared `.editorconfig`.
- `ElementId` is a dedicated type (§A-4): a `long` or a `string` cannot be passed instead of an ID.
- One `enum` per `ElementKind` and per property type, exhaustive `switch` (warning CS8509 → error).

**TypeScript**
- `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`.
- ESLint `typescript-eslint/strict-type-checked`. `any` forbidden outside generated files.
- **`ElementId` is a branded type** (`string & { __brand: "ElementId" }`), obtainable only through `parseElementId()`. This makes the "ID as a `number`" mistake (§A-4) impossible to write.
- The API client is **generated** from the OpenAPI document (§A-3.1). CI regenerates it and fails if the versioned file differs: an API change not carried over to the frontend breaks the build instead of breaking the application.

---

## 4. Domain unit tests (`Erable.Core`)

This is where most of the reliability is won. Target: **90 % line coverage and a mutation score ≥ 70 %** on Core (§12). Both figures together: coverage alone does not prove that the assertions check anything.

### 4.1 Property-based tests

Rather than a few hand-picked examples, an invariant is described and **CsCheck** generates thousands of inputs (and automatically shrinks a failure to the smallest case). On the TS side, **fast-check** does the same.

| Subject | Invariants checked |
|---|---|
| `ElementId` | `Parse(Format(id)) == id` for every `ulong ≠ 0`; lower case accepted; `long ↔ ulong` lossless; 0 and lengths ≠ 16 rejected |
| Reference parser | Any generated reference inserted into arbitrary Markdown is found, at the right position; the text outside references is preserved; no exception on random input |
| Validation rules | `between(a,b)` ⇔ `greater_or_equal(a) ∧ less_or_equal(b)`; a valid value stays valid after a JSON round trip |
| Category inheritance | No cycle accepted, whatever the sequence of reparentings; resolving a value does not depend on the declaration order of the categories |
| Three-way merge | `merge(b, b, x) == x`; `merge(b, x, b) == x`; two edits of disjoint sections → no conflict and both present; **no character lost without a reported conflict** |
| Tree | After any sequence of valid moves: one parent per element, no loop, no element outside its root |

### 4.2 Example-based tests

For readable business cases, in particular everything the reference project illustrates:
- PNJ inherits from Personnage, which inherits from Entité: the value comes from the most specific category.
- Ilsa is PNJ + Marchand, two different defaults for the same property: the value is marked **in conflict** (§A-5.3).
- `Niveau = 72` with `less_or_equal 60`: a warning, and saving is **not** blocked (§A-5.4).

Naming: `Method_Context_ExpectedResult`. A test checks a single behaviour.

### 4.3 Test data

- **Fluent builders**: `Project.Build().WithCategory("PNJ", parent: "Personnage").WithPage("Ilsa Varenne", c => c.InCategory("PNJ"))`. Tests stay readable and do not depend on construction details.
- **"Chroniques d'Aube" demo project**, versioned in `spec/projects/`. It is at once the e2e data set, the base of the visual snapshots and the demo. A single test world that everyone knows.
- **Large project generator** with a fixed seed (`--pages 10000 --seed 42`), for performance (§11).

---

## 5. Shared C# ↔ TypeScript specification

Three pieces of logic exist twice (§A-5.4, §A-6.3): reference parser, validation rules, ID format. They are described once:

```
spec/fixtures/
├─ ids.json            { "input": "3f2a9c14e07b5d21", "valid": true, "canonical": "3F2A9C14E07B5D21" }
├─ rules/              evaluation.json { "type": "int", "rules": [{"op":"less_or_equal","value":60}], "value": 72, "violations": [{"op":"less_or_equal"}] }
│                      definitions.json, values.json, regex.json
└─ markdown/
   ├─ refs.json        { "md": "Voir [[3F2A9C14E07B5D21|lui]].", "refs": [{ "id": "…", "label": "lui", "embed": false, "start": 5 }] }
   └─ edge-cases.json  refs in code, escapes, nested brackets, IDs of 15/17 characters…
```

- A **single runner** per language reads every file; adding a case means adding a JSON line, without code.
- Both suites run in the same CI: if one passes and the other fails, the divergence is visible immediately.
- Cases found by property-based testing (§4.1) are added here, so that the other implementation benefits from them.

---

## 6. Storage

Always against a **real SQLite**, in a temporary folder per test (fast: a few milliseconds).

- **Migrations**: for every published `formatVersion`, a frozen project is kept in `spec/projects/legacy/vN/`. The test opens it, migrates it to the current version, checks `PRAGMA integrity_check` and the content. **An existing user project is never broken.**
- **Queries**: filters on properties (inherited values included), FTS search (accents: "col gele" finds "Col gelé"), `WITH RECURSIVE` over inheritance.
- **Blobs**: deduplication by SHA-256, garbage collection (a blob still referenced by a revision is **not** deleted), `Range` requests.
- **`erable doctor` command**: checks SQLite integrity, orphan or missing blobs, broken references, cycles, out-of-sync FTS index. It is tested on deliberately corrupted projects, and run at the end of each e2e test to check that the database left behind is healthy.

### 6.1 Surviving interruptions

A local wiki must survive a power cut.
- A test **kills the process** (`Process.Kill`) at random moments during a burst of writes, then reopens the project: `integrity_check` OK, and each save is either entirely present or absent.
- Writing blobs: temporary file, then atomic `rename`, then insertion in the database. An incomplete blob must never be referenced.

---

## 7. API and integration

`WebApplicationFactory<Program>` starts the real server in memory, with a real SQLite project.

- **One test per endpoint** for the nominal case and the errors (404, 409, 422), with `ProblemDetails` responses.
- **Generated authorization matrix**: the test enumerates *every* exposed endpoint through the `EndpointDataSource` and calls it with each role (anonymous, reader, editor, administrator, member of another project). A new endpoint without an explicit authorization rule **fails the suite**. This is the protection against the most common security bug: the forgotten endpoint.
  *Implementation*: the rule of each endpoint is its `AccessRule` metadata; `AuthorizationMatrix` (tests/Erable.Server.Tests) compares it with the actual behaviour for six profiles and refuses any endpoint without a rule; a second check verifies that the writes of a project require an editor (except for a list of exceptions).
- **Contract**: the OpenAPI document is compared with a versioned snapshot (**Verify** library). An API change shows up in code review as a readable diff; removing a field in `/api/v1` is refused.
- **Concurrency**: two clients save the same page from the same revision → one receives `409` with the current version; N parallel writes on different pages → no loss.
- **SignalR**: two clients connected to the hub; a change by A produces `ElementChanged` for B, but not for a client of another project. Presence: connection, departure, abrupt disconnection (the ghost disappears after expiry).
- **Local mode security** (§A-9): request without a token → 401; `Host` other than `127.0.0.1:port` → refused; listening on `0.0.0.0` impossible in desktop mode.

---

## 8. Frontend

### 8.1 Unit and component tests (Vitest)

- **CodeMirror extensions tested without a DOM**: an `EditorState` is built, transactions are applied, and the decorations (ID pills), atomic ranges and suggestions of the `[[` palette are checked. This is fast and reliable, unlike simulating typing in a browser.
- **Components** with Testing Library: queried by role and accessible label (`getByRole("treeitem", { name: "PNJ" })`), never by CSS class. Bonus: a component testable this way is also accessible.
- **API simulated** with **MSW**, with handlers typed from the generated client: mocks cannot drift from the real contract.
- **Markdown rendering**: an XSS corpus (`<script>`, `javascript:`, `onerror=`, malicious SVG) whose rendering must produce no executable element.

### 8.2 Internationalization

- A test checks that **every key of `fr` exists in `en`, `de` and `es`**, and conversely, with the same interpolation parameters and plural forms.
- An ESLint rule forbids hard-coded strings in JSX.
- A pseudo-locale (`xx`, texts lengthened by 40 % and accented) reveals layout overflows and untranslated texts in the visual snapshots.

---

## 9. End-to-end (Playwright)

Against a **real backend** in server mode, started on a "Chroniques d'Aube" project freshly copied for each test file (complete isolation, parallel execution possible).

### 9.1 Browsers

- **Chromium**: engine of WebView2 (Windows) and of most web browsers.
- **WebKit**: the closest to WKWebView (macOS) and WebKitGTK (Linux), used by Photino. This is our best safety net for the risk identified in §A-10.
- Firefox: nightly only (web usage).
- **Mobile** (F18): Playwright project `mobile`, emulated phone (Chromium, Pixel 7 profile: 412 px, touch, coarse pointer), which runs **only** the `@mobile` scenarios; those tagged `@pr` run on every PR. The other projects exclude them. The engine of the Android WebView is Chromium: this project also covers the Android app, whose native shell is checked separately (§T-10.1).

### 9.2 Journeys covered

One file per main screen, plus the cross-cutting journeys. We keep **few, but end to end**:

| Journey | Checks in particular |
|---|---|
| Home | Create a project, open it, find it in the recent ones |
| Tree | Create a folder and a page, drag and drop, rename → **`[[ID]]` references show the new name everywhere** |
| Editing | `[[` opens the palette, filters, `Enter` inserts a link, `Shift+Enter` an embed; pasting an image → attachment; `Ctrl+S` |
| Page | TOC, sub-pages, properties inherited "via PNJ", incoming links, validation warning |
| Category / Property | Inheritance, value conflict, adding a rule → "2 pages breaking the rules" |
| Search | `Ctrl+K`, combined filters, recently viewed / edited |
| Deletion | "N pages reference…" warning, trash, restore, broken link shown cleanly |
| **Multi-user** | Two browser contexts: visible presence, change propagated without reloading, concurrent saves merged without loss |
| History | Compare two revisions, restore |

Writing rules:
- selectors by role and label, or `data-testid` as a last resort;
- never `waitForTimeout`: only waits on a state (`expect(...).toBeVisible()`);
- light *page objects* per screen (`EditorPage`, `TreePanel`) so that UI changes touch only one file;
- Playwright trace kept on failure (video, network, DOM), as a CI artifact.

### 9.3 Visual and accessibility (nightly)

- Snapshots of each main screen in **light/dark × fr/en × xx**, compared to references. Rendering is stabilized (embedded fonts, animations off, frozen clock) and run in a fixed Docker container to avoid differences from one machine to another.
- **axe-core** on each screen: zero "serious" or "critical" violations. Contrast checked in both themes.
- **Procedure**: the snapshots are Playwright tests of the `visual` project (`@visual`), excluded from the other projects. They are compared and updated in the fixed container `src/web/e2e/visual/Dockerfile`: `pnpm -C src/web e2e:visual:docker` (add `-- --update` to rewrite the references, then review each changed image before committing). The references are versioned in `src/web/e2e/screenshots/`. The `xx` pseudo-locale only exists in the test build (`VITE_PSEUDO_LOCALE=1`).
- A **keyboard-only** journey (tree, editing, palette, search), since the tool aims at IDE-like ergonomics.

---

## 10. Desktop application

Playwright does not drive every native WebView; two approaches are therefore combined.

- **Windows**: WebView2 exposes the DevTools protocol. By starting the app with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=…`, Playwright connects to it (`connectOverCDP`) and replays a subset of the e2e journeys **in the real binary**.
- **macOS and Linux**: **`--self-test`** mode. The app starts on a temporary project, the SPA runs a scripted scenario (create a page, insert a reference, attach an image, search, save), sends the result to the backend, and the process exits with code 0 or 1. On Linux, it runs under `xvfb`.
- This smoke test runs on **the published artifact** (self-contained publication, `pr.yml` and `nightly.yml`), not on the dev build. The release workflow does not rerun tests: the Velopack installer itself is not checked in CI.
- Also checked: random port really on loopback, second launch with the same project (lock or reuse of the instance), clean shutdown (WAL checkpointed, no leftover process).
- **Language**: the Windows e2e launcher forces `--lang=fr-FR` in WebView2 (the reference locale, like `locale: "fr-FR"` of the web e2e), whatever the language of the machine; `ERABLE_E2E_WEBVIEW_LANG` changes it, for instance to reproduce an English machine.
- **Startup budget** (E2E-16-01): 3 s. The shared CI runners (software rendering, Xvfb, cold disks) measure 3.3 to 3.6 s: the desktop jobs of `pr.yml` and `nightly.yml` set `ERABLE_STARTUP_BUDGET_MS=10000`, read by `--self-test` as well as by the Windows e2e. The measurement always appears in the report (`startup-budget`), to track the gap to the 3 s budget.

---

### 10.1 Android application

The shell (`src/Erable.Android`, decision Q11) has almost no logic: address normalization, version compatibility, navigation policy. It lives in classes without Android dependencies, also compiled by `tests/Erable.Android.Tests` (xUnit, in `Erable.sln`). The APK is built by the release CI; the on-device journey follows the checklist of `docs/release.md` (no emulator in CI).

## 11. Performance

"Efficient" must be measured, otherwise it degrades without anyone noticing.

### 11.1 Budgets

Measured on the generated project **10,000 pages / 300 categories / 50,000 references / 2 GB of attachments**, on the reference CI machine:

| Operation | Budget (p95) |
|---|---|
| Opening the project → tree shown | < 1 s |
| Loading the tree (API) | < 150 ms |
| Opening a page (API + rendering) | < 200 ms |
| Saving a page (index, refs, FTS, validation) | < 100 ms |
| Full-text search + filters | < 100 ms |
| `[[` palette: keystroke → suggestions | < 50 ms |
| Typing in the editor, 5,000-line page | no frame > 50 ms |
| Initial JS bundle (gzip) | < 500 KB |

### 11.2 Measurement

- **BenchmarkDotNet** on the hot paths of Core and Storage (parser, property resolution, search queries). Results are kept as history; a regression **> 15 %** fails the nightly.
- **Playwright** measures the UI budgets through `performance.mark` calls placed in the SPA.
- **NBomber** (C#) simulates 50 users (80 % reads, 20 % writes) on a server for 10 minutes: no error, p95 within budgets, stable memory (no leak).
- Bundle size checked on every PR.

**Tooling**: `tests/Erable.Benchmarks` (Release) provides `generate` (project with a fixed seed, `--light` for CI), `budgets` (server p95 over HTTP on the generated project), `bench` (in-process BenchmarkDotNet, history in `artifacts/perf/history.json`, comparison with `spec/perf/baseline.json`, failure beyond +15 %) and `compare`. The browser budgets are the Playwright `@perf` tests (`pnpm e2e:perf`, with `ERABLE_LARGE_PROJECT` for the large project). The commands are listed in `CONTRIBUTING.md`.

**Robustness**: `CrashTests` (`tests/Erable.Server.Tests/Robustness/`) starts the real server (`erable serve`) as a child process, kills it at random moments during bursts of writes (saves, creations, attachments), runs `erable doctor` on the folder, then restarts the server and reads back everything that had been acknowledged; `ERABLE_CRASH_ITERATIONS` (8 by default, 100 at night) and `ERABLE_CRASH_SEED` (replay). `Benchmarks load` is the NBomber load test. Both run every night (`.github/workflows/nightly-robustness.yml`).

---

## 12. Measuring the quality of the tests themselves

- **Mutation testing**: Stryker.NET on `Erable.Core` and StrykerJS on the duplicated TS modules (rules, parser), every week. Stryker introduces deliberate bugs (`<` → `<=`, inverted condition…) and checks that a test fails. A score ≥ 70 % avoids tests that pass without checking anything.
- **Coverage**: reported on every PR, but with a blocking threshold **only on Core**. Elsewhere, it is used to find gaps, without a numeric target: a global target pushes people to write useless tests.
- **Fuzzing** of the Markdown parser (C# and TS) with random inputs and a corpus of real cases for 10 minutes every week: no crash, no infinite loop, time linear in the size of the input.

**Implementation**: `.github/workflows/weekly-quality.yml` (Sunday) runs Stryker.NET (`tests/Erable.Core.Tests/stryker-config.json`) on `Erable.Core`, StrykerJS (`src/web/stryker.config.json`, `pnpm mutation`) on identifiers, Markdown and rules, then ten minutes of C# fuzzing (`MarkdownFuzzTests`, `ERABLE_FUZZ_SECONDS`) and TS fuzzing (`markdown.fuzz.test.ts`, `VITE_ERABLE_FUZZ_SECONDS`) with a new seed. A short version (1 s, fixed seed) is part of the normal suite. Scores and survivors are in the summary of each job (`build/mutation-summary.mjs`); failing inputs are written to `artifacts/fuzz/` in the format of `spec/fixtures/markdown/edge-cases.json`, to be added there once the expected output is decided.

---

## 13. Continuous integration

GitHub Actions, three pipelines:

| Pipeline | Trigger | Content | Target duration |
|---|---|---|---|
| **PR** | every branch push (not tags), every pull request | lint + typing, C# and TS unit tests, shared spec, storage, API, Chromium + WebKit e2e (critical journeys), OpenAPI client up to date, bundle size. Build + unit tests on **Windows, Linux and macOS** | < 12 min |
| **Nightly** | every night on `main` | full e2e (3 browsers), visual, accessibility, benchmarks, NBomber load, desktop smoke test on 3 OSes | < 45 min |
| **Release** | `v*` tag | packaged build, signing, Docker image + vulnerability scan; no tests (the tagged commit has already gone through the PR pipeline) | — |

- `main` is protected: merge only if the PR pipeline is green and a review was done.
- Dependencies are updated by **Renovate**, grouped every week, and go through the same pipeline.
- A nightly failure automatically opens an issue (`nightly-issue.yml`, `ci-failure` label; a comment on the issue already open rather than a duplicate).
- **Quarantine**: a flaky test gets the `@quarantine` tag in its title and an entry in `spec/quarantine.json`; it leaves the blocking pipelines (Playwright project `quarantine`, nightly only, non-blocking). `pnpm quarantine:check` (nightly) fails if an entry is older than 24 h or if the registry and the tags disagree: fixed or deleted, never "to be rerun".

---

## 14. Definition of "done" for a feature

A feature can ship when:

- [ ] its business logic is in Core with unit tests (and properties if it has invariants);
- [ ] any new shared syntax or rule has its cases in `spec/fixtures/`;
- [ ] its endpoints have integration tests and appear in the authorization matrix;
- [ ] if it changes the schema: migration + updated `legacy/` project + adapted `erable doctor`;
- [ ] the main user journey has an e2e test (or extends an existing one);
- [ ] its texts are translated into the four languages;
- [ ] it passes axe-core and can be used with the keyboard;
- [ ] no performance budget has regressed.

---

## 15. Main risks and countermeasures

| Risk | Severity | Countermeasures |
|---|---|---|
| Loss or corruption of a project | Critical | SQLite WAL, crash tests (§6.1), `erable doctor` after each e2e, migrations tested on frozen projects |
| A merge that silently overwrites text | Critical | Merge properties (§4.1), multi-user e2e, restorable history |
| Corrupted ID (JS precision) | High | TS branded type, JSON converter, `ids.json` fixtures |
| References broken after a rename or move | High | References by ID only, "rename" e2e (§9.2), detection in `doctor` |
| Diverging C# and TS parsers | Medium | Shared specification (§5), fuzzing of both |
| Endpoint without authorization | High | Generated matrix (§7) |
| XSS between users | High | Sanitization + XSS corpus (§8.1), attachments not rendered inline |
| Access to the loopback server by another process or a website | High | Token + `Host` check, tests §7 |
| Different rendering under WebKitGTK / WKWebView | Medium | WebKit e2e on every PR, desktop self-test on 3 OSes (§10) |
| Slowness on a large project | Medium | Measured budgets (§11), generated 10,000-page project |
| Flaky tests that make people ignore CI | Medium | Immediate quarantine, no time-based waits in e2e |

---

## 16. Set-up per milestone

| Milestone (§A-12) | Test infrastructure delivered |
|---|---|
| **M0** | Test projects, analyzers, PR CI on 3 OSes, Playwright Chromium + WebKit, desktop self-test, OpenAPI client check, OpenAPI snapshot |
| **M1** | Builders, `ids.json` and `markdown/` fixtures, property-based tests on IDs and parser, CodeMirror tests, "Chroniques d'Aube" project, first `legacy/v1`, `erable doctor` |
| **M2** | `rules.json` fixtures, properties on inheritance and tree, Category / Property e2e |
| **M3** | Large project generator, performance budgets, BenchmarkDotNet, Search e2e |
| **M4** | Authorization matrix, concurrency and SignalR tests, multi-user e2e, NBomber, crash tests |
| **M5** | Visual + axe-core + pseudo-locale, release pipeline, weekly Stryker and fuzzing |
| **M6** | Playwright `mobile` project, Android shell tests, APK build in the release |

The investment is concentrated on **M0 and M1**. Once the tooling and conventions are in place, adding tests costs almost nothing. Adding them afterwards to code that was not designed to be tested costs much more.
