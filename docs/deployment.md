# Erable — Installing a server

Erable comes in three hosting forms. It is **the same executable** (`erable`) in all three cases.

| Need | Form | Section |
|---|---|---|
| Share a wiki with colleagues tonight, on the same network | `erable serve ./MyWiki --lan` | [§1](#1-sharing-a-local-project-erable-serve) |
| Host several projects for one or more teams | Docker image | [§2](#2-docker-image) |
| The same, reachable from the Internet, with automatic HTTPS | Docker Compose + Caddy | [§3](#3-docker-compose-and-https-caddy) |

From a clone of the repository, two shortcuts chain these steps (`.bat` on Windows, `.sh` elsewhere):

| Script | Purpose |
|---|---|
| `scripts/serve.sh [folder] [--port 5080] [--lan] [--rebuild] [-- options]` | §1 without Docker: publishes the server in Release (SPA included) into `artifacts/server` — again only if the commit changed, or with `--rebuild` — then runs `erable serve`. Default folder: `data/`. The options after `--` go to `erable serve` (`--max-attachment-mb`, `--project-quota-mb`…). |
| `scripts/compose.sh setup [domain]` | §3: creates `deploy/.env` (readable by you only) with a random password for the first administrator, shown once. |
| `scripts/compose.sh up` / `update` | Builds the image and starts or updates the stack, then waits for the `healthy` state. `update` also pulls recent base images: run it after `git pull`. |
| `scripts/compose.sh down` / `status` / `logs [service]` | Stop (the volume is kept), state, logs. |
| `scripts/compose.sh backup [folder]` | Stops Erable, archives the `/data` volume into `artifacts/backups/erable-data-<date>.tar.gz` (or `folder`), restarts. |
| `scripts/compose.sh restore <archive>` | After confirmation, replaces the whole volume with the archive (accounts and projects), then restarts. |
| `scripts/compose.sh doctor [identifier]` | Stops Erable, runs `erable doctor` on every project (or one), restarts; exit code of `doctor`. |
| `scripts/compose.sh --litestream <command>` | The same with continuous backup (§6). |

---

## 1. Sharing a local project: `erable serve`

```bash
erable serve ./MyWiki --lan --port 5080
```

- `./MyWiki` is an Erable project folder (it contains `erable.json`). It is shared **as it is**: no file is added to it.
- Accounts (logins, hashed passwords, sessions) are stored **separately**, in the user profile (`%APPDATA%\Erable\serve\<project identifier>` on Windows, `~/.config/Erable/serve/…` on Linux, `~/Library/Application Support/Erable/serve/…` on macOS), so that the project can be copied or sent without taking the passwords along.
- Without `--lan` (or `--address`), the server only listens on **this machine** (`127.0.0.1`). `--lan` listens on every interface: colleagues open `http://<address of your machine>:5080`.
- On the first run, the server asks for the **first administrator** (login and password). They become administrator of the server and of the project.
- The project must be opened **by a single process**: close it in the desktop application first (F16.R2).
- A project in an older format version must be migrated first: open it once in the desktop application.

> **Plain HTTP on a local network**: passwords travel unencrypted. This is acceptable on a trusted network (home, office), not on public Wi-Fi nor on the Internet. For the Internet, see [§3](#3-docker-compose-and-https-caddy).

`erable serve <folder>` also accepts a **data folder** (without `erable.json`): the server creates `server.db` (accounts), `projects/<identifier>/` (one project folder per project) and `profiles/` in it. This is what the Docker image does with `/data`.

---

## 2. Docker image

### Build and run

```bash
docker build -t erable --build-arg ERABLE_VERSION="$(node build/version.mjs)" .
docker run -d --name erable -p 8080:8080 -v erable-data:/data \
  -e ERABLE_ADMIN_LOGIN=admin -e ERABLE_ADMIN_PASSWORD='a strong password' \
  erable
```

- **Chiseled** `mcr.microsoft.com/dotnet/aspnet` image (no shell nor package manager), **non-root** user (UID 1654), **Release** build (no test endpoint).
- The **`/data`** volume holds everything that matters: `server.db`, `projects/`, `profiles/`. Without a volume, everything disappears with the container.
- `ERABLE_ADMIN_LOGIN` / `ERABLE_ADMIN_PASSWORD` are only used to **create the first administrator**, when the volume holds no account. They are ignored afterwards: remove them from the deployment file once the server has started if you prefer.
- The health of the container is checked by `erable healthcheck` (local request to `/api/info`): `docker ps` shows `healthy`.
- The container listens in plain HTTP on port 8080: **put a TLS reverse proxy in front of it** if it can be reached from anywhere other than your machine.

### Configuration (environment variables)

The command line wins over the environment, which wins over the defaults. An invalid value **stops** the server, naming the faulty setting.

| Variable | Default | Purpose |
|---|---|---|
| `ERABLE_PORT` | `5080` (`8080` in the image) | TCP port. |
| `ERABLE_ADDRESS` | `127.0.0.1` (`0.0.0.0` in the image) | Listening address. |
| `ERABLE_ADMIN_LOGIN`, `ERABLE_ADMIN_PASSWORD` | — | First administrator (empty server). Login: 2 to 32 characters `a-z 0-9 . _ -`; password: 8 to 256 characters. |
| `ERABLE_PUBLIC_URL` | — | The address users type (`https://wiki.example.org`). Allowed by CORS; with `https`, the session cookie becomes `Secure`. |
| `ERABLE_TRUST_PROXY` | `false` | Trust `X-Forwarded-For/-Proto/-Host` from a reverse proxy (real client address for sign-in throttling, protocol for the cookie). **Only** if the server can only be reached through that proxy. |
| `ERABLE_COOKIE_SECURE` | from `ERABLE_PUBLIC_URL` | Forces the `Secure` flag of the cookie. |
| `ERABLE_CORS_ORIGINS` | — | Additional origins allowed to call the API from a browser (a SPA hosted elsewhere), comma-separated. Pages on `localhost` / `127.0.0.1` are always allowed: that is the connected desktop application, which authenticates with a token, never with a cookie. |
| `ERABLE_MAX_ATTACHMENT_MB` | `256` | Maximum size of an attachment. |
| `ERABLE_PROJECT_QUOTA_MB` | none | Total space of the attachments of a project (trash included). `0`: no quota. |
| `ERABLE_SESSION_HOURS` | `12` | Lifetime of an idle session (renewed at half-life). |

### Backup

Stop the container (or make a consistent copy of the volume), then copy `/data`: that's all. Each project is a self-contained folder (`projects/<identifier>/` with `erable.db`, `blobs/`): `erable doctor <folder>` checks its integrity, with the volume mounted elsewhere and the container stopped.
For a **continuous** backup (a few seconds behind at most), see [§6](#6-continuous-backup-with-litestream).

### Update

```bash
git pull && docker build -t erable . && docker rm -f erable && docker run … erable
```

Projects are migrated to the new format **when opened, after a backup** (`erable.db.bak-<date>`).

---

## 3. Docker Compose and HTTPS (Caddy)

`deploy/docker-compose.yml` runs Erable behind **Caddy**, which obtains and renews the TLS certificates on its own.

```bash
cp deploy/.env.example deploy/.env      # fill in the domain and the first administrator
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```

Prerequisites: a domain name that points to the machine, ports **80 and 443** open. The Erable container is not published: only Caddy reaches it, on the Compose network. `ERABLE_TRUST_PROXY=true` is therefore safe there.
WebSockets (SignalR: events and presence) go through `reverse_proxy` without any setting.

---

## 4. What the server protects, and what it does not

- **Passwords**: hashed (PBKDF2, salt per password), never logged nor returned. **Sign-in attempts are limited**: 5 failures for a login from the same address, or 40 failures from one address, within 15 minutes, then `429` (the block is logged). A login targeted from many addresses is never blocked, so that a third party cannot prevent its owner from signing in: beyond 5 failures, each attempt only waits for an increasing delay (1 s, doubled at each failure, at most 10 s).
- **Self-service password change** (*Preferences*), with the current password: the other devices are signed out, the one making the change stays signed in.
- **Sessions**: `HttpOnly`, `SameSite=Strict` cookie, revocable (sign-out, password change, disabled account); `Bearer` token for the connected desktop application. No session can be read by a script of the page.
- **Rights** per project: reader, editor, administrator. A non-member sees neither the name nor the existence of a project.
- **Attachments**: maximum size, quota per project, never interpreted by the browser (`Content-Disposition`, `nosniff`, SVG and HTML downloaded).
- **Browser**: content security policy (CSP) on the application, no embedding in the frame of another site (*clickjacking*), no `Referer` sent to linked sites. The provided `Caddyfile` adds HSTS; behind another reverse proxy, add `Strict-Transport-Security: max-age=31536000`.
- **Out of scope**: network protection (firewall), backups, updates of the system and of the base image (CI scans the image on every change and every week: `.github/workflows/docker.yml`).

## 5. Troubleshooting

| Symptom | Likely cause |
|---|---|
| "This server has no account yet" on startup | First start without a terminal: set `ERABLE_ADMIN_LOGIN` and `ERABLE_ADMIN_PASSWORD`. |
| `project.locked` / project not opened | The folder is already open in the desktop application or in another server. |
| Cannot sign in behind a proxy, or "Too many attempts" for everyone | `ERABLE_TRUST_PROXY` missing: every request seems to come from the address of the proxy. |
| The cookie is not stored over HTTPS | `ERABLE_PUBLIC_URL` in `http://`, or a proxy that does not forward `X-Forwarded-Proto`. |
| A user forgot their password | A server administrator gives them a new one (`PATCH /api/v1/accounts/{login}`); the user then replaces it themselves in *Preferences › Password*. If the **only** administrator lost theirs, there is no procedure yet: the `ERABLE_ADMIN_*` variables only apply to a server without accounts. |
| Attachments are refused (413) | `ERABLE_MAX_ATTACHMENT_MB` (size of a file) or `ERABLE_PROJECT_QUOTA_MB` (space of the project). |

---

## 6. Continuous backup with Litestream

A copy of the `/data` volume is the simplest backup (§2), but it requires stopping the server and only protects up to the last copy. **Litestream** continuously replicates the SQLite databases to S3-compatible storage (AWS S3, Scaleway, Backblaze B2, MinIO…): the maximum loss is a few seconds of writes, for a few cents a month.

### What is backed up, and how

| What | Where | How |
|---|---|---|
| Accounts and rights | `server.db` | Litestream |
| Content of each project | `projects/<id>/erable.db` | Litestream (new projects are detected automatically) |
| Project manifest | `projects/<id>/erable.json` | periodic copy (`files-backup` service, rclone) |
| Attachments | `projects/<id>/blobs/` | periodic copy: files are named by their SHA-256, so they never change; the copy deletes nothing in the bucket |

Litestream can only replicate SQLite: without the `files-backup` service, a restore would recover the pages but neither their attachments nor the manifest.

### Setup

1. Create a dedicated **bucket** and an access key limited to that bucket (read, write, list).
2. In `deploy/.env`, fill in the `LITESTREAM_*` variables (see `deploy/.env.example`).
3. Start with the additional file:

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml -f deploy/docker-compose.litestream.yml up -d
```

Litestream shares the `erable-data` volume with the server (the databases are in WAL mode, which Litestream requires) and asks nothing more of the server. `docker compose logs litestream` should show one replication per database. The image versions (`litestream/litestream`, `rclone/rclone`) are pinned in the file: update them deliberately.

### Restore

The server must be **stopped** and the volume empty (or rid of the damaged databases).

```bash
docker compose -f deploy/docker-compose.yml down
docker volume rm <project>_erable-data && docker volume create <project>_erable-data
export LITESTREAM_BUCKET=… LITESTREAM_ENDPOINT=… LITESTREAM_ACCESS_KEY_ID=… LITESTREAM_SECRET_ACCESS_KEY=…
deploy/litestream/restore.sh <project>_erable-data <project-identifier-1> <project-identifier-2>
```

The project identifiers are the folders under `projects/` in the bucket (S3 console or `rclone lsd`). The script restores `server.db`, each `erable.db`, then copies manifests and attachments, and hands the files over to the user of the image (UID 1654). Then restart the server and check each project:

```bash
docker compose -f deploy/docker-compose.yml run --rm --no-deps --entrypoint dotnet erable erable.dll doctor /data/projects/<identifier>
```

**Attachments more recent than their copy.** The databases are replicated continuously, the attachments every `FILES_BACKUP_INTERVAL_SECONDS` (5 minutes by default): an attachment added just before the failure may therefore be missing after the restore, while the database cites it. `doctor` reports it with a `blob.missing` error ("The file of the attachment is missing", with the ID of the attachment and the start of its SHA-256) and exits with code `1`; `--fix` does not repair it. The rest of the project is intact: get the file back from the person who added it, then add it to the page again.

### Testing the whole chain

`deploy/litestream/test-restore.sh` plays the scenario end to end with a local MinIO: creation of a project and a page, replication, destruction of the volume, restore, `erable doctor`, then reading the page. Run it once after any change to the Litestream file or to the image versions. **It has not been run yet.**

### Limits

- Litestream protects against losing the disk or the server, not against human error: a page deleted by mistake is restored from the trash and the history (F12, F13), not from the backup.
- The replication delay of attachments is `FILES_BACKUP_INTERVAL_SECONDS` (5 minutes by default).
- A restore puts the server back in the state of the last replication: more recent writes are lost.
