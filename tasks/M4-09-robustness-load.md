# M4-09 — Crash tests et charge

- **Statut** : todo
- **Jalon** : M4
- **Dépend de** : M4-05
- **Références** : §T-6.1, §T-11.2 (NBomber), §T-15

## Objectif

Prouver qu'Erable survit à une coupure brutale et à une équipe réelle.

## Périmètre

- Crash test : processus serveur tué (`Process.Kill`) à des instants aléatoires pendant une rafale d'écritures (pages, pièces jointes), puis réouverture : `integrity_check` OK, chaque enregistrement entièrement présent ou absent, `doctor` sans erreur.
- NBomber : 50 utilisateurs (80 % lecture, 20 % écriture) pendant 10 minutes : aucune erreur, p95 dans les budgets, mémoire stable.
- Exécution nightly.

## Critères d'acceptation

- [ ] Crash test sur 100 itérations sans corruption.
- [ ] Rapport de charge dans les notes de la tâche.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
