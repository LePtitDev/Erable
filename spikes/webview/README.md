# Spike WebView (M0-08) — code jetable

Compte rendu : [`docs/spikes/webview.md`](../../docs/spikes/webview.md). Ce dossier n'est pas dans `Erable.sln` et n'a pas vocation à rester.

```bash
pnpm -C spikes/webview/web install
pnpm -C spikes/webview/web media      # génère sample.mp4 et drop.png (ffmpeg)
pnpm -C spikes/webview/web build
dotnet build spikes/webview/host            # Photino.NET
dotnet build spikes/webview/host-photinox   # PhotinoX
node spikes/webview/run-checks.mjs <chemin absolu de Spike.Host.exe> [--real-keys]   # Windows, CDP
node spikes/webview/webkit-checks.mjs       # indication WebKit (Playwright)
```
