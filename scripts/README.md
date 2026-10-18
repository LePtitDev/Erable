# Scripts

Shortcuts for building, running, testing and hosting Erable from a clone of the repository. Every script exists twice, with the same arguments: `.sh` for Linux and macOS (bash), `.bat` for Windows. Run them from any folder: they move to the repository root first.

| Script | Purpose |
|---|---|
| [`build`](#build) | Builds the web client and the .NET solution |
| [`run`](#run) | Runs the desktop application, a development server or the Vite dev server |
| [`test`](#test) | Runs the checks of the PR pipeline locally |
| [`publish`](#publish) | Builds the desktop installer of this machine's system, and `erable-cli` |
| [`serve`](#serve) | Hosts a server on this machine, without Docker |
| [`compose`](#compose) | Hosts a server with Docker Compose behind Caddy (HTTPS) |

**Prerequisites:** the .NET SDK of `global.json` and Node.js with Corepack (it provides the pnpm version pinned in `src/web/package.json`). `compose` needs Docker with the Compose plugin instead.

---

## build

```bash
scripts/build.sh [Debug|Release]
```

Installs the web dependencies (`pnpm install --frozen-lockfile`), builds the SPA (copied into the server's `wwwroot`), then builds `Erable.sln`.

| Argument | Default | Meaning |
|---|---|---|
| `Debug` \| `Release` | `Debug` | .NET build configuration |

## run

```bash
scripts/run.sh [desktop | server [folder] [port] | dev]
```

Runs Erable for development, with `dotnet run` (build first with `build`, so that the SPA is up to date).

| Mode | Meaning |
|---|---|
| `desktop` (default) | The desktop application |
| `server [folder] [port]` | `erable serve` in Debug. `folder` defaults to `artifacts/data`, `port` to `5080` |
| `dev` | The Vite dev server on `:5173`, which proxies `/api` to a server started separately (`run server`) |

To host a server for other people, use [`serve`](#serve) instead: it runs a Release build.

## test

```bash
scripts/test.sh [--e2e] [--desktop]
```

Runs the checks of the PR pipeline (`docs/testing.md` §13): .NET format check, tests in Debug and Release (the Release run checks that the `/_test` endpoints are absent), web typecheck, lint, format, unit tests, API contract, SPA build and its size budget (500 kB gzip), and E2E traceability.

| Option | Meaning |
|---|---|
| `--e2e` | Also runs Playwright (Chromium and WebKit) against the real backend, installing the browsers if needed |
| `--desktop` | Also publishes the desktop application for this machine and runs its `--self-test`. On Windows, also runs the desktop e2e tests over CDP (`pnpm e2e:desktop`) |

## publish

```bash
scripts/publish.sh [--version x.y.z] [--rid win-x64|linux-x64|osx-arm64|osx-x64]
```

Builds the desktop installer locally, as the release workflow does (`docs/release.md`): installs the web dependencies, publishes the self-contained application (SPA included) with the version stamped in the binaries, then packs it with Velopack (`build/pack-desktop.mjs`, the `vpk` tool of `.config/dotnet-tools.json`). The files land in `artifacts/release/<rid>/`: `Erable-win-Setup.exe`, `Erable-linux-x86_64.AppImage` or `Erable-osx-<arch>-Setup.pkg`, a portable archive and the update packages. It also publishes `erable-cli` (`docs/cli.md`) as one self-contained executable in `artifacts/cli/<rid>/`, archived as `artifacts/cli/erable-cli-<rid>.zip` (or `.tar.gz`).

| Option | Default | Meaning |
|---|---|---|
| `--version x.y.z` | from Git | The tag of `HEAD` (`v1.2.0` or `1.2.0` gives `1.2.0`); after the last tag, `x.y.(z+1)-dev.N` with N commits since it, which sorts after that release and before the next one |
| `--rid <rid>` (`.sh` only) | this machine | Target system. Velopack packs for the system it runs on; macOS can pack both architectures. `.bat` always builds `win-x64` |

The packages are unsigned unless the `ERABLE_SIGN_*` variables of `docs/release.md` are set: Windows SmartScreen and macOS Gatekeeper then warn on first launch. An installed application checks GitHub for updates; set `ERABLE_DISABLE_UPDATES=1` to keep a local build as it is.

## serve

```bash
scripts/serve.sh [folder] [--port 5080] [--lan] [--rebuild] [-- erable serve options]
```

Hosts a server on this machine without Docker (`docs/deployment.md` §1). Publishes the server in Release, SPA included, into `artifacts/server`, then runs `erable serve`. The publication is reused as long as the commit does not change, so later starts take a few seconds. `Ctrl+C` stops the server.

| Argument | Default | Meaning |
|---|---|---|
| `folder` | `data/` (ignored by Git) | A project folder, shared as is, or a data folder that holds several projects |
| `--port N` | `5080` | TCP port |
| `--lan` | off | Listens on every interface, so that colleagues open `http://<this machine>:<port>`. Without it, only this machine can connect |
| `--rebuild` | off | Publishes again even if the commit did not change (for uncommitted changes) |
| `-- …` | — | Passed to `erable serve` as is, e.g. `-- --max-attachment-mb 64 --project-quota-mb 2048` |

On the first start of an empty folder, the server asks for the first administrator in the terminal, or reads `ERABLE_ADMIN_LOGIN` and `ERABLE_ADMIN_PASSWORD`. The other `ERABLE_*` settings of `docs/deployment.md` also apply.

> `--lan` serves plain HTTP: passwords cross the network unencrypted. Use it on a trusted network only; for the Internet, use [`compose`](#compose).

## compose

```bash
scripts/compose.sh [--litestream] <command> [arguments]
```

Hosts Erable with Docker Compose behind Caddy, which obtains the TLS certificates (`docs/deployment.md` §3). Wraps `deploy/docker-compose.yml` and the settings in `deploy/.env`. Run without a command, it prints its usage.

| Command | Meaning |
|---|---|
| `setup [domain]` | Creates `deploy/.env` from `deploy/.env.example`, readable by you only, with a random password for the first administrator (shown once). Refuses to overwrite an existing file. The domain must point to this machine, with ports 80 and 443 open |
| `up` | Builds the image, starts or updates the stack, then waits until the server is healthy (2 minutes at most) |
| `update` | Same as `up`, also pulling newer base images. Run it after `git pull` |
| `down` | Stops the stack. The data volume is kept |
| `status` | Shows the containers and their state |
| `logs [service]` | Follows the logs (last 200 lines), of every service or of `erable`, `caddy`… |
| `backup [folder]` | Stops Erable, archives the data volume into `folder/erable-data-<date>.tar.gz` (default `artifacts/backups`), then starts it again |
| `restore <archive>` | After you type `yes`, replaces the whole data volume (accounts and projects) by an archive made by `backup`, then starts Erable again |
| `doctor [project-id]` | Stops Erable, runs `erable doctor` on every project or on one, then starts it again. Exits with the code of `doctor`: 0 healthy, 1 anomalies, 2 unreadable |

| Option | Meaning |
|---|---|
| `--litestream` | Adds the continuous backup to S3-compatible storage (`deploy/docker-compose.litestream.yml`, `docs/deployment.md` §6). Put it before the command, e.g. `scripts/compose.sh --litestream up`. Needs the `LITESTREAM_*` settings in `deploy/.env` |

The compose project is named `deploy`, after the folder of the compose file, unless `COMPOSE_PROJECT_NAME` says otherwise. `backup`, `restore` and `doctor` find the data volume through it.

A typical first deployment:

```bash
scripts/compose.sh setup wiki.example.org
scripts/compose.sh up
scripts/compose.sh backup          # then regularly, or use --litestream
```
