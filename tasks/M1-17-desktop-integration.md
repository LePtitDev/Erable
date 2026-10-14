# M1-17 — Intégration desktop native

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-08, M0-11
- **Références** : F16.R2–R3, E2E-16-03 à E2E-16-05, E2E-16-07, §T-10, P-04

## Objectif

Que l'application desktop se comporte comme une vraie application native.

## Périmètre

- Sélecteur de dossier natif (`Ctrl+O`), ouvrir avec l'application système, révéler dans l'explorateur.
- Mono-instance par projet : la seconde instance transmet sa demande à la première (IPC local) qui passe au premier plan (F16.R2, P-04).
- Fermeture de fenêtre : demande à la SPA s'il y a des modifications non enregistrées, affiche Enregistrer / Abandonner / Annuler, puis arrête proprement (checkpoint WAL, aucun processus résiduel) (F16.R3).
- Glisser un dossier sur l'accueil (E2E-16-07).
- Titre de fenêtre = projet courant ; icône d'application provisoire.
- Scénario de fumée desktop enrichi : créer un projet, une page, une référence, une image.

## Critères d'acceptation

- [ ] `E2E-16-03`, `E2E-16-04`, `E2E-16-05`, `E2E-16-07` verts sur Windows (CDP) ; self-test macOS/Linux enrichi.
- [ ] `E2E-16-01` (fumée complète hors recherche) vert sur les 3 OS.

## Points d'attention

- Le chemin d'un dossier déposé n'est pas toujours accessible depuis le WebView : passer par le shell natif si nécessaire (cf. M0-08).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
