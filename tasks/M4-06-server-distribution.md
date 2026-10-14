# M4-06 — `erable serve` et image Docker

- **Statut** : todo
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

- [ ] L'image démarre, sert la SPA, passe le healthcheck ; e2e `@server` exécutables contre elle.
- [ ] Documentation d'installation dans `docs/`.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
