# M4-03 — Événements temps réel

- **Statut** : todo
- **Jalon** : M4
- **Dépend de** : M4-01
- **Références** : §A-8.2, F14.R4, §T-7 (SignalR)

## Objectif

Qu'une modification soit visible chez les autres sans rechargement ni polling.

## Périmètre

- Hub `/hubs/project` authentifié ; groupe par projet, accès contrôlé par l'appartenance.
- Événements `ElementChanged`, `ElementMoved`, `ElementDeleted`, `TreeChanged` émis **après** commit de la transaction.
- Client : connexion avec reconnexion automatique, invalidation ciblée du cache TanStack Query, resynchronisation complète après reconnexion.
- Indicateur de connexion dans la barre d'état (« Connecté à … · synchronisé »).
- Fonctionne aussi en mode desktop local (même code), utile pour plusieurs fenêtres.

## Critères d'acceptation

- [ ] Test d'intégration : A modifie → B reçoit l'événement, un client d'un autre projet non.
- [ ] `E2E-14-06` vert.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
