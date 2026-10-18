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
- **Constats du fuzzing (première exécution, locale)** :
  1. **Corrigé — dépassement de pile dans `ErableMarkdown.Analyze`** : `[[` répété 5 000 fois (10 Ko) tuait le processus (stack overflow, non rattrapable) : l'extraction du texte des inlines (`AppendInlines`) était récursive. Un éditeur pouvait donc arrêter tout le serveur en enregistrant une page. Rendue itérative ; test de non-régression `Analyze_ThousandsOfNestedInlines_DoesNotOverflowTheStack`.
  2. **Corrigé, comportement à confirmer — `ArgumentException` de Markdig** (« too deeply nested ») au-delà de sa limite d'imbrication (`>` ou `[` répétés des milliers de fois) : l'enregistrement échouait. Maintenant : la page est conservée et indexée comme texte brut, sans référence ni titre (`Analyze` renvoie une analyse dégradée). **Point à challenger** : alternative = refuser l'enregistrement avec une erreur 422 explicite ; je n'ai pas tranché de façon irréversible, la voie retenue ne perd aucune donnée.
  3. **Non corrigé — positions des références (R10) fausses dans un élément de liste contenant une tabulation** : `* ##<TAB>#    [[ID]]` donne `8..28` au lieu de `10..30` (Markdig, `UsePreciseSourceLocation`). Sans effet sur l'index (seuls les ID comptent) mais contraire à R10 ; la tabulation est exclue des jetons du fuzzer C# en attendant. Cas ajouté dans `spec/fixtures/markdown/edge-cases.json` (audit T7) avec `"knownFailure": "csharp"` : Vitest le vérifie normalement ; xUnit vérifie que l'écart existe **toujours** et échouera quand Markdig sera corrigé, pour qu'on retire le marqueur.
  4. **Non corrigé — temps quadratique** : C# (Markdig) sur `[a](`, `> `, `[`, `1. ` répétés (64 Ko de `[a](` ≈ 1 s, ×4 par doublement) ; TS (remark/micromark, identique sans l'extension Erable) sur `]`, `> `, `1. `, `[a](` (40 Ko de `]` ≈ 41 s). Marqués `KnownSuperLinear` (test C# explicite) et `it.fails` (TS) : ils deviennent rouges quand l'un est corrigé.
  5. **Non corrigé — `RangeError: Maximum call stack size exceeded` côté TS** sur citations imbriquées (`> ` × 8 000) et listes (`1. ` × 4 000), également dans remark seul : la prévisualisation d'une telle page échoue dans le navigateur. `it.fails` aussi.
  **Traitement des points 4 et 5 (décision de l'utilisateur : recommandations retenues)** : limites de sécurité du Markdown, `docs/markdown-syntax.md` §6 et décision Q10. Pré-balayage linéaire `MarkdownLimits` (Core) et `limits.ts` (client), jumeaux pilotés par `spec/fixtures/limits/limits.json` : page ≤ 1 Mo, ligne ≤ 10 000 caractères, ≤ 256 répétitions de `[ ] ( ) >`, ≤ 32 marqueurs de citation/liste par ligne (indentation ≤ 256), ≤ 2 000 lignes sans ligne vide. Refus en 422 (`page.content.*`) à la création et à l'enregistrement, y compris après fusion ; message avant envoi dans l'éditeur ; une page existante hors limites s'affiche en texte brut (sans sommaire). Les marqueurs `KnownSuperLinear` et `it.fails` sont remplacés par des tests qui vérifient le refus et, pour la plus grande répétition acceptée, un temps < 2 s. Le point 3 (positions avec tabulation) reste ouvert. Le repli de `Analyze` sur texte brut est conservé en défense en profondeur. Non couvert : un paragraphe sans ligne vide de 2 000 lignes reste lent côté Markdig (~0,2 s mesuré en M3-06 pour 1 000 lignes) ; les valeurs sont à confirmer sur de vraies pages.
- **Autres constats de l'exécution des tests de fin de jalon** : `E2E-15-02` (@pr) a échoué une fois sous forte charge CPU (rechargement avant que la préférence de thème soit enregistrée) et passe 6 fois sur 6 isolé : course latente préexistante du test, candidate à la quarantaine si elle revient.
- **Non fait** : scores de mutation et survivants (exigent l'exécution hebdomadaire), mesure de la durée nightly. Critères d'acceptation donc ouverts.
- Message de commit : `ci: add nightly and weekly quality workflows, mutation testing, fuzzing and Renovate (M5-04)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
