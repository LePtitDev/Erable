# Contributing to Erable

Thanks for your interest. Erable is a personal project maintained in limited free time: issues and pull requests are welcome, but answers may take a while. For anything larger than a fix, open an issue first so that we can agree on the approach.

Security problems are not reported in issues: see [SECURITY.md](SECURITY.md).

## Reference documents

| Document | Purpose | Abbreviation |
|---|---|---|
| [docs/architecture.md](docs/architecture.md) | Stack, data model, storage, API, security, milestones, decisions (`Q<n>`) | §A-x |
| [docs/features.md](docs/features.md) | Features `Fnn`, rules `Fnn.Rn`, scenarios `E2E-nn-nn` (= acceptance criteria) | Fnn / E2E-nn-nn |
| [docs/testing.md](docs/testing.md) | Testing strategy, tooling, definition of "done" | §T-x |
| [docs/markdown-syntax.md](docs/markdown-syntax.md) | Extended Markdown grammar, shared by C# and TypeScript | — |
| [docs/cli.md](docs/cli.md) | `erable-cli` and its JSON contract (`erable-cli/1`), which must not be broken | — |
| [docs/deployment.md](docs/deployment.md) | Hosting a server | — |
| [docs/release.md](docs/release.md) | Publishing a release | — |

A change that departs from these documents updates them in the same pull request. A structural decision gets a new `Q<n>` entry in §A-13.

## Architecture invariants

These are not negotiable without an explicit decision:

1. **`ElementId`**: a random non-zero `ulong`, one space for every kind. **Always a 16-character hex string outside the database** (JSON, URL, TS). Branded type on the TS side, obtained only through `parseElementId()`. Never a `number` (§A-4).
2. **`Erable.Core` is pure**: no IO, database, HTTP or system clock dependency (use `TimeProvider`). All business logic lives there.
3. **The frontend only talks HTTP/WebSocket to the backend**, never to the OS. The few native functions go through the desktop shell behind an interface.
4. **References go through the ID**: renaming or moving never breaks anything.
5. **Raw HTML disabled** in Markdown, sanitized rendering; SVG and HTML attachments never rendered inline (§A-9).
6. **Validation warns, it does not block** (§A-5.4).
7. **One specification, two implementations**: IDs, Markdown grammar and validation rules are driven by `spec/fixtures/` and run by xUnit **and** Vitest (§T-5).
8. **No hard-coded color** in components (CSS tokens), **no hard-coded UI string** (i18n).
9. **Generated API client** from the OpenAPI document; API types are never written by hand.
10. **Schema migrations**: any change adds a migration, updates `spec/projects/legacy/` and `erable doctor`. An existing project is never broken.
11. **Desktop mode**: listens on `127.0.0.1` only, session token required, `Host` header checked.
12. The `/_test/*` endpoints only exist with `ERABLE_TEST_MODE=1` and **never** in a Release build.

## Repository layout

```
Erable/
├─ Erable.sln
├─ Directory.Build.props · Directory.Packages.props · .editorconfig · global.json
├─ src/
│  ├─ Erable.Core/        pure domain
│  ├─ Erable.Storage/     SQLite + Dapper, migrations, FTS5, BlobStore
│  ├─ Erable.Server/      ASP.NET Core Minimal API, SignalR, auth, OpenAPI, CLI (serve, doctor)
│  ├─ Erable.Desktop/     Photino shell
│  ├─ Erable.Cli/         erable-cli: read-only extraction (JSON, Markdown), contract in docs/cli.md
│  ├─ Erable.Android/     Android app (.NET for Android), client of servers; outside Erable.sln (Q11)
│  └─ web/                React/TS SPA (pnpm + Vite); Playwright e2e in web/e2e/
├─ tests/                 Erable.Core.Tests, Erable.Storage.Tests, Erable.Server.Tests, …
├─ spec/
│  ├─ fixtures/           cases shared by C# and TS (ids, rules, markdown, limits)
│  └─ projects/           chroniques/ (reference project), legacy/vN/, users.json
└─ docs/
```

## Languages

- Everything in the repository is in **English**: code, comments, logs, exception messages, test names, documentation, commit messages.
- UI texts go through i18n only, with hierarchical English keys (`editor.palette.insertLink`). `fr` is the reference locale, then `en`, `de`, `es`; a test checks that every key exists in the four languages with the same parameters and plural forms.
- The reference project "Chroniques d'Aube" is French content and stays so; the tests run in the `fr` locale.
- E2E test titles start with the scenario ID followed by an English title: `test("E2E-05-03 · Insert a link via the palette @pr", …)`. Traceability relies on the ID.

## Conventions

### C# (.NET 10)

- `Nullable` enabled, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, centralized versions (`Directory.Packages.props`).
- File-scoped namespaces, `sealed` by default, `record` for values, exhaustive `switch`.
- Minimal APIs grouped by resource; errors as `ProblemDetails`.
- Explicit SQL with Dapper, always bound parameters, no EF Core.
- Tests: xUnit v3 + Shouldly; CsCheck for properties; naming `Method_Context_ExpectedResult`; one behaviour per test; real SQLite in a temporary folder, no mocks of what we own (§T-1).

### TypeScript / React

- `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`; ESLint `strict-type-checked`; no `any` outside generated code.
- Function components, CSS Modules + tokens; Radix UI for accessible primitives.
- Server state: TanStack Query; UI state: Zustand; routing: TanStack Router.
- Tests: Vitest + Testing Library (queries by role and label), typed MSW, fast-check; CodeMirror extensions tested on `EditorState`, without a DOM.
- e2e: Playwright, page objects, no `waitForTimeout`.

### General

- Prefer the simplest solution that respects the invariants. No abstraction "for later" without a need identified in the documents.
- No new dependency without a justification (maintenance, license, bundle size).
- Accessibility from the first component: ARIA roles, visible focus, everything usable with the keyboard.
- Portable code: paths through `Path.Combine`, no hard-coded separator; line endings are handled by `.gitattributes`.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat(core): add ElementId parsing and formatting`): the release notes are generated from them.

## Definition of "done" (§T-14)

- business logic in Core, tested (with properties if it has invariants);
- shared cases added to `spec/fixtures/` for any shared syntax or rule;
- endpoints tested in integration and present in the authorization matrix;
- schema changed → migration + `legacy/` + `doctor`;
- the `E2E-*` scenarios of the change implemented and green;
- texts translated into the 4 languages;
- usable with the keyboard, no serious axe-core violation;
- no performance budget degraded.

Before opening a pull request, run at least `scripts/test.sh` (or `scripts\test.bat`): it runs the checks of the PR pipeline. A bug fix starts with a test that reproduces the bug.

## Commands

Prerequisites: .NET 10 SDK, Node.js and Corepack (pnpm is pinned by `packageManager`; use `corepack pnpm …` if the shims are not enabled). Development happens mainly on Windows (PowerShell), but the code and scripts stay portable.

Shortcuts (`.bat` on Windows, `.sh` elsewhere) in `scripts/`, detailed in [scripts/README.md](scripts/README.md):

```bash
scripts/build.sh [Debug|Release]            # web dependencies, SPA, .NET solution
scripts/run.sh [desktop|server [folder] [port]|dev]
scripts/test.sh [--e2e] [--desktop]         # PR pipeline checks; --desktop: publication + smoke test
scripts/publish.sh [--version x.y.z] [--rid <rid>] [--android]  # --android: APK only; otherwise the desktop installer of this machine (Velopack) in artifacts/release/<rid>/ and erable-cli in artifacts/cli/; default version: latest Git tag
scripts/serve.sh [folder] [--port 5080] [--lan] [--rebuild] [-- options]   # host without Docker: publishes in Release (artifacts/server, redone if the commit changes) then erable serve; default folder data/
scripts/compose.sh [--litestream] setup|up|update|down|status|logs|backup|restore|doctor   # host with Docker Compose + Caddy (deploy/.env)
```

### .NET

Tests run on Microsoft.Testing.Platform: `--solution` is required.

```bash
dotnet build Erable.sln
dotnet test --solution Erable.sln
dotnet test --project tests/Erable.Core.Tests          # a single project
dotnet test --solution Erable.sln -c Release           # also checks that /_test is absent in Release
dotnet test --project tests/Erable.Storage.Tests -- --filter-method "*GenerateChroniques" --explicit only   # regenerates spec/projects/chroniques
dotnet run --project src/Erable.Server -- serve <folder> [--port 5080] [--lan]   # project folder (shared as is) or data folder; ERABLE_ADMIN_LOGIN/ERABLE_ADMIN_PASSWORD for the first administrator; ERABLE_TEST_MODE=1 for /_test; ERABLE_* settings: docs/deployment.md
dotnet run --project src/Erable.Server -- healthcheck [--port 8080]       # exit code 0 if the local server answers (container health)
dotnet run --project src/Erable.Server -- doctor <project> [--json] [--fix]   # integrity of a closed project (0 healthy, 1 anomalies, 2 unreadable)
dotnet run --project src/Erable.Desktop                 # desktop app (after pnpm -C src/web build)
dotnet run --project src/Erable.Cli -- <info|tree|page|pages|schema|links|attachments|attachment|export> <project> [--pretty]   # erable-cli, docs/cli.md (codes 0 ok, 1 usage, 2 unreadable project, 3 element not found, 4 output)
node build/version.mjs                     # version of a local build from Git (tag of HEAD, otherwise x.y.(z+1)-dev.N); used by the executables (Directory.Build.targets)
node build/publish-cli.mjs win-x64 [--version x.y.z]  # self-contained erable-cli (single file) → artifacts/cli/
docker build -t erable --build-arg ERABLE_VERSION="$(node build/version.mjs)" .   # server image (docs/deployment.md); without the argument, it shows 0.0.0-dev
```

### SPA

```bash
pnpm -C src/web install
pnpm -C src/web dev              # Vite on :5173, proxy /api → http://localhost:5080
pnpm -C src/web build            # prints the gzip size of the initial bundle
pnpm -C src/web test             # Vitest
pnpm -C src/web lint             # ESLint strict-type-checked, 0 warnings
pnpm -C src/web typecheck
pnpm -C src/web format:check     # Prettier
pnpm -C src/web api:generate     # exports the OpenAPI document (dotnet) then regenerates src/api/schema.ts
pnpm -C src/web api:check        # CI: fails if openapi.json or schema.ts are stale
pnpm -C src/web e2e              # Playwright Chromium + WebKit against the real backend (SPA build + erable serve, ERABLE_TEST_MODE=1)
pnpm -C src/web e2e:pr           # only the @pr tests
pnpm -C src/web e2e:perf         # timing measurements (@perf), alone, one worker (excluded from the other projects)
pnpm -C src/web exec playwright test --project mobile   # @mobile scenarios (F18) on an emulated phone (Pixel 7)
pnpm -C src/web e2e:trace        # E2E-* ↔ tests traceability; `-- --enforce M1` fails if a scenario ≤ M1 is missing
pnpm -C src/web bundle-size      # fails beyond 500 KB gzip
pnpm -C src/web exec playwright install chromium webkit   # once per machine
node src/web/scripts/generate-icons.mjs   # regenerates the icons (web manifest, Android launcher) from the logo drawing
```

### Performance and robustness (always in Release)

```bash
dotnet run -c Release --project tests/Erable.Benchmarks -- generate artifacts/perf/large [--light] [--pages 10000 --categories 300 --refs 50000 --attachments-size 2GB --seed 42]   # --light: 16 MB of attachments (CI)
dotnet run -c Release --project tests/Erable.Benchmarks -- budgets [--folder artifacts/perf/large] [--light] [--json file]   # server budgets (p95) over HTTP; exit code 1 if exceeded
dotnet run -c Release --project tests/Erable.Benchmarks -- bench [--pages 10000] [--update-baseline]   # BenchmarkDotNet; writes artifacts/perf/, compares with spec/perf/baseline.json (exit code 1 beyond +15 %)
ERABLE_LARGE_PROJECT=<folder generated with --light> pnpm -C src/web e2e:perf   # browser budgets (tree, page, palette, typing); without the variable, only the @perf tests of the reference project run
ERABLE_CRASH_ITERATIONS=100 dotnet test --project tests/Erable.Server.Tests -- --filter-class "*CrashTests"   # kills the real server during writes (8 iterations by default; ERABLE_CRASH_SEED replays a draw)
dotnet run -c Release --project tests/Erable.Benchmarks -- load [--users 50] [--minutes 10 | --seconds N] [--light] [--json file] [--reports folder]   # NBomber load (80 % reads / 20 % writes); exit code 1 on error, p95 beyond budget or growing memory
```

### Desktop and Android

```bash
node build/publish-desktop.mjs win-x64     # or linux-x64, osx-arm64, osx-x64 → artifacts/desktop/
pnpm -C src/web e2e:desktop                # Windows: E2E-16-01/02 on the published binary (CDP)
artifacts/desktop/<rid>/Erable/Erable.Desktop [folder] --self-test [--self-test-report report.json]   # exit code 0/1
dotnet build src/Erable.Android                       # debug APK (needs `dotnet workload install android`; outside Erable.sln, decision Q11)
node build/publish-android.mjs [--version x.y.z]      # signed APK → artifacts/android/ (keystore: docs/release.md; debug key otherwise)
dotnet test --project tests/Erable.Android.Tests       # shell logic, without the workload (in Erable.sln)
```

Desktop environment variables: `ERABLE_STARTUP_BUDGET_MS=<ms>` replaces the 3 s startup budget (10000 in CI); `ERABLE_E2E_WEBVIEW_LANG` changes the language forced in the e2e tests (fr-FR); `ERABLE_PROFILE_DIR=<folder>` replaces the application profile (tests); with `ERABLE_TEST_MODE=1`, `ERABLE_TEST_PICK_FOLDER=<folder>` replaces the native folder picker (E2E-16-03).
