# Captures de référence (M5-02)

Une capture par écran du mockup × thème (clair, sombre) × langue (`fr`, `en`, `xx` = pseudo-locale) :
`<écran>-<thème>-<langue>.png`, produites par `e2e/specs/f17-visual.spec.ts` (projet Playwright `visual`).

Le rendu dépend du système (polices, anticrénelage) : les références sont **uniquement** produites et comparées
dans le conteneur fixe `e2e/visual/Dockerfile` (Playwright 1.64, Ubuntu, SDK .NET). Polices embarquées dans le bundle,
animations coupées, horloge du navigateur et du serveur figées.

## Comparer

```bash
pnpm -C src/web e2e:visual:docker
```

## Mettre à jour les références (changement d'interface voulu)

```bash
pnpm -C src/web e2e:visual:docker -- --update
```

Relire **chaque** image modifiée (`git diff --stat` puis ouvrir les PNG) avant de commiter : une capture mise à jour sans
relecture n'est plus une référence. Un débordement visible dans la capture `xx` est un défaut de mise en page à
corriger, pas une référence à accepter.

Sans Docker : `pnpm -C src/web e2e:visual` s'exécute, mais les différences de rendu locales sont attendues.
