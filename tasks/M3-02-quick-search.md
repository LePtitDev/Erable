# M3-02 — Recherche rapide Ctrl+K

- **Statut** : review
- **Jalon** : M3
- **Dépend de** : M3-01
- **Références** : F10.R1, F17 (E2E-17-02), Mockup (champ de recherche de la barre supérieure), P-13

## Objectif

Aller n'importe où au clavier en une seconde.

## Périmètre

- `Ctrl+K` (et `⌘K`) ouvre une palette de recherche par nom sur tous les types ; `Entrée` ouvre le premier résultat ; flèches pour naviguer ; `Échap` ferme.
- Récemment consultés proposés quand le champ est vide.
- Lien « Recherche avancée » vers l'écran M3-03 avec le texte saisi.
- Réutiliser la logique de liste de la palette d'insertion (M1-13) si pertinent.

## Critères d'acceptation

- [x] `E2E-10-01` et `E2E-17-02` écrits et verts (Chromium + WebKit ; `f10-search.spec.ts`, `f17-a11y.spec.ts`).
- [x] Accessible : `combobox`, annonce des résultats.

## Notes d'implémentation

- **Ouverture** : `Ctrl+K` / `⌘K` écouté sur `window` en phase de capture dans `ProjectShell` (il passe donc avant CodeMirror, y compris en édition) ; le champ de la barre supérieure est devenu un bouton qui ouvre la même palette. `Ctrl+K` ajouté à `shortcuts.ts` (`quickSearch`) pour M5-01.
- **Palette** (`QuickSearch`) : boîte de dialogue Radix (focus piégé, Échap, retour du focus), champ `role=combobox` avec `aria-activedescendant`, liste `role=listbox`/`option`, région `role=status` qui annonce « N résultats ». Les options sont filtrées par nom sur tous les types (`GET search?nameOnly=true`, 8 résultats, 120 ms de pause), avec chemin et surlignage. Les flèches se bouclent, Entrée ouvre l'option active (la première par défaut), `Ctrl/⌘/Alt+Entrée` ou le bouton « Recherche avancée » ouvrent l'écran M3-03 avec le texte. La palette est montée seulement à l'ouverture : elle repart toujours vide.
- **Vide** : les huit derniers éléments consultés (M3-04), sans appel de recherche.
- **Réutilisation** : la logique de liste de la palette d'insertion (M1-13) n'est pas reprise : elle filtre l'arbre en mémoire et insère un lien ; ici le classement et les accents sont ceux du serveur (FTS5), ce qui évite deux implémentations de « trouver par nom ». Seuls `Highlighted`, `KindBadge` et `RecentList` sont partagés.
- Vérification faite pendant la tâche : Vitest `QuickSearch` (4), Playwright des scénarios `E2E-10-01` (×2) et `E2E-17-02` en Chromium et WebKit, lint, typecheck.
- Commit proposé : `feat(web): add the Ctrl+K quick search (M3-02)`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
