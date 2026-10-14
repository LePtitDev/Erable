# M1-13 — Palette d'insertion `[[`

- **Statut** : todo
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

- [ ] `E2E-05-03` à `E2E-05-06` verts.
- [ ] Frappe → propositions < 50 ms sur le projet généré de 10 000 pages (mesure indicative avant M3-06).
- [ ] Tests unitaires de la logique d'ouverture et d'insertion sur `EditorState`.

## Points d'attention

- Classement : correspondance de préfixe > mot > floue ; récents en bonus. Proposer une règle simple et la tester.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
