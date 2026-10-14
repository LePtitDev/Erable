# M4-01 — Comptes, authentification, rôles, multi-projets

- **Statut** : todo
- **Jalon** : M4
- **Dépend de** : M2-03
- **Références** : §A-2, §A-7.1 (mode serveur), §A-9, F14.R1–R2, features §2.3, décision Q5

## Objectif

Passer d'un projet local mono-utilisateur à un serveur hébergeant plusieurs projets, avec des droits par projet.

## Périmètre

- `data/server.db` : comptes (ASP.NET Core Identity, mots de passe hachés), projets, appartenances (rôle Lecteur / Éditeur / Administrateur par projet).
- `data/projects/<projectId>/` : un dossier de projet standard par projet.
- Authentification : cookie httpOnly (navigateur, `SameSite`, anti-CSRF), jeton bearer (desktop connecté) ; déconnexion invalidant la session ; expiration.
- Politiques d'autorisation par endpoint, **explicites** pour chaque endpoint ; un non-membre reçoit 404 (pas de fuite de nom, E2E-14-04) ou 403 selon une règle unique à décider et documenter.
- `GET /api/v1/projects` filtré par appartenance.
- Écran de connexion (« Identifiant ou mot de passe incorrect », sans préciser lequel) ; avatar dans la barre supérieure.
- Interface en lecture seule pour les lecteurs (aucun bouton d'édition).
- `/_test/login-as/{user}` ; utilisateurs de test de `spec/projects/users.json`.
- Protection contre la force brute (limitation de débit sur la connexion).

## Critères d'acceptation

- [ ] `E2E-14-01` à `E2E-14-04` et `E2E-11-03` verts.
- [ ] Une requête PUT forgée d'un lecteur reçoit 403.

## Points d'attention

- Le mode desktop local ne doit pas être affecté : utilisateur implicite « moi », aucun écran de connexion.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
