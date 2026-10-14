# M1-12 — Éditeur CodeMirror

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-11
- **Références** : F05 (hors R3 et R6), §A-6.2, §T-8.1, Mockup « Page — édition + Alt+Espace (sombre) », P-03, P-09

## Objectif

Un éditeur Markdown source agréable, où les ID sont remplacés visuellement par des noms sans jamais modifier le texte.

## Périmètre

- CodeMirror 6 + `@codemirror/lang-markdown`, thème branché sur les tokens.
- Extension « pastilles » : décoration de remplacement **atomique** pour `[[ID…]]` et `![[ID…]]` (nom résolu, icône de type) ; le curseur saute la pastille, Retour arrière la supprime entière (F05.R1).
- Bascule « Afficher les ID bruts » (F05.R2).
- Barre d'outils : titres, gras, italique, Référence, Pièce jointe (E2E-05-13).
- En-tête d'édition : « Brouillon enregistré · il y a 4 s », « Annuler », « Enregistrer » (`Ctrl+S`).
- Brouillon automatique (F05.R4) — emplacement de stockage selon P-03 ; bandeau « Un brouillon non enregistré existe » (Restaurer / Ignorer).
- Garde-fou de sortie (F05.R5) : Enregistrer / Abandonner / Annuler, y compris à la fermeture de la fenêtre.
- Barre d'état : position du curseur (Ln, Col).
- Enregistrement → `PUT` avec `If-Match` ; un `409` affiche pour l'instant un message clair (la fusion arrive en M4-05).

## Critères d'acceptation

- [x] `E2E-05-01`, `E2E-05-02`, `E2E-05-08` à `E2E-05-11`, `E2E-05-13` verts.
- [x] Tests unitaires des extensions sur `EditorState` (décorations, plages atomiques), sans DOM.
- [x] Aucune frame > 50 ms en frappe sur une page de 5 000 lignes (mesure locale).

## Points d'attention

- P-09 : ne pas reprendre « Alt+Espace » ni « fusionnés en direct » du mockup.
- Les décorations doivent être calculées sur la partie visible (viewport) pour tenir le budget de frappe.

## Notes d'implémentation

- **Dépendances** : `@codemirror/state` 6.7.6, `view` 6.43.14, `commands` 6.11.1, `lang-markdown` 6.5.2, `language` 6.13.1 — les versions validées par le spike M0-08 — plus `@lezer/highlight` 1.2.5 et `@lezer/markdown` 1.8.0 (types de l'extension). Licence MIT.
- **Pastilles** (`src/features/editor/pills.ts`, F05.R1–R2) : décorations de remplacement calculées **sur la partie visible** (`view.visibleRanges`), exposées aussi comme `atomicRanges` (le curseur saute la pastille, Retour arrière la supprime entière). Libellé `[[ID|texte]]`, sinon nom actuel (une seule requête `resolve` pour tout le texte), sinon l'ID ; cible supprimée ou inconnue barrée en rouge ; couleurs du type d'élément. « Afficher les ID bruts » : un champ d'état qui retire les décorations sans toucher au document.
- **Extension Lezer** (`lezerErable.ts`) : `[[ID…]]` et `![[ID…]]` deviennent des nœuds `ErableReference` / `ErableEmbed` de l'analyseur de CodeMirror, reconnus par la grammaire partagée (`referenceSyntax.ts`) **avant** les liens. Les pastilles se lisent dans l'arbre syntaxique, donc jamais dans du code ni après un échappement (R6, R7). **Correctif de performance** trouvé par la mesure : sans elle, chaque `[[` ouvrait deux délimiteurs de lien et `LinkEnd` devenait quadratique dans un long paragraphe (≈ 190 ms par frappe sur 5 000 lignes, profil CDP) ; avec elle, aucune tâche > 50 ms.
- **Écran d'édition** (`src/features/page/PageEditor.tsx`, `?edit=true`) : en-tête (fil d'Ariane, titre modifiable, pastille « Édition », état « Brouillon enregistré · … », Annuler, Enregistrer `Ctrl S`), barre d'outils (titre, gras, italique, liste, code, Référence — insère `[[`, la palette arrive en M1-13 —, Pièce jointe — M1-14 —, « Afficher les ID bruts », indication du raccourci de la palette **Ctrl+Espace**, pas Alt+Espace : P-09), CodeMirror (thème sur les tokens, IBM Plex Mono 14/26, numéros de ligne). Le mode et la position du curseur (Ln, Col) sont dans la barre d'état. Bandeau « fusionnés en direct » du mockup non repris (P-09, §A-8.3).
- **Enregistrement** : `PUT content` avec `If-Match: "<révision>"`, **puis** renommage s'il y a lieu (l'inverse donnerait un 409, le renommage changeant la révision). `409` → message clair (la fusion arrive avec M4-05). Après succès : brouillon supprimé, cache du projet invalidé, retour en lecture (« Modifiée à l'instant par moi »). Les dates de moins de 45 s s'affichent « à l'instant » (`time.justNow`).
- **Brouillon** (F05.R4, P-03) : document `draft-<projet>-<page>` du profil (`me/ui-state`, nouvelle route `DELETE` testée), écrit 600 ms après la dernière frappe et **immédiatement** (`keepalive`) au `pagehide` ou à la fermeture de l'éditeur. À l'ouverture, un brouillon différent du contenu enregistré propose « Restaurer » ou « Ignorer ».
- **Garde-fous** (F05.R5) : `useBlocker` de TanStack Router (navigation interne : boîte Enregistrer / Abandonner / Annuler ; fermeture d'onglet : invite native du navigateur). « Annuler » avec des modifications demande confirmation, puis restaure et supprime le brouillon (E2E-05-11). La fermeture de la fenêtre desktop arrive avec M1-17.
- **Page créée** : `?edit=true&focus=title` (arbre, « Nouvelle sous-page ») : l'éditeur s'ouvre titre focalisé et sélectionné (E2E-02-01 complété).
- **Mesures** : tests `@perf` séparés (projet Playwright `perf`, `pnpm e2e:perf`, un seul worker) — sous une exécution parallèle complète, une mesure de temps n'a pas de sens. Frappe dans une page de 5 000 lignes : aucune tâche longue > 50 ms (`PerformanceObserver` `longtask`, Chromium) ; décorations d'un écran sur 5 000 lignes : < 50 ms (test unitaire).
- **Correctif** (trouvé en exécutant toute la suite en parallèle) : au dépôt dans l'arbre, la position avant / dedans / après est recalculée depuis l'événement `drop` (l'état du dernier `dragover` pouvait ne pas être rendu ; E2E-02-06 sous WebKit).
- **Tests** : 14 tests unitaires sur `EditorState` sans DOM (recherche des références, code et échappements ignorés, plages des décorations, libellés, partie visible seulement, ID bruts, performance ; commandes de mise en forme) ; e2e `E2E-05-01`, `05-02`, `05-08` à `05-11`, `05-13` + mesure de frappe.

**Vérification** : `pnpm typecheck`, `lint`, `format:check`, `test` (319) · `pnpm e2e` (78 réussis, Chromium + WebKit) · `pnpm e2e:perf` (2 réussis) · `dotnet test --solution Erable.sln`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
