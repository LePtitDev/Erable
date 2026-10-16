# M4-01 — Comptes, authentification, rôles, multi-projets

- **Statut** : review
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

- [x] `E2E-14-01` à `E2E-14-04` et `E2E-11-03` écrits (`src/web/e2e/specs/f14-multiuser.spec.ts`) ; exécutés en fin de M4.
- [x] Une requête PUT forgée d'un lecteur reçoit 403 (`AuthApiTests`, `E2E-14-03`).

## Points d'attention

- Le mode desktop local ne doit pas être affecté : utilisateur implicite « moi », aucun écran de connexion.

## Notes d'implémentation

**Décisions**
- **Identity** : seul le hacheur de mots de passe d'ASP.NET Core Identity (`PasswordHasher<T>`, PBKDF2 avec sel, migration d'algorithme automatique) est utilisé. Ni EF Core ni schéma de cookie Identity : les sessions doivent pouvoir être révoquées côté serveur (déconnexion, changement de mot de passe, compte désactivé) et le stockage est Dapper (§A-3). Écart assumé avec la lettre de §A-9 (« ASP.NET Core Identity »).
- **Sessions** : jeton opaque de 256 bits ; seule son empreinte SHA-256 est stockée (`server.db`, table `sessions`). Cookie `erable_auth` (`HttpOnly`, `SameSite=Strict`, `Secure` en HTTPS ou si `CookieSecure`) pour le navigateur, `Authorization: Bearer` pour le desktop connecté (`issueToken`). Durée glissante (12 h par défaut, renouvelée à mi-vie), horloge = `TimeProvider`.
- **Anti-CSRF** : cookie `SameSite=Strict` + contrôle de l'en-tête `Origin` (et `Sec-Fetch-Site`) sur toute écriture authentifiée par cookie (`access.crossOrigin`).
- **Non-membre → 404** `project.notFound` (même réponse qu'un projet inexistant : aucune fuite, F14.R2) ; **rôle insuffisant → 403** `access.forbidden`. L'interface affiche « Accès refusé » dans les deux cas côté serveur. Règle unique, à reporter dans §A-9 (proposition de décision **Q8**).
- **Autorisation** : métadonnée d'endpoint `AccessRule` (`Public`, `Authenticated`, `ServerAdmin`, `ProjectReader/Editor/Admin`) appliquée par `AccessControlMiddleware` (après les fichiers statiques). Sans règle : refus (fail closed). Dans un projet : GET/HEAD → lecteur, toute autre méthode → éditeur, sauf déclaration explicite (`RequireProjectRole`) : `POST me/views` et l'aperçu des propriétés restent accessibles aux lecteurs. La matrice générée est M4-02.
- **Desktop** : `AccessControlMiddleware` donne l'utilisateur « local » (administrateur) ; aucun écran de connexion ; `/api/v1/auth/*` répond 404.
- **Projets** : `data/projects/<id>/`, enregistrés dans `server.db` (table `projects`) avec leurs membres ; ouverts au démarrage (`ServerStartup`). Le créateur est administrateur. `POST /projects` sans `folder` crée le projet sous `data/projects` (administrateur du serveur) ; `folder`, `open` et `inspect` (chemins de la machine) sont réservés au desktop et au mode test (`ErableHostOptions.FolderProjects`), `404 feature.folderProjects` sinon. `ApiInfo` annonce `folderProjects`.
- **Profils** : en mode serveur, un profil par compte (`data/profiles/u-<login>`) pour préférences, projets récents et état de l'interface (décision Q7).
- **Force brute** : `LoginThrottle` (en mémoire) — 5 échecs par identifiant et 40 par adresse sur 15 min → 429 + `Retry-After`, même avec le bon mot de passe. Les trois échecs (inconnu, mauvais mot de passe, désactivé) ont la même réponse et un temps comparable (hachage factice).
- **Premier administrateur** : `ERABLE_ADMIN_LOGIN` / `ERABLE_ADMIN_PASSWORD` à la création du serveur vide (documentation d'installation : M4-06).
- **Mode test** : comptes de `spec/projects/users.json` (mots de passe de test ajoutés) créés au démarrage ; `POST /_test/reset?user=&other=` connecte le test (administrateur par défaut, vide = anonyme), inscrit les membres du projet et, avec `other=true`, crée « Autre projet » (administré par « externe ») ; `POST /_test/login-as/{user}[?token=true][&slot=]` ; `POST /_test/expire-sessions`. La liste des projets d'un test ne montre que ceux de son *slot*.
- **Interface** : `AuthGate` (écran de connexion à la place de l'application tant que `/me` répond 401 en mode serveur), `ReloginDialog` (401 en cours de travail : le client rejoue la requête après reconnexion, sauf les envois multipart — voir M4-05/E2E-14-09), menu de l'avatar (déconnexion = rechargement complet de la page), accueil « Projets du serveur » (l'accueil local — dossiers, récents — n'apparaît que si `folderProjects`), `ProjectAccessContext` / `useCanEdit()` : un lecteur ne voit aucun bouton d'écriture (modifier, lier, joindre, sous-page, actions de l'arbre, corbeille, restauration) et le lien `?edit=true` ouvre la lecture. `ProjectSummary.myRole` porte le rôle.

**Dettes / à vérifier**
- Les tests (xUnit, Vitest, Playwright) n'ont pas été lancés à la fin de cette tâche, sur demande de l'utilisateur : exécution globale en fin de M4.
- Les textes d'interface sont traduits en fr/en/de/es ; vérifier l'allemand et l'espagnol à la relecture.
- Ajout de comptes par l'API : M4-08.

**Commandes de vérification** : `dotnet test --solution Erable.sln`, `pnpm -C src/web test`, `pnpm -C src/web e2e -- f14`.

**Message de commit proposé** : `feat(server,web): add accounts, sessions, project roles and the login screen (M4-01)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
