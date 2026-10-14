# M2-03 — Stockage et API : catégories, propriétés, valeurs

- **Statut** : todo
- **Jalon** : M2
- **Dépend de** : M2-02, M1-05
- **Références** : §A-7.4, §A-8.1, F07, F08.R3–R6, §T-6, P-07

## Objectif

Persister la structure et exposer une API qui renvoie des valeurs déjà résolues et des violations déjà calculées.

## Périmètre

- Migration de schéma v2 : `categories`, `page_categories`, `property_defs`, `property_values` (colonnes typées), `validation_issues` ; nouveau projet figé `legacy/v2` ; `doctor` étendu.
- Trancher P-07 : table dérivée de valeurs résolues (recommandé) maintenue à l'écriture.
- Recalcul des violations et des valeurs résolues : à l'enregistrement d'une page, au changement d'une règle ou d'un défaut (toutes les pages concernées, F08.R4), en une transaction.
- Endpoints CRUD typés : `categories`, `properties`, valeurs d'une page (`pages/{id}/properties`), catégories d'une page.
- Opérations spéciales : renommer / supprimer une option d'`enum` avec remplacement (F08.R5) ; changement de type avec liste des pages bloquantes (F08.R6) ; renommer la clé (E2E-08-11).
- Réponse « page » enrichie : valeurs résolues avec origine (`via PNJ`), conflits, violations.
- Mettre à jour « Chroniques d'Aube » (M1-07) avec catégories, propriétés et valeurs de features §2.2.

## Critères d'acceptation

- [ ] Tests d'intégration de chaque endpoint (nominal + erreurs).
- [ ] Changer `≤ 60` en `≤ 80` sur Niveau met à jour les violations de toutes les pages dans la même transaction.
- [ ] Migration `legacy/v1` → v2 testée.
- [ ] Enregistrement d'une page toujours < 100 ms avec recalcul.

## Points d'attention

- Un changement de défaut sur une catégorie haute (Entité) peut toucher des milliers de pages : mesurer et, si besoin, proposer un recalcul en tâche de fond avec état « en cours ».

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
