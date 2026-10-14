# M1-16 — Commande `erable doctor`

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-06
- **Références** : §T-6, §T-15

## Objectif

Un diagnostic d'intégrité qu'on lance sur n'importe quel projet, et qui sert de garde-fou à la fin de chaque test e2e.

## Périmètre

- `erable doctor <dossier> [--json] [--fix]` dans l'exécutable serveur (et exposé en interne pour les tests).
- Vérifications : `PRAGMA integrity_check`, `foreign_key_check`, blobs manquants / orphelins / empreinte incorrecte, références cassées, cycles dans l'arbre, éléments hors de leur racine, index `refs` désynchronisé du Markdown, `formatVersion`. FTS et propriétés résolues ajoutés en M2/M3.
- `--fix` limité aux réparations sûres (reconstruire un index dérivé, supprimer un blob orphelin) ; jamais de suppression de contenu utilisateur.
- Projets volontairement corrompus dans `spec/projects/corrupted/` pour les tests.
- Hook Playwright : `doctor` exécuté après chaque fichier e2e.

## Critères d'acceptation

- [ ] Chaque type d'anomalie a un projet corrompu et un test.
- [ ] Code de sortie non nul en cas d'anomalie ; sortie JSON stable.
- [ ] « Chroniques d'Aube » passe sans anomalie.

## Points d'attention

- Les références cassées sont un état **légitime** (élément supprimé) : les signaler en information, pas en erreur.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
