# M3-06 — Générateur de gros projets et budgets de performance

- **Statut** : todo
- **Jalon** : M3
- **Dépend de** : M3-01
- **Références** : §T-4.3, §T-11, §T-16 (M3)

## Objectif

Mesurer la performance pour qu'elle ne se dégrade pas sans qu'on le voie.

## Périmètre

- Générateur à graine fixe : `--pages 10000 --categories 300 --refs 50000 --attachments-size 2GB --seed 42` (option pour des pièces jointes factices plus légères en CI).
- BenchmarkDotNet sur les chemins chauds : parseur, résolution des propriétés, enregistrement, recherche. Résultats historisés (fichier JSON) ; régression > 15 % signalée.
- Marques `performance.mark` dans la SPA et tests Playwright de budget (§T-11.1).
- Contrôle de la taille du bundle (< 500 Ko gzip) en échec bloquant.
- Rapport des budgets actuels dans les notes de la tâche ; tout budget dépassé est signalé avec une proposition.

## Critères d'acceptation

- [ ] Tous les budgets de §T-11.1 mesurés ; écarts documentés.
- [ ] Commandes ajoutées à `CLAUDE.md` §9.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
