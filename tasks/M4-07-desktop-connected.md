# M4-07 — Desktop en mode connecté

- **Statut** : review
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

- [x] `E2E-01-06`, `E2E-16-06` écrits (`e2e/desktop/connected.spec.ts`, binaire publié, Windows/CDP) ; exécutés en fin de M4. Côté API, les mêmes scénarios sont couverts par `RemoteServersTests`.

## Points d'attention

- Le stockage du jeton dans le coffre de l'OS diffère selon les plateformes : proposer une bibliothèque ou une implémentation par OS avant de coder.

## Notes d'implémentation

**Point à challenger (traité, à valider)** — *constat* : la tâche prévoit que « la SPA embarquée parle au serveur distant (CORS) » avec un jeton stocké dans le coffre de l'OS ; or une SPA qui appelle le serveur directement a le jeton en mémoire JavaScript, ne peut pas authentifier les `<img>`, `<video>` et téléchargements (pas d'en-tête `Authorization`, cookie `SameSite=Strict` non envoyé entre origines), et dépend du CORS du serveur. *Option retenue* : **le backend du bureau fait proxy** — la page parle toujours à sa propre origine (`/api/v1/projects/<id>/…`, `/hubs/project?projectId=<id>`) ; le backend reconnaît les projets d'un serveur et transmet la requête avec le jeton (YARP `IHttpForwarder`). Le jeton ne sort jamais du backend, médias et WebSocket fonctionnent sans changement, aucun CORS n'est nécessaire (celui de M4-06 reste pour les SPA tierces). *Coût* : une dépendance (`Yarp.ReverseProxy` 2.3.0, MIT, Microsoft) et un composant à maintenir. *Alternative* : appels directs + jeton en mémoire de la page (plus simple, mais sans médias authentifiés). → décision **Q9** ajoutée à `docs/architecture.md`.

**Décisions**
- **Connexion** (`RemoteServers`) : `POST /api/v1/servers/probe` fait `GET /api/info` et rien d'autre ; l'API `v1` est la seule comprise (`compatible`), la réponse porte les deux jeux de versions. `POST /servers/connect` sonde d'abord (incompatible → `422 server.incompatible` avec les versions, **sans autre requête**, F16.R4), puis `POST /auth/login` avec `issueToken` ; l'identifiant et le mot de passe passent du formulaire au backend local (boucle locale) et le jeton va au coffre. Une réponse inconnue ou illisible → `server.notErable` (502) ; injoignable → `server.unreachable` (502). L'adresse est réduite à son origine (`https://` ajouté si absent ; les identifiants dans l'adresse sont refusés).
- **Coffre** (`ISecretStore`) : **Windows : DPAPI** (`ProtectedData`, portée utilisateur courant, entropie liée à la clé ; fichier dans `<profil>/secrets/`) ; **macOS : Keychain** par `security -i` (secret sur l'entrée standard, jamais en argument) ; **Linux : libsecret** par `secret-tool` ; **sans coffre : mémoire seulement** (`ServerList.tokensSurviveRestart = false`, la liste le dit et l'on se reconnecte à chaque lancement). Jamais de jeton en clair sur disque, jamais dans la page (`RemoteServersTests` vérifie que la liste ne le contient pas). **macOS et Linux ne sont pas testés ici** (le code est court et se replie sur la mémoire en cas d'échec). La « proposition avant de coder » demandée par la tâche est ce choix et le point ci-dessus ; à valider.
- **Registre** (`ServerRegistry`, `<profil>/servers.json`) : serveurs (identifiant dérivé de l'adresse : se reconnecter met à jour la même entrée, nom, compte, projets connus). Les projets qu'un serveur a listés sont routés vers lui, y compris après un redémarrage (identifiants persistés).
- **Proxy** (`RemoteProxyMiddleware`, avant les règles d'accès locales) : retire cookie, `Origin`, `Referer`, `X-Erable-Session` et `Sec-Fetch-*`, pose `Authorization: Bearer` ; retire les `Set-Cookie` du serveur ; ajoute `X-Erable-Remote-Server` à chaque réponse ; une réponse **401** efface le jeton (le serveur apparaît « déconnecté ») ; serveur injoignable → `502 server.unreachable`.
- **Interface** : l'action « Se connecter à un serveur » (désormais active) ouvre `ConnectServerDialog` (adresse → vérification des versions → compte) ; `ServersSection` liste les serveurs dans l'accueil avec le mode « Serveur » (E2E-01-06), leurs projets (liens), « Se reconnecter » et « Retirer » ; la barre d'état d'un projet de serveur affiche « Connecté à <hôte> · synchronisé » ; `useCurrentUserId` rend « moi » = le compte du serveur dans ces projets (auteurs, présence, avertissement de modification par un autre) ; `RemoteReloginDialog` redemande le mot de passe si le jeton prend fin en cours de travail et **rejoue** la requête (même mécanisme qu'E2E-14-09) ; le hub se connecte avec `?projectId=` pour que le bureau l'aiguille.
- Les préférences, l'état de l'interface (arbre, brouillons) et la liste des projets récents restent **locaux** (profil du bureau), y compris pour un projet de serveur (décision Q7 : rien dans le navigateur).
- Tests : `RemoteServersTests` (adresses, sonde, serveur d'une autre version = une seule requête `GET /api/info`, connexion, jeton hors de la page, projets, transmission lecture/écriture sous l'identité de l'utilisateur, lecteur refusé par le serveur, session locale non transmise, jeton révoqué → 401 et « déconnecté », serveur éteint → 502, retrait), `SecretStoreTests` (DPAPI sous Windows), Vitest du dialogue de connexion.

**Dettes**
- Tests non exécutés (consigne : fin de M4).
- Coffre macOS/Linux non éprouvé ; pas de rafraîchissement automatique de la liste des projets d'un serveur (rouvrir la ligne).
- Une SPA servie par un serveur reste en cookie ; le jeton `Bearer` du bureau ne sert qu'au backend local.

**Message de commit proposé** : `feat(server,web): connect the desktop application to a server through a local forwarding proxy (M4-07)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
