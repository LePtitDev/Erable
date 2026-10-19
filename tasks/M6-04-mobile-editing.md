# M6-04 — Édition sur téléphone

- **Statut** : review
- **Jalon** : M6
- **Dépend de** : M6-03
- **Références** : F18.R6, F05, F06, F08

## Objectif

Modifier une page sur un téléphone, propriétés comprises, sans rien perdre de l'édition du bureau.

## Périmètre

- Éditeur pleine largeur ; panneau de la page (propriétés, catégories, liens, pièces jointes) dans un onglet au-dessus de l'éditeur (« Texte » / « Panneau de la page ») au lieu de la colonne latérale.
- Barre d'outils défilante ; « Annuler / Enregistrer » collés en haut, visibles clavier ouvert.
- Palette `[[` et autocomplétion : largeur bornée à l'écran.

## Critères d'acceptation

- [x] `F18.R6`.
- [x] `E2E-18-04`, `E2E-18-05` verts.
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

- **Texte / panneau** : en mise en page étroite, deux boutons bascule (`aria-pressed`, groupe « Vue de l'éditeur ») au-dessus de l'éditeur. Pas de `Tabs` Radix : sans `Tabs.Content` les `aria-controls` pointeraient dans le vide. Les deux moitiés restent montées (`hidden`) : l'état de CodeMirror et le brouillon des propriétés sont conservés.
- **En-tête** : titre sur sa ligne, puis brouillon · Annuler · Enregistrer. La pastille « Édition » (déjà dans la barre d'état) et les catégories (onglet du panneau) sont masquées sur téléphone ; les raccourcis (`kbd`) disparaissent avec un pointeur grossier.
- **Barre d'outils** : une ligne qui défile (`data-scroll-x`), l'aide « Insérer : Ctrl+Espace » masquée.
- **Clavier virtuel** : `interactive-widget=resizes-content` dans le viewport (Chrome Android) : `100dvh` rétrécit, l'en-tête et ses boutons restent visibles. Non vérifiable en émulation Playwright ; à contrôler sur appareil (liste de `docs/release.md`, M6-08).
- **Barre d'état** : le bloc `@media` de la coque était placé avant les règles de base, qui l'écrasaient ; déplacé en fin de fichier. La connexion se tronque, l'avertissement et le mode gardent leur place.
- **Palette** : déjà bornée à la largeur de l'écran ; E2E-18-04 vérifie qu'elle est entièrement visible.
- **Vérifications** : `playwright test --project mobile` (7 scénarios), Vitest `features/page`, lint, Prettier ; captures Pixel 7 (texte, palette, panneau).
