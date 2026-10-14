# M4-07 — Desktop en mode connecté

- **Statut** : todo
- **Jalon** : M4
- **Dépend de** : M4-01, M1-17
- **Références** : §A-2 (Desktop connecté, compatibilité de versions), F01 (E2E-01-06), F16.R4, E2E-16-06

## Objectif

Utiliser l'application desktop sur un projet hébergé.

## Périmètre

- « Se connecter à un serveur » sur l'accueil : URL, puis identifiants ; jeton bearer stocké de façon sûre (coffre de l'OS via le shell natif, jamais en clair).
- `GET /api/info` appelé **avant toute autre requête** ; version incompatible → message avec les versions en présence (F16.R4).
- La SPA embarquée parle au serveur distant (CORS configuré côté serveur).
- Récents : entrée « Serveur » avec le mode affiché.

## Critères d'acceptation

- [ ] `E2E-01-06`, `E2E-16-06` verts.

## Points d'attention

- Le stockage du jeton dans le coffre de l'OS diffère selon les plateformes : proposer une bibliothèque ou une implémentation par OS avant de coder.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
