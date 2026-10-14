# M1-14 — Pièces jointes (interface)

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-12, M1-06
- **Références** : F06, Mockup « Page — lecture » (panneau Pièces jointes)

## Objectif

Ajouter des fichiers à une page aussi naturellement qu'en les collant.

## Périmètre

- Coller ou déposer un ou plusieurs fichiers dans l'éditeur → téléversement avec progression, puis `![[ID]]` inséré au curseur (F06.R1).
- Panneau « Pièces jointes » : extension, nom, taille, « + Ajouter » (sans insertion dans le texte).
- Rendu des intégrations par type : image, vidéo (streaming), audio, PDF ; autres types en carte téléchargeable ; SVG et HTML **jamais** inline (F06.R2).
- Limite de taille : message clair, aucune pièce jointe créée (F06.R5).
- Actions : ouvrir avec l'application système / révéler dans l'explorateur (desktop, via le pont natif).

## Critères d'acceptation

- [ ] `E2E-06-01` à `E2E-06-05`, `E2E-06-07`, `E2E-06-08` verts. (`E2E-06-06` en M2-08.)
- [ ] Échec réseau pendant un téléversement : aucune pastille orpheline dans le texte.

## Points d'attention

- Pendant le téléversement, insérer un marqueur temporaire non enregistrable, remplacé par l'ID final.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
