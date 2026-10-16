# M4-02 — Matrice d'autorisation générée

- **Statut** : review
- **Jalon** : M4
- **Dépend de** : M4-01
- **Références** : §T-7 (Matrice d'autorisation), §T-15

## Objectif

Rendre impossible l'oubli d'autorisation sur un endpoint.

## Périmètre

- Test qui énumère **tous** les endpoints via `EndpointDataSource` et les appelle avec chaque profil : anonyme, lecteur, éditeur, administrateur, membre d'un autre projet.
- Tableau attendu déclaré (métadonnée d'endpoint ou fichier), comparé au résultat réel.
- Un endpoint sans métadonnée d'autorisation explicite fait échouer la suite.
- Le même test vérifie le mode desktop (jeton exigé partout sauf ressources statiques explicitement listées).

## Critères d'acceptation

- [x] Ajouter un endpoint sans règle fait échouer le test (`Matrix_EndpointWithoutARule_FailsTheRun`, `Server_EndpointWithoutARule_RefusesEveryone`).
- [x] Matrice ajoutée à la définition de « terminé » pour tout nouvel endpoint (`CLAUDE.md` §10 et `docs/testing.md` §14 la citent ; §7 décrit la mise en œuvre).

## Notes d'implémentation

**Mise en œuvre**
- `tests/Erable.Server.Tests/Infrastructure/AuthorizationMatrix.cs` énumère les `RouteEndpoint` de l'`EndpointDataSource` du vrai hôte (mode serveur), remplit les paramètres de route (vrai `projectId`, ID aléatoires ailleurs) et appelle chaque méthode avec six profils : anonyme, membre d'un autre projet, lecteur, éditeur, administrateur du projet, administrateur du serveur non membre. Une **session neuve par appel** (la déconnexion termine la session qu'elle reçoit).
- Le tableau attendu est la **métadonnée `AccessRule`** de l'endpoint (§T-7 : « métadonnée d'endpoint ou fichier ») : la matrice vérifie que le comportement réel la respecte pour chaque profil. Un endpoint sans règle est un échec ; un échec de classement (`access.undeclared`) existe aussi à l'exécution.
- Le résultat réel est lu dans le `code` du `ProblemDetails` (`auth.required`, `access.forbidden`, `project.notFound`), pas dans le statut seul : `POST /auth/login` répond légitimement 401 sans que ce soit un refus d'accès.
- **Second regard sur les déclarations** (`CheckDeclaration`) pour qu'une règle affaiblie ne passe pas inaperçue : sous `/projects/{projectId}`, une écriture ouverte aux lecteurs doit figurer dans `AuthorizationMatrix.ReaderMayWrite` (`RecordView`, `PreviewPageProperties`, ouverture/révélation d'une pièce jointe), une lecture ne doit pas exiger un éditeur, et un endpoint de projet doit déclarer un rôle de projet.
- **Desktop** : `Desktop_EveryApiEndpoint_NeedsTheSessionToken` appelle sans jeton chaque endpoint `/api`, `/_test` ou `/hubs` (401 attendu) ; hors ces préfixes, seules les routes de `DesktopOpenRoutes` (coquille de la SPA, `/ipc/activate` qui a son propre jeton) sont ouvertes.
- `TestBackend.StartAsync` accepte `beforeStart` pour ajouter un endpoint de test avant le démarrage (cas de l'oubli).

**À faire quand un endpoint est ajouté** : `.RequireProjectRole(…)`, `.RequireSignIn()`, `.RequireServerAdmin()` ou `.PublicAccess()` (ou un groupe sous `/projects/{projectId}`, qui fournit la règle par défaut selon la méthode HTTP) ; `dotnet test --project tests/Erable.Server.Tests -- --filter-class "*AuthorizationMatrixTests"`.

**Dette** : les tests ne sont pas encore exécutés (consigne : une exécution globale à la fin de M4).

**Message de commit proposé** : `test(server): add the generated authorization matrix (M4-02)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
