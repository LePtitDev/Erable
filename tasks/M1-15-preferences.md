# M1-15 — Préférences : langue et thème

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-08
- **Références** : F15.R1–R3, Mockup « Accueil » (langue et thème), P-03

## Objectif

Choisir la langue d'interface et le thème, mémorisés par utilisateur et appliqués sans flash.

## Périmètre

- Préférences stockées côté backend (`GET/PUT /api/v1/me/preferences`) : profil applicatif en local, compte en mode serveur (M4).
- Langue d'interface (Français, English, Deutsch, Español) indépendante de la langue du contenu ; dates reformatées.
- Thème Clair / Sombre / Système ; Système suit l'OS en direct.
- Le backend injecte `lang` et `data-theme` dans `index.html` (pas de flash au premier rendu).
- Écran ou panneau « Préférences » accessible depuis l'accueil et le projet.

## Critères d'acceptation

- [ ] `E2E-15-01`, `E2E-15-02`, `E2E-15-03` verts.
- [ ] Les préférences survivent à un redémarrage du desktop (port différent).

## Points d'attention

- Le choix des raccourcis arrive en M5-01 : prévoir la structure de données, pas l'écran.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
