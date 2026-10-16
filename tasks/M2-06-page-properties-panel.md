# M2-06 — Propriétés sur la page (lecture et édition)

- **Statut** : review
- **Jalon** : M2
- **Dépend de** : M2-03, M1-12
- **Références** : F04.R4, F05.R6, F07.R3, F08.R3, Mockups « Page — lecture » et « Page — édition » (panneau latéral)

## Objectif

Afficher et saisir les valeurs de propriétés d'une page, avec héritage, conflits et validation en direct.

## Périmètre

- **Lecture** : panneau « Propriétés · N valeurs » (clé, type, valeur, « via <catégorie> »), badge « hors règles » sur la page, pastilles de catégories dans l'en-tête.
- **Édition** : onglets du panneau latéral (Propriétés, Catégories, Liens, Fichiers) ; éditeur par type (interrupteur, liste, champ numérique filtré, sélecteur de page filtré par `in_category`, date…) ; « Valeur héritée de la catégorie X » ; « Rétablir la valeur héritée » ; conflit à résoudre ; « Ajouter une propriété ».
- Ajout / retrait de catégories sur la page.
- Validation instantanée (évaluateur TS), compteur « N erreur(s) de validation » dans la barre d'état, enregistrement jamais bloqué.
- Les valeurs s'enregistrent avec le contenu (même révision).

## Critères d'acceptation

- [x] `E2E-05-12`, `E2E-07-04`, `E2E-07-05`, `E2E-07-06`, `E2E-07-08` (repris de M2-04), `E2E-08-06` verts ; volets « page » de `E2E-08-02`, `08-05`, `08-08`, `08-11` complétés.
- [x] Tests de composant de chaque éditeur de type (`ValueEditor.test.tsx`, M2-04) et du panneau (`PageStructurePanel.test.tsx`).

## Notes d'implémentation

- **Lecture** : section « Propriétés · N valeurs » en tête du panneau latéral (nom lié à la propriété, type, valeur, « via PNJ », message de chaque violation, conflit « En conflit : PNJ (Non) / Montagnard (Oui) »), pastilles de catégories et badge « hors règles » dans l'en-tête.
- **Édition** : panneau à onglets Propriétés / Catégories / Fichiers (`PageStructurePanel`) à droite de l'éditeur. L'onglet Liens arrive avec M2-07. Chaque champ utilise `ValueEditor` ; modifier une valeur héritée la définit sur la page, « Rétablir la valeur héritée » ou la croix la retirent ; un conflit se résout par des boutons portant les valeurs candidates ; « Ajouter une propriété » réutilise `AddPropertyDialog` (extrait de l'écran Catégorie).
- **Une seule logique d'héritage** : pas de seconde implémentation TS de la résolution. Le brouillon (`StructureDraft` : catégories + valeurs propres) est envoyé à un nouvel endpoint `POST pages/{id}/properties/preview` (`StructureState.Overlay`, rien n'est écrit) qui renvoie la résolution que donnerait l'enregistrement ; appel différé de 150 ms, uniquement tant que le brouillon diffère de la page. **Écart avec la tâche** : seule la validation d'une valeur saisie est faite par l'évaluateur TS (immédiate) ; les valeurs héritées et les références (règles `in_category`/`kind`) viennent de l'aperçu.
- **`PagePropertyItem.fallback`** (nouveau) : ce que serait la propriété sans la valeur propre de la page (origine, sources, candidats), nécessaire à « Rétablir la valeur héritée ». OpenAPI et client régénérés.
- **Vider un champ** ne rétablit pas l'héritage (le champ reste vide, la valeur n'est pas enregistrée) : seul le bouton « Rétablir » le fait.
- **Enregistrement** : `PUT pages/{id}/content` avec `categories` et `properties` (remplacement complet, même révision), envoyés seulement si le brouillon de structure a changé. Le brouillon local (IndexedDB) ne couvre toujours que le Markdown : les catégories et valeurs non enregistrées sont perdues si l'on abandonne l'onglet (dette mineure).
- **Barre d'état** : « N erreur(s) de validation » (`shellStore.validationErrors`), l'enregistrement n'est jamais bloqué.
- Vérification : `pnpm -C src/web test` (601 verts), `lint`, `typecheck`, `format:check`, `api:check`, `build` (416 kB gzip) ; `pnpm e2e:pr` (108 verts, Chromium + WebKit) ; `dotnet test --solution Erable.sln` (843, dont 6 nouveaux : aperçu et valeur de repli).
- Commit proposé : `feat(web,server): show and edit page categories and properties with live validation (M2-06)`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
