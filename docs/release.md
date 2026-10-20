# Publishing a release

A release is a **Git tag `vX.Y.Z` or `X.Y.Z`**. There are two ways to create it; the `.github/workflows/release.yml` workflow does the rest:

- **push the tag** (`git tag v1.2.0 && git push origin v1.2.0`): the workflow creates a **draft** release, attaches the installers, then publishes it once everything is green;
- **create the release on GitHub** (*Releases → Draft a new release*, new tag, *Publish release*): GitHub pushes the tag, the workflow keeps the release as it is (title, notes, published) and **attaches the installers** once built. If the release has no notes, it adds the generated ones. While the workflow runs (≈ 30 min), the published release does not have its files yet; if a job fails, it stays without them (rerun the workflow, or use the catch-up below).

**Catch-up** of an existing release without installers (tag created before this workflow, failure): *Actions → Release → Run workflow*, `version` = the exact tag (e.g. `1.0.0`), tick **attach**. The workflow builds the code **of the tag** and attaches the files to its release, without changing its state (draft or published).

| Step | What is checked or produced |
|---|---|
| `notes` | release notes generated from the Conventional Commits since the previous tag (`build/release-notes.mjs`) |
| `package` (×4) | `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`: self-contained publication (binaries stamped with the version of the tag), then **Velopack** (`build/pack-desktop.mjs`) and `erable-cli`; files attached to the release |
| `android` | APK of the Android application (`build/publish-android.mjs`), signed with the release keystore and attached to the release; without a keystore, built but **not attached** (warning). This job does not block `publish` |
| `image` | Docker image of the server, Trivy scan (fixable HIGH/CRITICAL = failure), published as `ghcr.io/<owner>/erable:<version>` and `latest` |
| `publish` | turns the draft into a published release (no effect if it already is) |

The workflow runs **no tests**: the tagged commit has already gone through the PR pipeline (tests, migration of the `legacy/` projects, desktop smoke test on the 3 OSes), and pushing a tag does not rerun that pipeline.

A manual run (*Actions → Release → Run workflow*, with a version, without **attach**) builds everything **without publishing anything**: the packages stay as workflow artifacts.

To produce an installer on your own machine, without CI: `scripts/publish.sh` (or `scripts\publish.bat`), see `scripts/README.md`; the version comes from the latest Git tag when not given.

## Deliverables per system

| System | Files (in `artifacts/release/<rid>/`) |
|---|---|
| Windows | `Erable-win-Setup.exe` (per-user installer, no administrator rights), `.nupkg` packages, `Erable-win-Portable.zip` |
| macOS | `Erable-osx-<arch>-Setup.pkg`, `Erable-osx-<arch>-Portable.zip`, update packages; each architecture has its own Velopack **channel** (`osx-arm64`, `osx-x64`) so that their files do not overwrite each other in the release |
| Linux | `Erable-linux-x86_64.AppImage`, `Erable-linux-Portable.zip`, update packages |
| All | `releases.<channel>.json` and `RELEASES-*`: the manifest the application queries |
| All | `erable-cli-<rid>.zip` (Windows) or `.tar.gz`: the `erable-cli` tool as one self-contained executable (`docs/cli.md`), in `artifacts/cli/` |
| Android | `Erable-<version>.apk` (in `artifacts/android/`): installed by hand (*Unknown sources*), Android 8 or later; it is a client of Erable servers (decision Q11) |

The exact names are those of `vpk` (Velopack).

**Linux dependencies.** The AppImage does not bundle the web engine nor the desktop libraries: the machine must provide WebKitGTK 4.1 and libnotify (PhotinoX stops at startup without `libnotify.so.4`). On Debian / Ubuntu: `sudo apt install libwebkit2gtk-4.1-0 libnotify4`; on Fedora: `sudo dnf install webkit2gtk4.1 libnotify`.

## Automatic updates

At startup, the installed application queries the GitHub releases. If a newer version exists, it is downloaded in the background and **installed at the next close**: never in the middle of a session. A development build (not installed) checks nothing.

Turning it off:

- environment variable `ERABLE_DISABLE_UPDATES=1` (managed deployments);
- or a `desktop.json` file in the profile folder (`%APPDATA%\Erable`, `~/Library/Application Support/Erable`, `~/.config/erable`): `{ "checkForUpdates": false }`.

### Checking an N → N+1 update without publishing anything

`ERABLE_UPDATE_SOURCE` replaces GitHub with a folder (or a URL) containing the files of `vpk pack`.

```bash
# 1. Pack N then N+1 into the same feed folder
node build/pack-desktop.mjs win-x64 --version 1.0.0
mkdir /tmp/erable-feed && cp -r artifacts/release/win-x64/* /tmp/erable-feed
node build/pack-desktop.mjs win-x64 --version 1.0.1 --skip-publish
cp -r artifacts/release/win-x64/* /tmp/erable-feed
```

Then install 1.0.0 (its `Setup`), start the application with the local feed, close it, start it again: the version shown at the bottom of the home sidebar is 1.0.1.

```bash
ERABLE_UPDATE_SOURCE=/tmp/erable-feed "$LOCALAPPDATA/Erable/current/Erable.Desktop.exe"
```

## Signing

Without signing, everything works, but Windows SmartScreen and macOS Gatekeeper show a warning ("unrecognized application"). To avoid it, the maintainer must provide (architecture §A-11.4):

| What | Where to get it | GitHub secret to create |
|---|---|---|
| Windows signing | **Azure Trusted Signing** (Azure account, identity validation, ~$10/month): a signing account and a certificate profile | `ERABLE_SIGN_AZURE_METADATA`: content of the `metadata.json` file (endpoint, account, profile). Azure authentication (service principal, `AZURE_*` variables) is wired in once the account exists |
| macOS signing and notarization | **Apple Developer Program** (~$99/year): *Developer ID Application* and *Developer ID Installer* certificates | `ERABLE_SIGN_APPLE_APP_IDENTITY` (name of the application certificate), `ERABLE_SIGN_APPLE_INSTALL_IDENTITY` (name of the installer certificate), `ERABLE_NOTARY_PROFILE` (`notarytool` profile stored in the keychain of the runner); the `.p12` certificates must be imported into the keychain of the runner before the *Package* step |
| Linux | nothing to pay | — |
| Android | nothing to pay: a **keystore** that you create and keep (see below) | `ERABLE_ANDROID_KEYSTORE_BASE64` (the file in base64), `ERABLE_ANDROID_KEY_ALIAS`, `ERABLE_ANDROID_KEYSTORE_PASSWORD`, `ERABLE_ANDROID_KEY_PASSWORD` (optional: defaults to the one of the keystore) |

### Android keystore

Android only installs an update signed with **the same key** as the installed version: the keystore is created once and must not be lost (without it, users would have to uninstall the application to get the next version). Create it with the `keytool` of a JDK 17:

```bash
keytool -genkeypair -v -keystore erable-release.keystore -alias erable -keyalg RSA -keysize 4096 -validity 10000
```

Then create the GitHub secrets (`base64 -w0 erable-release.keystore` gives the value of `ERABLE_ANDROID_KEYSTORE_BASE64`) and keep a copy of the file and the passwords outside the repository. Locally, the same variables without `_BASE64`: `ERABLE_ANDROID_KEYSTORE` (path of the file), `ERABLE_ANDROID_KEY_ALIAS`, `ERABLE_ANDROID_KEYSTORE_PASSWORD`, then `scripts/publish.sh --android`. Without them, the APK is signed with the debug key of the machine: fine for a trial, not for distribution.

`versionCode` (what Android compares) comes from the version: `((major × 1000 + minor) × 1000 + patch) × 100 + n`, with `n = 99` for a release and the build number for `x.y.z-dev.N` (at most 98); a major version of at most 20.

### Checking the Android application on a device

CI builds the APK but has no emulator: before a release, on a phone (or an Android Studio emulator) with a test server (`scripts/serve.sh`, `--lan` option for the local network):

1. Install the APK, open it: the list of servers is empty, in the language of the phone (fr, en, de, es; English otherwise).
2. *Add a server* with a wrong address (`example.invalid`): reported as unreachable; with the address of any website: reported as not answering like an Erable server; with the address of the server: it opens. With `http://`, the list shows that the connection is not encrypted.
3. Sign in, open a page, edit it: the keyboard does not hide *Save*; notches and the gesture bar hide nothing.
4. Add an attachment from the editor (Android picker), then download an attachment: it lands in *Downloads*.
5. A link to another site opens in the browser; the back button walks back the history, then returns to the list.
6. Close the application (kill it from the recent apps), reopen it: the session is still there.
7. Long press on the server → *Forget*: it leaves the list; added again, it asks to sign in again.

The wiring steps (Azure authentication, import of the Apple keychain) depend on accounts that do not exist yet: they are therefore not written in `release.yml` and will be added with the secrets. `build/pack-desktop.mjs` already reads the variables above and signs when they are present.
