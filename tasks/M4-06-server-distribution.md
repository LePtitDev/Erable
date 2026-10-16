# M4-06 — `erable serve` et image Docker

- **Statut** : review
- **Jalon** : M4
- **Dépend de** : M4-01
- **Références** : §A-2 (Serveur ad hoc), §A-9 (TLS), §A-10 (Packaging), §T-13 (Release)

## Objectif

Déployer un serveur Erable en une commande.

## Périmètre

- `erable serve ./MonWiki --port 5080` : partage d'un projet local sur le LAN avec comptes locaux ; création du premier administrateur au démarrage (interactive ou par variable d'environnement).
- Image Docker basée sur `mcr.microsoft.com/dotnet/aspnet` (variante *chiseled*), utilisateur non root, volume `data/`, healthcheck.
- Configuration par variables d'environnement (limites de taille, quotas, URL publique, CORS pour une SPA hébergée ailleurs).
- Exemple `docker-compose` avec Caddy (TLS automatique).
- Scan de vulnérabilités de l'image dans la CI.

## Critères d'acceptation

- [ ] L'image démarre, sert la SPA, passe le healthcheck ; e2e `@server` exécutables contre elle. **Non vérifié ici : Docker n'est pas installé sur la machine de développement** ; le workflow `.github/workflows/docker.yml` construit l'image, la scanne, la démarre, attend `healthy`, vérifie la SPA, la connexion, l'utilisateur non root et l'absence de `/_test`. À constater au premier passage du CI. Les e2e `@server` tournent contre `erable serve` (même exécutable) ; les rejouer contre l'image : `ERABLE_E2E_PORT` + `reuseExistingServer` ne suffisent pas (le mode test n'existe pas en Release) — voir dette.
- [x] Documentation d'installation dans `docs/` : `docs/deployment.md`.

## Notes d'implémentation

**Décisions**
- **`erable serve <dossier>`** : un dossier qui contient `erable.json` est un **projet partagé tel quel** (aucun fichier n'y est écrit) ; les comptes vont dans le profil applicatif (`<AppData>/Erable/serve/<projectId>/server.db`), pour qu'on puisse copier ou envoyer le projet sans les mots de passe. Tout autre dossier est un **dossier de données** (`server.db`, `projects/`, `profiles/`), celui de l'image Docker (`/data`). Au démarrage, le dossier partagé est enregistré et son premier administrateur est le premier administrateur du serveur (`ServerStartup.ShareProject`).
- **Adresse par défaut : `127.0.0.1`** (point à trancher, voir Propositions) : partager sur le LAN est un geste explicite (`--lan` = `--address 0.0.0.0`), et la commande l'écrit au démarrage. Raison : HTTP en clair + mots de passe ; l'architecture dit « sur le LAN » sans préciser le défaut. L'image Docker met `ERABLE_ADDRESS=0.0.0.0`.
- **Premier administrateur** : variables `ERABLE_ADMIN_LOGIN` / `ERABLE_ADMIN_PASSWORD` (non interactif, Docker) ou **invite sur le terminal** (identifiant, mot de passe masqué, confirmation) ; ni l'un ni l'autre → le serveur refuse de démarrer (code 2) plutôt que de tourner sans compte. Le mode test n'en a pas besoin (comptes de `users.json`).
- **Configuration** (`ServeConfiguration`, testée) : ligne de commande > `ERABLE_*` > défauts ; **toute valeur invalide arrête le serveur en nommant le réglage** (jamais de repli silencieux). Variables : `ERABLE_PORT`, `_ADDRESS`, `_MAX_ATTACHMENT_MB`, `_PROJECT_QUOTA_MB`, `_PUBLIC_URL`, `_CORS_ORIGINS`, `_TRUST_PROXY`, `_COOKIE_SECURE`, `_SESSION_HOURS`, `_ADMIN_*`.
- **Quota par projet** (§A-9) : total des pièces jointes (corbeille comprise) ; un fichier qui ne tient pas est refusé en cours de réception (`413 attachment.quotaExceeded`), ou avant sa lecture quand la place est épuisée ; le code `attachment.tooLarge` reste celui de la taille maximale.
- **CORS** : politique unique (mode serveur) — `ERABLE_PUBLIC_URL`, `ERABLE_CORS_ORIGINS`, et les pages de **loopback** (l'application de bureau connectée, M4-07, qui s'authentifie par jeton). Avec `AllowCredentials` (SignalR négocie avec des identifiants) : sans risque, le cookie est `SameSite=Strict` et l'écriture par cookie venue d'ailleurs est refusée par l'en-tête `Origin`. Placé avant le contrôle d'accès (une pré-requête n'a pas d'identifiants).
- **Reverse proxy** : `ERABLE_TRUST_PROXY` active `UseForwardedHeaders` (X-Forwarded-For/-Proto/-Host, proxys connus vidés : le serveur n'est joignable que par le proxy) ; l'adresse réelle alimente la limitation des essais, le protocole le cookie `Secure`.
- **Image** (`Dockerfile`) : build en trois étapes (client Vite, publication Release du serveur — sans `/_test` —, `aspnet:10.0-noble-chiseled`), UID 1654, `/data` en volume, `ERABLE_ADDRESS=0.0.0.0`, `ERABLE_PORT=8080`, **`HEALTHCHECK` par `erable healthcheck`** (nouvelle sous-commande : l'image n'a ni shell ni curl). `deploy/` : `docker-compose.yml` (Erable non publié + Caddy), `Caddyfile`, `.env.example` (`deploy/.env` ignoré par Git).
- **CI** : `.github/workflows/docker.yml` — build, scan Trivy (HIGH/CRITICAL corrigeables font échouer), démarrage + attente `healthy`, SPA servie, connexion de l'administrateur, utilisateur non root, `/_test` absent ; aussi chaque lundi (nouvelles vulnérabilités de l'image de base). Actions épinglées par SHA.
- **Documentation** : `docs/deployment.md` (installation, variables, sauvegarde, mise à jour, sécurité, dépannage) ; commandes ajoutées à `CLAUDE.md` §9.
- Tests : `ServeConfigurationTests`, `ServerDeploymentTests` (quota, CORS, proxy, projet partagé et redémarrage, `healthcheck` sur un vrai serveur).

**Dette / à vérifier**
- **Image jamais construite localement** (pas de Docker ici) : la balise `10.0-noble-chiseled`, le `chown` de `/data` et le `HEALTHCHECK` sont à constater au premier passage du CI.
- Rejouer les e2e `@server` contre l'image est impossible tant que `/_test` n'existe pas en Release (par construction, CLAUDE.md §6.12) : on teste le même exécutable en Debug ; la fumée du CI couvre le reste.
- Interdire le HTTP en clair hors loopback sans `ERABLE_PUBLIC_URL` https n'est pas fait (avertissement seulement dans la documentation).
- Les tests ne sont pas exécutés (consigne : fin de M4).

**Message de commit proposé** : `feat(server): add erable serve for shared projects, quotas, CORS, a Docker image and its CI (M4-06)`

## Propositions

- Adresse d'écoute par défaut de `erable serve` : loopback (retenu) ou LAN ? Le texte de §A-2 (« partager un projet local sur le LAN ») pourrait aussi se lire « LAN par défaut avec avertissement ». À valider.

## Propositions

