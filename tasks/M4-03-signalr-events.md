# M4-03 — Événements temps réel

- **Statut** : review
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

- [x] Test d'intégration : A modifie → B reçoit l'événement, un client d'un autre projet non (`HubEventsTests`).
- [x] `E2E-14-06` écrit (`f14-multiuser.spec.ts`) ; exécuté en fin de M4.

## Notes d'implémentation

**Décisions**
- **Serveur** : hub `/hubs/project` (`ProjectHub : Hub<IProjectClient>`), authentifié (`RequireSignIn`). Un client appelle `JoinProject(projectId)` : refusé (`joined: false`, rien d'autre) si le projet est inconnu ou si l'utilisateur n'en est pas membre ; sinon il entre dans le groupe `project:<id>`. `ProjectConnections` suit qui est connecté à quoi (base de la présence, M4-04) ; `ProjectEvents.RevokeAsync` sort un membre retiré du groupe sans attendre sa reconnexion (utilisé par M4-08).
- **Événements** (`ElementChanged`, `ElementMoved`, `ElementDeleted`, `TreeChanged`) émis par `ProjectEventsMiddleware` **après** la réponse d'une écriture réussie (2xx) : la transaction est validée à ce moment. Une écriture refusée (403, 409…) n'émet rien, ni `POST me/views` ni les aperçus. Un seul point d'émission pour tous les endpoints : un endpoint ajouté plus tard émet sans code supplémentaire (au minimum `TreeChanged` + `ElementChanged` si la route porte `{id}`). Une sauvegarde de page n'émet que `ElementChanged` (avec la révision lue dans l'`ETag`).
- **Authentification du hub** : cookie ou `Authorization: Bearer` ; pour les WebSocket sans en-tête, `?access_token=` accepté sur `/hubs` seulement. Délais : keep-alive 5 s, `ClientTimeoutInterval` 15 s (base de l'expiration des présences fantômes, M4-04).
- **Client** : `@microsoft/signalr` 10.0.11 (MIT, client officiel ; **nouvelle dépendance justifiée** : négociation, repli de transport, reconnexion et keep-alive à ne pas réécrire ; chargée à la demande, hors du bundle initial qui reste à 435 Ko gzip pour un budget de 500 Ko). `connectToProject` rejoint le projet, reconnexion automatique sans fin (0, 1, 2, 4, 8 puis 10 s), nouvelle jonction et **resynchronisation complète** après chaque reconnexion, nouvelle tentative si la première connexion échoue.
- **Invalidation ciblée** (`shared/realtime/invalidation.ts`) : `ElementChanged` rafraîchit ce qui est à l'écran du projet sauf l'arbre ; `TreeChanged` tout le projet ; dans les deux cas **la page en cours d'édition est laissée telle quelle** (l'éditeur garde son brouillon, le conflit se règle à l'enregistrement, M4-05). `RealtimeContext` permet aux tests unitaires de ne pas se connecter.
- **Barre d'état** : « Connecté à <hôte> · synchronisé » / « Connexion… » / « Connexion perdue · reconnexion… » / « Hors ligne » en mode serveur ; « Local » sur le desktop. Le hub fonctionne aussi sur le desktop (même code), sans rien afficher de plus.
- Tests : `HubEventsTests` (SignalR.Client en long polling sur TestServer : diffusion, isolation entre projets, non-membre refusé, écriture refusée muette, renommage/suppression/création, membre révoqué, 401 sans identifiants, déconnexion) ; Vitest sur l'invalidation et le délai de reconnexion.

**Dettes**
- Tests non exécutés (consigne : fin de M4). La matrice d'autorisation couvre `/hubs/project` (règle « connecté »).
- `ElementMoved` et `ElementDeleted` déclenchent la même invalidation qu'`ElementChanged` ; une réaction plus fine (fermer une page supprimée par un autre) est possible plus tard.

**Message de commit proposé** : `feat(server,web): add the project hub and live invalidation of the cache (M4-03)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
