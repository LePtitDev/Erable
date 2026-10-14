# M1-11 — Lecture d'une page

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-09
- **Références** : F03, F04, §A-6, §A-9, Mockup « Page — lecture »

## Objectif

Afficher une page fidèle au mockup, avec des références toujours à jour et un rendu sûr.

## Périmètre

- En-tête : fil d'Ariane, titre, bouton « Modifier », ID affiché `XXXX XXXX XXXX XXXX` copiable au format canonique avec confirmation (F03.R2), pastilles de catégories (vides jusqu'à M2), « Modifiée il y a … par … · créée le … ».
- Rendu unified : `remark-parse` + `remark-gfm` + plugin Erable (M1-04) → composants React ; `rehype-sanitize` ; HTML brut affiché échappé (F04.R5).
- Références : nom **actuel** via `resolve` groupé et mis en cache ; `[[ID|texte]]` ; ancres ; lien cassé stylé avec infobulle (F04.R2).
- Intégrations `![[ID]]` : image, vidéo, audio, PDF, sinon carte de fichier ; légende « Pièce jointe de <page> » si elle appartient à une autre page.
- Sommaire (niveaux 2 et 3), défilement, ancre dans l'URL, entrée active (F04.R3).
- Aperçu au survol d'une référence (E2E-04-03).
- Section « Sous-pages » + « Nouvelle sous-page ».
- Panneau latéral : pièces jointes en M1 ; Propriétés et Liens (F04.R4) ajoutés en M2-06 et M2-07, emplacements prévus.
- Retour arrière : restauration de la position de défilement.
- Corps des pages en Source Serif 4 (mockup).

## Critères d'acceptation

- [ ] `E2E-03-01`, `E2E-03-02`, `E2E-04-01` à `E2E-04-04`, `E2E-04-06`, `E2E-04-07` verts. (`E2E-04-05` en M2-08.)
- [ ] Corpus XSS (§T-8.1) : aucun élément exécutable produit.
- [ ] Ouverture d'une page < 200 ms (mesure via `performance.mark`).

## Points d'attention

- Une page qui référence 200 éléments ne doit déclencher qu'un appel `resolve`.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
