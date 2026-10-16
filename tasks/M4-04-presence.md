# M4-04 — Présence

- **Statut** : review
- **Jalon** : M4
- **Dépend de** : M4-03
- **Références** : §A-8.2 (Présence), F14.R3, Mockups « Page — lecture » (avatars) et « Page — édition » (bandeau), P-09

## Objectif

Savoir qui consulte ou modifie quelle page, et quelle section.

## Périmètre

- Messages de présence : page consultée, mode (lecture / édition), section courante (titre englobant le curseur).
- Avatars (initiales) dans la barre supérieure et sur la page ; « +N ».
- Bandeau d'édition : « Camille R. modifie aussi cette page — section « Histoire ». Vos changements seront fusionnés à l'enregistrement. » (libellé v1, P-09).
- Expiration des présences fantômes (déconnexion brutale) < 30 s.
- Barre d'état « Connecté · N éditeurs ».

## Critères d'acceptation

- [x] `E2E-14-05` écrit (`f14-multiuser.spec.ts`) ; exécuté en fin de M4.
- [x] Test d'intégration de la déconnexion brutale (`PresenceTests.Presence_OfAClientThatStopsAnswering_ExpiresWithoutAGoodbye`).

## Notes d'implémentation

**Décisions**
- **Protocole** : le client appelle `SetPresence(projectId, elementId?, mode, section?)` (mode `read` / `edit`) sur un projet qu'il a rejoint ; le serveur tient l'état par connexion (`ProjectConnections`) et diffuse à tout le groupe un instantané complet `PresenceChanged { projectId, users[] }` à chaque changement (jonction, mise à jour, départ, connexion perdue). Un utilisateur avec plusieurs onglets est une seule ligne : celle de sa connexion la plus récemment active. `JoinProject` renvoie aussi l'instantané. La section n'est conservée qu'en mode `edit` et tronquée à 200 caractères ; valeurs invalides ou projet non rejoint : ignorés.
- **Fantômes** : `HubKeepAlive` 5 s, `HubClientTimeout` 15 s (`ErableHostOptions`, réglables par les tests) ; un client muet depuis 15 s est déconnecté, sa présence disparaît chez les autres (borne basse de « < 30 s » avec la marge des intervalles de ping ; une fermeture normale est immédiate).
- **Client** : un seul rapporteur, `useProjectEvents`, qui envoie la présence depuis le store du shell (`viewingId` posé par `ElementRoute`, `mode`, `section` posée par l'éditeur) ; la présence est renvoyée après chaque reconnexion (le serveur a perdu l'état de l'ancienne connexion). La section vient de `sectionAt` (dernier titre au plus tard sur la ligne du curseur, hors blocs de code, sans ancre explicite), recalculée 300 ms après le dernier mouvement.
- **Interface** : pastilles d'initiales des autres dans la barre supérieure (3 au plus puis « +N », anneau pour ceux qui modifient) et dans l'en-tête de la page qu'ils consultent ; bandeau « Camille R. modifie aussi cette page — section « Histoire ». Vos changements seront fusionnés à l'enregistrement. » (libellé v1, P-09) pour tout utilisateur sur une page que quelqu'un d'autre modifie, lecteur ou éditeur ; barre d'état « Connecté à … · synchronisé · N éditeurs » (N = utilisateurs en mode édition dans le projet, soi-même compris, affiché s'il est > 0). Libellé de l'énoncé « Connecté · N éditeurs » enrichi du suffixe d'M4-03.
- Tests : `PresenceTests` (instantané à la jonction, section, mode lecture sans section, valeurs invalides, projet non rejoint, deux onglets, départ, isolation des projets, silence prolongé) ; Vitest `sectionAt`, bandeau, avatars.

**Dettes**
- Tests non exécutés (consigne : fin de M4).
- Le bandeau annonce la fusion « à l'enregistrement » : elle n'est effective qu'avec M4-05.

**Message de commit proposé** : `feat(server,web): add presence, avatars and the shared-editing banner (M4-04)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
