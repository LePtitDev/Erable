# M4-08 — Gestion des membres

- **Statut** : review
- **Jalon** : M4
- **Dépend de** : M4-01
- **Références** : F14.R1, E2E-14-10, décision Q5

## Objectif

Permettre à un administrateur de projet de gérer qui y accède.

## Périmètre

- Écran « Membres » (administrateurs seulement) : liste, ajout d'un compte existant, changement de rôle, retrait.
- Création de comptes : par l'administrateur du serveur (pas d'inscription libre en v1, sauf décision contraire).
- Changement de rôle pris en compte au rafraîchissement de la session.
- Impossible de retirer le dernier administrateur d'un projet.

## Critères d'acceptation

- [x] `E2E-14-10` écrit (`e2e/specs/f14-multiuser.spec.ts`) ; exécuté en fin de M4. Côté API : `MembersApiTests`, `MemberRulesTests`, `ServerStoreTests`.


## Notes d'implémentation

**Décisions**
- **Règle du dernier administrateur** (`MemberRules.LeavesNoAdministrator`, Core) appliquée **dans la transaction** de `ServerStore.ChangeRole` / `RemoveMemberChecked` : deux administrateurs qui se retirent en même temps ne peuvent pas laisser le projet sans administrateur (test de concurrence). Refus : `409 member.lastAdmin`.
- **API** (`MemberEndpoints`) : `GET/PUT/DELETE /api/v1/projects/{id}/members[/{userId}]` réservés aux administrateurs du projet ; `GET/POST/PATCH /api/v1/accounts` réservés à l’administrateur du serveur (pas d’inscription libre en v1). Désactiver un compte termine ses sessions et l’éjecte des projets connectés (`RevokeEverywhereAsync`) ; le dernier administrateur du serveur ne peut être ni désactivé ni rétrogradé (`account.lastAdmin`).
- **Prise en compte immédiate du rôle** : un changement diffuse `MembersChanged` au groupe du projet ; le client recharge le résumé du projet (donc `myRole`) et la liste des membres. Plus rapide que « au rafraîchissement de la session » demandé par la tâche ; le serveur vérifie de toute façon le rôle à chaque requête.
- **Interface** : écran `/p/$projectId/members` (lien « Membres » dans le pied de l’explorateur, visible pour un administrateur d’un projet hébergé — serveur ou serveur connecté ; non-administrateur → « Accès refusé »). Tableau avec sélecteur de rôle et bouton « Retirer » ; ajout d’un compte existant par son identifiant ; « Créer un compte » (administrateur du serveur) crée le compte puis l’ajoute avec le rôle choisi.
- Tests : Vitest `Members.test.tsx` (passe), xUnit (`MemberRulesTests`, `ServerStoreTests`, `MembersApiTests`), Playwright `E2E-14-10`.

**Dettes** : tests .NET et e2e non lancés à ce stade (consigne : tests en fin de M4).

**Message de commit proposé** : `feat(server,web): add the members screen and account administration (M4-08)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
