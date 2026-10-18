# Erable

A structured, IDE-style wiki: extended Markdown pages, categories with inheritance, typed and validated properties, typed links between elements, attachments.

Erable runs in two ways, from the same code:

- **local desktop application**: a project is a folder on your disk (SQLite + attachments), with no service to install;
- **multi-user server mode**, for working together (Docker or plain executable).

![A page of the sample project "Chroniques d'Aube": explorer tree, Markdown content, properties and links panel](docs/images/screenshot.png)

## Key points

- **References go through the identifier** (`[[3F2A9C14E07B5D21]]`): renaming or moving an element never breaks anything.
- **Validation warns, it does not block.**
- **Nothing is lost by mistake**: trash, plus revisions that can be compared and restored.
- **Backing up = copying a folder** (or continuous replication with Litestream on the server side).
- Raw HTML is disabled in pages, and rendering is sanitized.

## Stack

| Layer | Choice |
|---|---|
| Backend | C# / .NET 10, ASP.NET Core (Minimal API), SignalR |
| Storage | SQLite + Dapper, FTS5 for search, content-addressed attachments |
| Frontend | React + TypeScript + Vite, CodeMirror 6 |
| Desktop | Photino with an in-process backend |
| Distribution | Velopack (desktop), Docker image (server) |

## Quick start

Prerequisites: .NET 10 SDK, Node.js and Corepack (pnpm).

```bash
scripts/build.sh              # web dependencies, SPA, .NET solution
scripts/run.sh desktop        # desktop application
scripts/run.sh server ./MyWiki 5080   # server on a project folder
scripts/test.sh               # PR pipeline checks (--e2e for Playwright)
scripts/serve.sh ./MyWiki --lan              # host a server without Docker (Release build)
scripts/compose.sh setup wiki.example.org    # host with Docker Compose + Caddy (HTTPS), then: scripts/compose.sh up
```

On Windows, use the `.bat` equivalents. Every script and its arguments: [scripts/README.md](scripts/README.md). The full list of commands (tests, e2e, performance, publishing) is in [CLAUDE.md](CLAUDE.md#9-commandes).

## Repository layout

```
src/Erable.Core       pure domain (no IO dependency)
src/Erable.Storage    SQLite, migrations, FTS5, attachment storage
src/Erable.Server     API, SignalR, authentication, CLI (serve, doctor)
src/Erable.Desktop    Photino shell
src/Erable.Cli        erable-cli: read-only extraction of a project as JSON / Markdown
src/web               React/TypeScript SPA
tests/ · spec/        tests and shared C# ↔ TypeScript cases
docs/ · tasks/        documentation and implementation plan
```

## Documentation

The documentation is written in French.

| Document | Content |
|---|---|
| [docs/user-guide.md](docs/user-guide.md) | User guide: getting started, `[[ID]]` links, shortcuts |
| [docs/architecture.md](docs/architecture.md) | Stack, data model, storage, API, security |
| [docs/features.md](docs/features.md) | Features and acceptance scenarios |
| [docs/markdown-syntax.md](docs/markdown-syntax.md) | Extended Markdown syntax |
| [docs/testing.md](docs/testing.md) | Testing strategy |
| [docs/deployment.md](docs/deployment.md) | Installing a server: Docker, Compose, Litestream |
| [docs/release.md](docs/release.md) | Publishing a release |
| [docs/cli.md](docs/cli.md) | `erable-cli`: extracting a project for other tools (commands, JSON contract) |

Contributions (human or AI-assisted) follow the rules in [CLAUDE.md](CLAUDE.md).

## License

[MIT](LICENSE) © 2026 Arthur Ferre
