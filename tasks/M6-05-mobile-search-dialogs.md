# M6-05 — Recherche et boîtes de dialogue sur téléphone

- **Statut** : review
- **Jalon** : M6
- **Dépend de** : M6-02
- **Références** : F18.R7, F10, F15

## Objectif

Rechercher et répondre aux dialogues confortablement sur un petit écran.

## Périmètre

- `Dialog` partagé : plein écran en mise en page étroite (préférences, nouveau projet, confirmations, conflits…).
- Recherche rapide plein écran ; écran Recherche : filtres dans un panneau (bouton « Filtres »), refermé quand un filtre est choisi.

## Critères d'acceptation

- [x] `F18.R7`.
- [x] `E2E-18-06` vert.
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

- **Décision (écart au plan, documents mis à jour)** : le panneau de filtres ne se referme pas à chaque choix (on ne pourrait pas en combiner plusieurs) mais par « Afficher N résultats » ; les confirmations courtes (`size="sm"`) restent des cartes centrées, seules les boîtes `md` et `lg` passent en plein écran. F18.R7 et E2E-18-06 réécrits ; le scénario cherche « ilsa » (la recherche porte sur le texte brut, où une référence est un `[[ID]]`).
- **Boîtes plein écran** : `Dialog.module.css` sous 720 px ; pied collant en bas de l'écran (bouton Fermer / Enregistrer toujours atteignable), marges de sécurité.
- **Recherche rapide** : plein écran ; bouton « Fermer » à la place de l'indication « Échap » avec un pointeur grossier.
- **Écran Recherche** : bouton « Filtres » (« Filtres (actifs) » quand un filtre s'applique) qui ouvre `FiltersPanel` dans un Radix Dialog plein écran ; même composant qu'au bureau.
- **Vérifications** : `playwright test --project mobile` (8 scénarios), Vitest `features/search` et `shared/ui`, lint, typecheck, Prettier ; captures Pixel 7.

## Propositions

- Masquer la section « Raccourcis clavier » des préférences sur un écran tactile sans clavier physique (impossible à détecter de façon fiable : `pointer: coarse` couvre aussi les tablettes avec clavier).
- Le champ « Nom affiché » prend le focus à l'ouverture des préférences, ce qui ouvre le clavier virtuel sur téléphone.
