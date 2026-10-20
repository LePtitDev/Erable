# Reference snapshots

One snapshot per main screen × theme (light, dark) × language (`fr`, `en`, `xx` = pseudo-locale):
`<screen>-<theme>-<language>.png`, produced by `e2e/specs/f17-visual.spec.ts` (Playwright project `visual`).

Rendering depends on the system (fonts, anti-aliasing): the references are **only** produced and compared
in the fixed container `e2e/visual/Dockerfile` (Playwright 1.64, Ubuntu, .NET SDK). Fonts embedded in the bundle,
animations off, browser and server clocks frozen.

## Compare

```bash
pnpm -C src/web e2e:visual:docker
```

## Update the references (intended interface change)

```bash
pnpm -C src/web e2e:visual:docker -- --update
```

Review **each** changed image (`git diff --stat`, then open the PNG files) before committing: a snapshot updated without
review is no longer a reference. An overflow visible in the `xx` snapshot is a layout defect to fix, not a reference to
accept.

Without Docker: `pnpm -C src/web e2e:visual` runs, but local rendering differences are expected.
