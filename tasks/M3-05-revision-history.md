# M3-05 — Historique des révisions

- **Statut** : review
- **Jalon** : M3
- **Dépend de** : M1-05
- **Références** : F13, §A-7.4 (Historique), §A-8.1 (`revisions`)

## Objectif

Consulter, comparer et restaurer les versions d'une page sans jamais réécrire l'historique.

## Périmètre

- Instantané de révision complet : contenu, catégories, valeurs de propriétés (F13.R1) — vérifier que M1-05/M2-03 l'assurent.
- API : liste (auteur, date, résumé), lecture d'une révision, diff entre deux révisions (texte ligne/mot + propriétés), restauration = **nouvelle** révision « Restauration de la révision N » (F13.R3).
- Écran Historique : liste, sélection de deux révisions, vue de diff, bouton Restaurer.
- Algorithme de diff : bibliothèque éprouvée (côté C# ou TS, à justifier), réutilisable pour la vue de conflits de M4-05.

## Critères d'acceptation

- [x] `E2E-13-01` à `E2E-13-03` écrits et verts (`f13-history.spec.ts`, Chromium + WebKit).
- [x] Propriété : restaurer la révision N donne un contenu identique à N.

## Points d'attention

- Politique de rétention des révisions non définie : signaler la croissance sur les gros projets et proposer une option (ex. compactage des révisions anciennes) **sans** l'implémenter.

## Notes d'implémentation

- **Aucune migration** : F13.R1 est déjà vrai (le snapshot d'une page contient texte, catégories et valeurs propres, M1-05/M2-03 ; vérifié par les tests). Seul ajout au snapshot JSON : `restoredFrom` (absent des anciennes révisions), qui évite une colonne `summary` et donc un format v5.
- **Diff** : DiffPlex 1.9.0 (MIT, sans dépendance) dans `Erable.Core` (`History/TextDiff`), choisi côté C# parce que P-05 place la fusion à trois voies de M4-05 dans Core : une seule implémentation du diff pour l'historique et la fusion. DiffPlex travaille ligne par ligne puis mot par mot dans une ligne modifiée ; `TextDiff` rend des enregistrements simples. Propriété testée : les lignes d'un côté reconstituent exactement chaque texte. Un texte vide n'a aucune ligne (≠ une ligne vide).
- **API** : `GET elements/{id}/revisions` (plus récente d'abord ; `changes` calculés en comparant chaque snapshot au précédent : created, name, text, categories, properties, other), `GET …/revisions/{n}`, `GET …/revisions/diff?from=&to=` (l'ordre importe peu, la plus ancienne est `from`) et `POST …/revisions/{n}/restore`. Noms des catégories et propriétés pris dans l'arbre actuel.
- **Restauration** (`RestoreRevisionAsync`) : une transaction, **nouvelle** révision marquée « Restauration de la révision N » (F13.R3) ; l'historique existant n'est jamais modifié (test). Restaure contenu, catégories et valeurs propres ; ne touche ni au nom ni à l'emplacement de la page. Une catégorie ou un format de valeur qui n'existe plus est ignoré et compté (`skippedCategories`/`skippedValues`, affiché). Réservé aux pages vivantes. Index de recherche, références et valeurs dérivées sont mis à jour (le doctor reste sain).
- **Interface** : bouton « Historique » sur la page, écran `/p/:projectId/e/:elementId/history` (liste, deux cases à cocher pour comparer, bouton Restaurer par ligne sauf la plus récente). Les deux révisions comparées sont dans l'URL (`?from=&to=`). Le diff montre nom, catégories et propriétés modifiés puis le texte avec contexte de 2 lignes et plis (`…`) ; ajout/suppression sont dits par `<ins>`/`<del>` et un signe, pas par la seule couleur.
- **Dette / hors périmètre** : pas de rétention. **Croissance** : chaque enregistrement stocke un snapshot complet (Brotli niveau rapide), donc le coût est proportionnel à la taille de la page × nombre d'enregistrements (une page de 20 Ko enregistrée 1 000 fois ≈ quelques Mo compressés). Les gros projets collaboratifs grossiront surtout par la table `revisions` ; voir la proposition ci-dessous. L'historique des éléments autres que les pages n'a pas d'écran (l'API le sert).
- Vérification faite pendant la tâche : tests xUnit `TextDiffTests`, `HistoryStorageTests`, `HistoryApiTests` ; Vitest `features/history` (8) ; Playwright `E2E-13-01` à `03` en Chromium et WebKit ; lint, typecheck.
- Commit proposé : `feat(core,storage,server,web): add revision history, comparison and restoration (M3-05)`.

## Propositions

- Compactage des révisions anciennes (non implémenté, comme demandé) : au-delà de N révisions ou d'un âge, ne garder qu'une révision par jour par auteur, et toujours la première, la dernière et les restaurations. À décider avec une politique par projet (réglage du projet, jamais automatique sans accord) et avec `erable doctor` pour mesurer.
- Stocker les révisions successives d'une page en deltas (DiffPlex sait les produire) : gain de place important sur les longues pages, au prix d'une reconstruction à la lecture.
- Un écran d'historique pour les catégories, propriétés et types de lien (l'API les sert déjà).
