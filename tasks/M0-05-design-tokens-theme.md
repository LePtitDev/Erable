# M0-05 — Tokens de design et thèmes

- **Statut** : review
- **Jalon** : M0
- **Dépend de** : M0-02
- **Références** : §A-3.1 (Thèmes), F15.R2–R3, Mockup (tous les écrans ; « Page — édition » est en sombre), P-03

## Objectif

Extraire du mockup un système de tokens (couleurs, typographie, espacements, rayons, ombres) en variables CSS, clair et sombre, pour qu'aucun composant n'ait de couleur en dur.

## Périmètre

- Décoder le mockup (`CLAUDE.md` §1) et relever les valeurs des écrans clair et sombre. Les styles y sont inline avec de nombreuses nuances proches : les regrouper en un jeu **sémantique** réduit (`--color-bg`, `--color-surface`, `--color-text-muted`, `--color-accent`, `--color-danger`…).
- Couleurs par type d'élément (badges P / C / Pr / L du mockup) en tokens dédiés.
- Polices **auto-hébergées** (l'app doit fonctionner hors ligne) : IBM Plex Sans, IBM Plex Mono, Source Serif 4 ; inclure leurs licences OFL.
- `:root` = clair, `[data-theme="dark"]` = sombre ; « Système » suit `prefers-color-scheme` en direct.
- Thème appliqué **avant le premier rendu** (attribut injecté par le backend dans `index.html`, voir P-03).
- Page interne de démonstration des tokens (dev uniquement) pour la revue visuelle.
- Règle de lint (stylelint) interdisant les couleurs littérales hors du fichier de tokens.

## Critères d'acceptation

- [x] Contraste AA vérifié pour le texte normal et atténué dans les deux thèmes ; les couleurs du mockup qui échouent sont signalées avec une correction proposée.
- [x] Une couleur littérale dans un `.module.css` fait échouer le lint.
- [x] Bascule clair / sombre / système fonctionnelle sur la page de démonstration.

## Points d'attention

- Ne pas recopier des dizaines de nuances : **proposer** le jeu réduit et le faire valider avant de construire les écrans.

## Notes d'implémentation

- **Le mockup définit déjà un thème sémantique** (fonction `theme(dark)` de chaque écran, ~30 clés : `bg`, `surface`, `sunken`, `hover`, `border`, `text`, `muted`, `faint`, `accent*`, couleurs par type…). Le jeu réduit reprend ces clés, renommées en variables CSS (`src/shared/theme/tokens.css`) :
  - surfaces : `--color-bg`, `-surface`, `-sunken`, `-hover`, `-border`, `-border-strong`, `-editor-bg`, `-editor-line` ;
  - texte : `--color-text`, `-text-muted`, `-text-faint` ; accent/états : `--color-accent`, `-accent-soft`, `-accent-text`, `-on-accent`, `-danger`, `-danger-soft`, `-highlight` ;
  - types d'élément : `--color-kind-{page,category,property,link,attachment}` et `…-soft` ;
  - `--shadow-sm`, `--shadow-pop` ; polices `--font-ui/-mono/-content` ; tailles `--text-2xs` (10) → `--text-h1` (42), base UI 13 px, corps de page serif 19 px ; graisses 400/500/600 ; interlignages ; espacements `--space-*` (grille de 4 px + demi-pas 2/6/10) ; hauteurs de contrôle 28/32/36/40 ; rayons 4/6/8/10/12/pilule ; `--focus-ring`.
  - Écarts du mockup absorbés : l'écran Accueil a des nuances légèrement différentes (`page #2F64A0`, `cat #8F5B0E`…) et l'écran Édition un `propSoft` et une ombre différents → valeurs majoritaires retenues. `heading` et `mark` (Édition) = `text` et `faint` : non dupliqués. Tailles 12,5 / 13,5 / 15,5 px ramenées sur l'échelle.
- **Contraste AA (testé automatiquement, `tokens.test.ts`, 54 paires)** : texte, atténué et discret sur les 5 surfaces, accent, badges de type sur leur fond, danger, surlignage — dans les deux thèmes.
  - **Couleur du mockup en échec corrigée** : `faint` (texte discret, 49 usages : dates, ID, métadonnées) — clair `#737B75` → **`#646C66`** (3,65–4,36:1 → 4,54–5,41:1) ; sombre `#7C847E` → **`#878F89`** (3,89:1 sur `hover` → ≥ 4,51:1). Changement visuel minime.
  - **Signalé, non corrigé** : `border-strong` (1,6:1) ne doit pas être le seul indicateur d'un champ de saisie (WCAG 1.4.11 demande 3:1) ; le focus utilise `--focus-ring` (accent, conforme). À trancher quand les premiers champs arrivent (M1-08) : soit fond `sunken` + bordure, soit une bordure de champ plus contrastée.
- **Thèmes** : `:root` = clair, `:root[data-theme="dark"]` = sombre, `:root[data-theme="system"]` sous `@media (prefers-color-scheme: dark)` = sombre. Le bloc sombre est écrit deux fois (forcé / système) ; un test vérifie qu'ils sont identiques. `light-dark()` aurait évité la duplication mais exige WebKit ≥ 17.5 : risqué sous WebKitGTK (M0-08).
- **Thème avant le premier rendu (P-03)** : le backend sert `index.html` via `SpaIndexEndpoint`, qui insère `data-theme` lu dans `IThemePreferenceSource` (par défaut `system` ; la vraie préférence arrive avec l'API de préférences, M1-15). Testé sur `/`, `/index.html` et une route client. Côté SPA, store Zustand `useThemeStore` + `applyTheme`.
- **Polices auto-hébergées** via Fontsource (OFL) : IBM Plex Sans 400/500/600, IBM Plex Mono 400/500, Source Serif 4 variable (+ italique) ; sous-ensembles latin + latin-ext uniquement. Licences copiées dans `public/licenses/` (livrées avec l'app).
- **stylelint** (`stylelint-config-standard` + `css-modules`) intégré à `pnpm lint` : `color-no-hex`, `color-named: never`, fonctions de couleur interdites, sauf dans `tokens.css`. Vérifié : `color: #fff` dans un `.module.css` fait échouer le lint.
- Page de démonstration `/_dev/tokens` (route enregistrée seulement si `import.meta.env.DEV`, absente du build) : nuancier avec ratio de contraste, badges, typographie, sélecteur Clair / Sombre / Système. Vérifiée dans le navigateur : bascule clair/sombre, et « Système » suit l'émulation `prefers-color-scheme` en direct. Ses textes ne sont pas traduits (outil de dev).
- Vitest ne fournit le contenu d'un CSS importé en `?raw` que si le fichier figure dans `test.css.include` : `tokens.css` y est ajouté.

**Vérification** : `pnpm -C src/web lint` (ESLint + stylelint), `typecheck`, `test` (60), `build` · `dotnet test --solution Erable.sln` (15) · revue visuelle de `/_dev/tokens` (clair, sombre, système).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
