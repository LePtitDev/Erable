# M5-03 — Velopack, signature, pipeline release

- **Statut** : todo
- **Jalon** : M5
- **Dépend de** : M0-11
- **Références** : §A-10 (Packaging), §A-11.4 (coûts), §T-10, §T-13 (Release), P-06

## Objectif

Une version 1.0 installable et qui se met à jour seule sur les trois OS.

## Périmètre

- Velopack : installeurs Windows, macOS, AppImage Linux ; canal de mise à jour ; vérification de mise à jour au démarrage (désactivable).
- Workflow `release.yml` sur tag `v*` : build, packaging, fumée desktop sur les **installeurs**, migration de tous les projets `legacy/`, image Docker + scan, publication.
- Signature : préparer l'intégration (Azure Trusted Signing, notarisation Apple) derrière des secrets ; **ne rien acheter ni créer de compte** — lister à l'utilisateur ce qu'il doit fournir (§A-11.4).
- Notes de version générées.

## Critères d'acceptation

- [ ] Une release de test (tag créé par l'utilisateur) produit des installeurs fonctionnels sur les 3 OS.
- [ ] Mise à jour d'une version N vers N+1 vérifiée.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
