# Erable — Installer un serveur

> Statut : version 0.1 · M4-06 · complète `docs/architecture.md` §2, §9 et §10.
> Public : la personne qui met Erable à disposition d'une équipe.

Erable existe sous trois formes d'hébergement. C'est **le même exécutable** (`erable`) dans les trois cas.

| Besoin | Forme | Page |
|---|---|---|
| Partager un wiki avec des collègues, ce soir, sur le même réseau | `erable serve ./MonWiki --lan` | [§1](#1-partager-un-projet-local-erable-serve) |
| Héberger plusieurs projets pour une ou plusieurs équipes | Image Docker | [§2](#2-image-docker) |
| Idem, accessible depuis Internet, avec HTTPS automatique | Docker Compose + Caddy | [§3](#3-docker-compose-et-https-caddy) |

---

## 1. Partager un projet local : `erable serve`

```bash
erable serve ./MonWiki --lan --port 5080
```

- `./MonWiki` est un dossier de projet Erable (il contient `erable.json`). Il est partagé **tel quel** : aucun fichier n'y est ajouté.
- Les comptes (identifiants, mots de passe hachés, sessions) sont rangés **à part**, dans le profil de l'utilisateur (`%APPDATA%\Erable\serve\<identifiant du projet>` sous Windows, `~/.config/Erable/serve/…` sous Linux, `~/Library/Application Support/Erable/serve/…` sous macOS), pour qu'on puisse copier ou envoyer le projet sans emporter les mots de passe.
- Sans `--lan` (ou `--address`), le serveur n'écoute que sur **cette machine** (`127.0.0.1`). `--lan` écoute sur toutes les interfaces : les collègues ouvrent `http://<adresse de votre machine>:5080`.
- À la première exécution, le serveur demande le **premier administrateur** (identifiant et mot de passe). Il devient administrateur du serveur et du projet.
- Le projet doit être ouvert **par un seul processus** : fermez-le dans l'application de bureau avant (F16.R2).
- Un projet d'une version de format plus ancienne doit d'abord être migré : ouvrez-le une fois dans l'application de bureau.

> **HTTP en clair sur un réseau local** : les mots de passe circulent sans chiffrement. C'est acceptable sur un réseau de confiance (domicile, bureau), pas sur un Wi-Fi public ni sur Internet. Pour Internet, voir [§3](#3-docker-compose-et-https-caddy).

`erable serve <dossier>` accepte aussi un **dossier de données** (sans `erable.json`) : le serveur y crée `server.db` (comptes), `projects/<identifiant>/` (un dossier de projet par projet) et `profiles/`. C'est ce que fait l'image Docker avec `/data`.

---

## 2. Image Docker

### Construire et lancer

```bash
docker build -t erable .
docker run -d --name erable -p 8080:8080 -v erable-data:/data \
  -e ERABLE_ADMIN_LOGIN=admin -e ERABLE_ADMIN_PASSWORD='un mot de passe solide' \
  erable
```

- Image `mcr.microsoft.com/dotnet/aspnet` **chiseled** (sans shell ni gestionnaire de paquets), utilisateur **non root** (UID 1654), build **Release** (aucun point d'appui de test).
- Le volume **`/data`** contient tout ce qui compte : `server.db`, `projects/`, `profiles/`. Sans volume, tout disparaît avec le conteneur.
- `ERABLE_ADMIN_LOGIN` / `ERABLE_ADMIN_PASSWORD` ne servent qu'à la **création du premier administrateur**, quand le volume ne contient aucun compte. Ils sont ignorés ensuite : retirez-les du fichier de déploiement une fois le serveur démarré si vous préférez.
- La santé du conteneur est vérifiée par `erable healthcheck` (requête locale vers `/api/info`) : `docker ps` affiche `healthy`.
- Le conteneur écoute en HTTP simple sur le port 8080 : **mettez un reverse proxy TLS devant** s'il est joignable autrement que depuis votre machine.

### Configuration (variables d'environnement)

La ligne de commande l'emporte sur l'environnement, qui l'emporte sur les valeurs par défaut. Une valeur invalide **arrête** le serveur en nommant le réglage fautif.

| Variable | Défaut | Rôle |
|---|---|---|
| `ERABLE_PORT` | `5080` (`8080` dans l'image) | Port TCP. |
| `ERABLE_ADDRESS` | `127.0.0.1` (`0.0.0.0` dans l'image) | Adresse d'écoute. |
| `ERABLE_ADMIN_LOGIN`, `ERABLE_ADMIN_PASSWORD` | — | Premier administrateur (serveur vide). Identifiant : 2 à 32 caractères `a-z 0-9 . _ -` ; mot de passe : 8 à 256 caractères. |
| `ERABLE_PUBLIC_URL` | — | L'adresse que tapent les utilisateurs (`https://wiki.exemple.org`). Autorisée par CORS ; en `https`, le cookie de session devient `Secure`. |
| `ERABLE_TRUST_PROXY` | `false` | Faire confiance à `X-Forwarded-For/-Proto/-Host` d'un reverse proxy (adresse réelle du client pour la limitation des essais de connexion, protocole pour le cookie). **Seulement** si le serveur n'est joignable que par ce proxy. |
| `ERABLE_COOKIE_SECURE` | selon `ERABLE_PUBLIC_URL` | Force le drapeau `Secure` du cookie. |
| `ERABLE_CORS_ORIGINS` | — | Origines supplémentaires autorisées à appeler l'API depuis un navigateur (une SPA hébergée ailleurs), séparées par des virgules. Les pages sur `localhost` / `127.0.0.1` sont toujours autorisées : c'est l'application de bureau connectée (M4-07), qui s'authentifie par jeton, jamais par cookie. |
| `ERABLE_MAX_ATTACHMENT_MB` | `256` | Taille maximale d'une pièce jointe. |
| `ERABLE_PROJECT_QUOTA_MB` | aucun | Place totale des pièces jointes d'un projet (corbeille comprise). `0` : aucun quota. |
| `ERABLE_SESSION_HOURS` | `12` | Durée d'une session sans activité (renouvelée à mi-vie). |

### Sauvegarde

Arrêtez le conteneur (ou faites une copie cohérente du volume) puis copiez `/data` : c'est tout. Chaque projet est un dossier autonome (`projects/<identifiant>/` avec `erable.db`, `blobs/`) : `erable doctor <dossier>` en vérifie l'intégrité, volume monté ailleurs et conteneur arrêté.
Pour une sauvegarde **continue** (quelques secondes de retard au plus), voir [§6](#6-sauvegarde-continue-avec-litestream).

### Mise à jour

```bash
git pull && docker build -t erable . && docker rm -f erable && docker run … erable
```

Les projets sont migrés vers le nouveau format **à l'ouverture, avec sauvegarde préalable** (`erable.db.bak-<date>`).

---

## 3. Docker Compose et HTTPS (Caddy)

`deploy/docker-compose.yml` lance Erable derrière **Caddy**, qui obtient et renouvelle seul les certificats TLS.

```bash
cp deploy/.env.example deploy/.env      # renseigner le domaine et le premier administrateur
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```

Prérequis : un nom de domaine qui pointe vers la machine, les ports **80 et 443** ouverts. Le conteneur Erable n'est pas publié : seul Caddy l'atteint, sur le réseau de Compose. `ERABLE_TRUST_PROXY=true` y est donc sûr.
Les WebSocket (SignalR : événements et présence) traversent `reverse_proxy` sans réglage.

---

## 4. Ce que le serveur protège, et ce qu'il ne protège pas

- **Mots de passe** : hachés (PBKDF2, sel par mot de passe), jamais journalisés ni renvoyés. **Essais de connexion limités** : 5 échecs pour un identifiant depuis une même adresse, ou 40 échecs depuis une adresse, en 15 minutes, puis `429` (le blocage est journalisé). Un identifiant visé depuis de nombreuses adresses n'est jamais bloqué, pour qu'un tiers ne puisse pas empêcher son titulaire de se connecter : au-delà de 5 échecs, chaque essai attend seulement un délai croissant (1 s, doublé à chaque échec, au plus 10 s).
- **Changement de mot de passe** en libre-service (*Préférences*), avec le mot de passe actuel : les autres appareils sont déconnectés, celui qui fait le changement reste connecté.
- **Sessions** : cookie `HttpOnly`, `SameSite=Strict`, révocables (déconnexion, changement de mot de passe, compte désactivé) ; jeton `Bearer` pour l'application de bureau connectée. Aucune session n'est lisible par un script de la page.
- **Droits** par projet : lecteur, éditeur, administrateur. Un non-membre ne voit ni le nom ni l'existence d'un projet.
- **Pièces jointes** : taille maximale, quota par projet, jamais interprétées par le navigateur (`Content-Disposition`, `nosniff`, SVG et HTML en téléchargement).
- **Navigateur** : politique de sécurité du contenu (CSP) sur l'application, pas d'intégration dans le cadre d'un autre site (*clickjacking*), aucun `Referer` envoyé aux sites liés. Le `Caddyfile` fourni ajoute HSTS ; derrière un autre reverse proxy, ajoutez `Strict-Transport-Security: max-age=31536000`.
- **Hors du périmètre** : la protection du réseau (pare-feu), les sauvegardes, la mise à jour du système et de l'image de base (le CI analyse l'image à chaque changement et chaque semaine : `.github/workflows/docker.yml`).

## 5. Dépannage

| Symptôme | Cause probable |
|---|---|
| « This server has no account yet » au démarrage | Premier lancement sans terminal : renseigner `ERABLE_ADMIN_LOGIN` et `ERABLE_ADMIN_PASSWORD`. |
| `project.locked` / projet non ouvert | Le dossier est déjà ouvert par l'application de bureau ou par un autre serveur. |
| Impossible de se connecter derrière un proxy, ou « Trop de tentatives » pour tout le monde | `ERABLE_TRUST_PROXY` manquant : toutes les requêtes semblent venir de l'adresse du proxy. |
| Le cookie n'est pas enregistré en HTTPS | `ERABLE_PUBLIC_URL` en `http://`, ou proxy qui ne transmet pas `X-Forwarded-Proto`. |
| Un utilisateur a oublié son mot de passe | Un administrateur du serveur lui en donne un nouveau (`PATCH /api/v1/accounts/{login}`) ; l'utilisateur le remplace ensuite lui-même dans *Préférences › Mot de passe*. Si le **seul** administrateur a perdu le sien, il n'existe pas encore de procédure : les variables `ERABLE_ADMIN_*` ne servent que sur un serveur sans compte. |
| Les pièces jointes sont refusées (413) | `ERABLE_MAX_ATTACHMENT_MB` (taille d'un fichier) ou `ERABLE_PROJECT_QUOTA_MB` (place du projet). |

---

## 6. Sauvegarde continue avec Litestream

Une copie du volume `/data` est la sauvegarde la plus simple (§2), mais elle exige d'arrêter le serveur et ne protège que jusqu'à la dernière copie. **Litestream** réplique en continu les bases SQLite vers un stockage S3-compatible (AWS S3, Scaleway, Backblaze B2, MinIO…) : la perte maximale est de quelques secondes d'écriture, pour quelques centimes par mois.

### Ce qui est sauvegardé, et comment

| Quoi | Où | Comment |
|---|---|---|
| Comptes et droits | `server.db` | Litestream |
| Contenu de chaque projet | `projects/<id>/erable.db` | Litestream (les nouveaux projets sont détectés seuls) |
| Manifeste du projet | `projects/<id>/erable.json` | copie périodique (service `files-backup`, rclone) |
| Pièces jointes | `projects/<id>/blobs/` | copie périodique : les fichiers sont nommés par leur SHA-256, donc jamais modifiés ; la copie n'efface rien dans le bucket |

Litestream ne sait répliquer que SQLite : sans le service `files-backup`, une restauration retrouverait les pages mais pas leurs pièces jointes ni le manifeste.

### Mise en place

1. Créez un **bucket** dédié et une clé d'accès limitée à ce bucket (lecture, écriture, liste).
2. Dans `deploy/.env`, renseignez les variables `LITESTREAM_*` (voir `deploy/.env.example`).
3. Démarrez avec le fichier supplémentaire :

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml -f deploy/docker-compose.litestream.yml up -d
```

Litestream partage le volume `erable-data` avec le serveur (les bases sont en mode WAL, ce que Litestream exige) et ne demande rien de plus au serveur. `docker compose logs litestream` doit montrer une réplication par base. Les versions des images (`litestream/litestream`, `rclone/rclone`) sont figées dans le fichier : mettez-les à jour volontairement.

### Restauration

Le serveur doit être **arrêté** et le volume vide (ou débarrassé des bases abîmées).

```bash
docker compose -f deploy/docker-compose.yml down
docker volume rm <projet>_erable-data && docker volume create <projet>_erable-data
export LITESTREAM_BUCKET=… LITESTREAM_ENDPOINT=… LITESTREAM_ACCESS_KEY_ID=… LITESTREAM_SECRET_ACCESS_KEY=…
deploy/litestream/restore.sh <projet>_erable-data <identifiant-projet-1> <identifiant-projet-2>
```

Les identifiants de projets sont les dossiers sous `projects/` du bucket (console S3 ou `rclone lsd`). Le script restaure `server.db`, chaque `erable.db`, puis copie manifestes et pièces jointes, et rend les fichiers à l'utilisateur de l'image (UID 1654). Redémarrez ensuite le serveur et vérifiez chaque projet :

```bash
docker compose -f deploy/docker-compose.yml run --rm --no-deps --entrypoint dotnet erable erable.dll doctor /data/projects/<identifiant>
```

### Tester la chaîne complète

`deploy/litestream/test-restore.sh` joue le scénario de bout en bout avec un MinIO local : création d'un projet et d'une page, réplication, destruction du volume, restauration, `erable doctor`, puis lecture de la page. À lancer une fois après tout changement du fichier Litestream ou des versions d'images. **Il n'a pas encore été exécuté** : voir `tasks/M5-05-i18n-backup-docs.md`.

### Limites

- Litestream protège contre la perte du disque ou du serveur, pas contre une erreur humaine : une page supprimée par erreur se restaure par la corbeille et l'historique (F12, F13), pas par la sauvegarde.
- Le retard de réplication des pièces jointes est celui de `FILES_BACKUP_INTERVAL_SECONDS` (5 minutes par défaut).
- Une restauration remet le serveur dans l'état de la dernière réplication : les écritures plus récentes sont perdues.
