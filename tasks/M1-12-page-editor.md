# M1-12 — Éditeur CodeMirror

- **Statut** : todo
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

- [ ] `E2E-05-01`, `E2E-05-02`, `E2E-05-08` à `E2E-05-11`, `E2E-05-13` verts.
- [ ] Tests unitaires des extensions sur `EditorState` (décorations, plages atomiques), sans DOM.
- [ ] Aucune frame > 50 ms en frappe sur une page de 5 000 lignes (mesure locale).

## Points d'attention

- P-09 : ne pas reprendre « Alt+Espace » ni « fusionnés en direct » du mockup.
- Les décorations doivent être calculées sur la partie visible (viewport) pour tenir le budget de frappe.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
