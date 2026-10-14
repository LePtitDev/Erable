# M1-13 — Palette d'insertion `[[`

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-12
- **Références** : F05.R3, §A-6.2, §A-11.3, Mockup « Page — édition » (palette), §T-11.1

## Objectif

Insérer une référence ou une intégration en quelques touches, sans connaître aucun ID.

## Périmètre

- Ouverture en tapant `[[` ou `![[`, ou par raccourci (par défaut `Ctrl+Espace` Windows/Linux, `⌘+Maj+K` macOS ; table de raccourcis en données, configurable en M5-01).
- Recherche floue sur les noms (endpoint dédié rapide, ou index client de l'arbre : **choisir et justifier** au regard du budget < 50 ms sur 10 000 éléments).
- Onglets Tout / Pages / Pièces jointes / Catégories / Propriétés ; `Tab` change d'onglet ; groupes, chemin et ID affichés comme dans le mockup.
- `Entrée` → lien, `Maj+Entrée` → intégration, `Échap` → ferme sans rien insérer (seul le texte tapé reste).
- Pièces jointes d'autres pages affichées « Page › fichier ».
- Accessible : `combobox` / `listbox`, annonce du nombre de résultats.

## Critères d'acceptation

- [x] `E2E-05-03` à `E2E-05-06` verts.
- [x] Frappe → propositions < 50 ms sur le projet généré de 10 000 pages (mesure indicative avant M3-06).
- [x] Tests unitaires de la logique d'ouverture et d'insertion sur `EditorState`.

## Points d'attention

- Classement : correspondance de préfixe > mot > floue ; récents en bonus. Proposer une règle simple et la tester.

## Notes d'implémentation

- **Choix demandé : index côté client.** L'arbre est déjà chargé et tenu en cache (§A-8.1) ; une route légère `GET projects/{id}/attachments` (testée) y ajoute les pièces jointes, avec leur page. Aucune requête par frappe, pas de latence réseau, et le classement reste testable en pur TypeScript. Mesure : 10 000 éléments, recherche < 50 ms (test unitaire). Quand la recherche plein texte existera (M3-01), la palette pourra la compléter pour les gros projets si le volume l'exige.
- **Classement** (`paletteSearch.ts`, simple et testé) : préfixe du nom (ou du fichier) > préfixe d'un mot > sous-chaîne > lettres dans l'ordre **à partir d'un début de mot** (« cpo » trouve « Capitaine Orsen » ; sans cette condition, « orse » trouvait « carte_hautbois.webp »). Égalités : libellé le plus court, puis ordre alphabétique. Insensible à la casse et aux accents. Les récents (bonus prévu) arrivent avec M3-04.
- **Dans l'éditeur** (`palette.ts`, extension CodeMirror testée sur `EditorState`) : `[[` ou `![[` tapés (pas collés, pas après `\`) ouvrent la palette ; le texte tapé après est la requête et reste dans le document si l'on ferme avec Échap (E2E-05-06) ; elle se ferme si le curseur revient avant le marqueur, sur `]` ou un saut de ligne. Raccourci `Ctrl+Espace` (Windows, Linux) / `⌘+Maj+K` (macOS) : ouvre la palette sans rien insérer, avec son propre champ de recherche. Les raccourcis sont des **données** (`shared/keyboard/shortcuts.ts`), prêtes pour M5-01.
- **Clavier** : ↑ ↓, `Entrée` lien, `Maj+Entrée` intégration (quel que soit le marqueur tapé), `Tab` / `Maj+Tab` onglet suivant / précédent, `Échap` ferme. Clavier de la palette prioritaire sur celui de l'éditeur (`Prec.highest`), actif seulement quand elle est ouverte.
- **Affichage** (`PalettePopup.tsx`, mockup) : au curseur, onglets Tout / Pages / Pièces jointes / Catégories / Propriétés (les types de liens sont avec les propriétés), groupes par type dans « Tout » (l'ordre du clavier est celui de l'écran), badge, libellé (« Hautbois › carte_hautbois.webp » pour une pièce jointe), chemin, ID abrégé.
- **Accessibilité** : `role="listbox"` / `option`, l'option active est annoncée par l'éditeur (`aria-activedescendant` + `aria-controls` sur le contenu, calculés depuis l'état), nombre de résultats en `role="status"`. Contrôle axe palette ouverte : il a révélé deux défauts de M1-12, corrigés (contraste du raccourci dans « Enregistrer » ; `tabindex="0"` sur le contenu de CodeMirror, que axe ne comptait pas comme focalisable).
- **Écart corrigé dans features.md** : E2E-05-05 disait « deux fois Tab » pour atteindre « Catégories », alors que F05.R3, §A-6.2 et le mockup placent « Pièces jointes » avant : scénario corrigé en « trois fois ».
- **Bouton « Référence »** de la barre d'outils : insère `[[` comme une frappe, donc ouvre la palette.
- Compilateur React : les touches de la palette appellent les derniers gestionnaires par une ref, lue uniquement à l'appui (une seule désactivation commentée de `react-hooks/refs`).
- **Tests** : 13 tests unitaires (état de la palette, requête, fermeture, insertion lien / intégration / raccourci ; classement, accents, libellé des pièces jointes, onglets, 10 000 éléments) ; e2e `E2E-05-03` à `05-06` ; axe éditeur + palette.

**Vérification** : `pnpm typecheck`, `lint`, `format:check`, `test` · `playwright test f05-palette f05-editor a11y` (35 réussis) · `dotnet test --project tests/Erable.Server.Tests` (route des pièces jointes).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
