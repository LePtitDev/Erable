# M6-06 — Tactile : menus sans clic droit, déplacement sans glisser, cibles de 44 px

- **Statut** : review
- **Jalon** : M6
- **Dépend de** : M6-02
- **Références** : F18.R4, F18.R5, F02

## Objectif

Tout faire au doigt : ce qui passe aujourd'hui par le survol, le clic droit ou le glisser-déposer a un équivalent tactile.

## Périmètre

- Arbre : bouton « … » par nœud (toujours visible avec un pointeur grossier, au survol et au focus sinon) qui ouvre le menu contextuel ; l'appui long l'ouvre aussi.
- Action « Déplacer vers… » (dialogue avec les destinations valides, filtrable), aussi sur le bureau ; même mutation que le glisser-déposer.
- Revue des actions visibles seulement au survol (lignes de l'accueil, listes) ; zones de toucher de 44 px.

## Critères d'acceptation

- [x] `F18.R4`, `F18.R5`.
- [x] `E2E-18-07`, `E2E-18-08`, `E2E-18-10` verts.
- [x] Vitest : destinations valides de « Déplacer vers… » (`treeModel`).
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

- **Bouton « … »** des lignes de l'arbre (éditeurs seulement) : ouvre le menu contextuel existant, ancré sous le bouton ; toujours visible avec un pointeur grossier, au survol et sur la ligne focalisée sinon. Hors de l'ordre de tabulation (l'arbre est un seul arrêt ; le clavier a F2, Suppr, Ctrl+X et la touche Menu). L'appui long ouvre déjà le menu : Chrome Android émet `contextmenu`.
- **Nom des lignes** : `aria-labelledby` sur le libellé (et le compteur des racines), sinon le nom du bouton s'ajoutait à celui du `treeitem`.
- **« Déplacer vers… »** : `moveDestinations` (pur, `treeModel.ts`) parcourt la racine de l'élément dans l'ordre de l'arbre et garde les parents acceptés par `checkMove` (même règle que le glisser-déposer), sans le parent actuel. `MoveDialog` les liste (filtre au-delà de 5), le choix appelle la même mutation que le dépôt. Aussi sur le bureau.
- **Libellé** : l'ancien « Déplacer… » (couper, Ctrl+X) devient « Couper » dans les 4 langues, pour ne pas côtoyer « Déplacer vers… ».
- **Cibles de 44 px** (pointeur grossier) : lignes de l'arbre (hauteur de ligne de la virtualisation comprise), fil d'Ariane, outils et case de l'éditeur, filtre de l'arbre, nom des projets récents. Le détecteur `expectTouchTargets` mesure le `treeitem` entier et, pour un champ, son `label` englobant.
- **Vérifications** : `playwright test --project mobile` (11 scénarios F18), Vitest `features/explorer` (32, dont `moveDestinations` par propriétés), lint, typecheck, Prettier ; captures Pixel 7.
