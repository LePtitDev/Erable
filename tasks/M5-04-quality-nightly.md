# M5-04 — Nightly, mutation testing, fuzzing

- **Statut** : todo
- **Jalon** : M5
- **Dépend de** : M3-06
- **Références** : §T-12, §T-13 (Nightly), §T-16 (M5), P-10

## Objectif

Vérifier la qualité des tests eux-mêmes et attraper ce que la CI de PR ne voit pas.

## Périmètre

- Workflow `nightly.yml` : e2e complet (3 navigateurs), visuel, accessibilité, benchmarks, charge, crash tests, fumée desktop 3 OS ; ouverture automatique d'une issue en cas d'échec.
- Stryker.NET sur `Erable.Core` et StrykerJS sur les modules TS dupliqués (règles, parseur, ID) : hebdomadaire, objectif ≥ 70 %.
- Fuzzing des parseurs Markdown C# et TS (10 min/semaine) : aucun crash, aucune boucle infinie, temps linéaire ; cas trouvés ajoutés à `spec/fixtures/`.
- Quarantaine des tests instables (mécanisme + règle : corrigé ou supprimé sous 24 h).
- Renovate configuré (mises à jour regroupées chaque semaine).

## Critères d'acceptation

- [ ] Scores de mutation actuels rapportés ; survivants significatifs listés avec proposition de tests.
- [ ] Durée nightly < 45 min.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
