# M5-04 — Nightly, mutation testing, fuzzing

- **Statut** : in-progress
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

- **Nightly** (`nightly.yml`) : e2e complet Chromium + WebKit + Firefox (`ERABLE_E2E_NIGHTLY=1` ajoute le projet `firefox` et le projet `quarantine`, absents de la CI de PR), accessibilité (E2E-17-04), visuel dans le conteneur fixe de M5-02, budgets serveur + BenchmarkDotNet, fumée desktop 3 OS ; les jobs tournent en parallèle (cible < 45 min, **non mesurée**). Crash et charge restent dans `nightly-robustness.yml`. `nightly-issue.yml` ouvre (ou commente) une issue `ci-failure` quand un workflow planifié (Nightly, Nightly robustness, Weekly quality) échoue.
- **Mutation** (`weekly-quality.yml`) : Stryker.NET 5.0.0 (outil local) sur `Erable.Core`, StrykerJS 10.0.0 sur ids, Markdown et règles TS ; `break: 0` tant que le premier score n'est pas connu (objectif 70 % affiché dans le résumé par `build/mutation-summary.mjs`). À durcir à `break: 70` après la première exécution.
- **Fuzzing** : `MarkdownFuzzTests` (C#) et `markdown.fuzz.test.ts` (TS) : entrées aléatoires construites sur les jetons de la grammaire (aucune exception, offsets cohérents) et répétitions pathologiques (temps de 8× la taille ≤ 4 × 8 × le temps de base, plancher 250 ms). Version courte (1 s, graine fixe) dans la suite normale ; 10 min hebdomadaires avec graine neuve. Les cas en échec sont écrits dans `artifacts/fuzz/`.
- **Quarantaine** : tag `@quarantine` + `spec/quarantine.json` + `pnpm quarantine:check` (échec au-delà de 24 h ou si registre et tags divergent) ; projet Playwright `quarantine` non bloquant.
- **Renovate** : `renovate.json` (une PR groupée par écosystème, le lundi avant 6 h ; image Playwright du visuel groupée avec le paquet ; majeures à part). L'application Renovate doit être installée sur le dépôt par l'utilisateur.
- **Risque connu à surveiller** : M3-06 a mesuré un coût plus que linéaire d'`Analyze` sur un très long paragraphe (Markdig, `UsePreciseSourceLocation`). Le test de linéarité C# peut donc échouer à la première exécution : ce serait un vrai constat à traiter (proposition de M3-06), pas un défaut du test. Stryker.NET avec Microsoft.Testing.Platform (xUnit v3) est à valider à la première exécution.
- **Non fait** : scores de mutation et survivants (exigent l'exécution hebdomadaire), mesure de la durée nightly. Critères d'acceptation donc ouverts.
- Message de commit : `ci: add nightly and weekly quality workflows, mutation testing, fuzzing and Renovate (M5-04)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
