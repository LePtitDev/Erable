# CLAUDE.md — Erable

Erable est un wiki structuré « façon IDE » : pages Markdown étendu, catégories avec héritage, propriétés typées et validées, liens typés, pièces jointes. Il fonctionne en **application desktop locale** (Photino + backend in-process) et en **mode serveur** multi-utilisateur (ASP.NET Core + SPA React).

Ce fichier s'adresse aux agents IA qui implémentent le projet. Il est **prioritaire** sur les habitudes par défaut de l'agent.

---

## 1. Sources de vérité

| Document | Rôle | Abréviation dans les tâches |
|---|---|---|
| `docs/architecture.md` | Stack, modèle de données, stockage, API, sécurité, jalons | §A-x |
| `docs/features.md` | Fonctionnalités `Fnn`, règles `Fnn.Rn`, scénarios `E2E-nn-nn` (= critères d'acceptation) | Fnn / E2E-nn-nn |
| `docs/testing.md` | Stratégie de test, outillage, définition de « terminé » | §T-x |
| `docs/deployment.md` | Installer un serveur : `erable serve`, Docker, Compose + Caddy, variables `ERABLE_*`, sauvegarde continue Litestream | — |
| `docs/release.md` | Publier une version : tag `v*`, installeurs Velopack, mise à jour, signature | — |
| `docs/user-guide.md` | Guide de l'utilisateur : démarrage, `[[ID]]`, raccourcis, dossiers et sous-pages | — |
| `docs/i18n-review.md` | Traductions : vérifications automatiques et liste à relire | — |
| `mockups/Erable — Wiki local.html` | Référence visuelle des 6 écrans | Mockup |
| `tasks/` | Plan d'implémentation, une tâche par fichier | `M<j>-<nn>` |

Les documents de `docs/` sont **validés**. On ne s'en écarte pas en silence : tout écart passe par une proposition à l'utilisateur (§5), puis par une mise à jour du document concerné une fois la décision prise.

### Lire le mockup

Le fichier HTML est un bundle compressé (≈ 3 Mo) : ne pas le lire avec `Read`. Pour l'inspecter :
- l'ouvrir dans un navigateur (il contient 6 écrans : Accueil, Page — lecture, Page — édition (sombre), Catégorie, Propriété, Recherche) ;
- ou le décoder dans le **scratchpad** (jamais dans le dépôt) : le `<script type="__bundler/manifest">` est un JSON `{uuid: {mime, compressed, data}}` en base64 (gzip si `compressed`), et chaque écran est lui-même un bundle du même format.

Polices du mockup : **IBM Plex Sans** (UI), **IBM Plex Mono** (ID, code), **Source Serif 4** (corps des pages). Les couleurs y sont en dur (styles inline) : la tâche `M0-05` les convertit en tokens.

---

## 2. Langues

| Quoi | Langue |
|---|---|
| Code : identifiants, noms de fichiers, commentaires, messages de log et d'exception, noms et titres de tests | **Anglais** |
| Clés i18n | Anglais, hiérarchiques (`editor.palette.insertLink`) |
| Textes d'interface | Via i18n uniquement ; `fr` est la locale de référence, puis `en`, `de`, `es` |
| Documentation (`docs/`), tâches (`tasks/`), échanges avec l'utilisateur | Français |
| Données du projet de référence « Chroniques d'Aube » | Français (c'est du contenu) |

Les titres des tests e2e commencent par l'ID du scénario, suivi d'un titre **en anglais** : `test("E2E-05-03 · Insert a link via the palette @pr", …)`. La traçabilité repose sur l'ID, pas sur le libellé.

---

## 3. Git : réservé à l'utilisateur

**L'agent ne fait aucune opération Git qui modifie l'état du dépôt.** Interdits : `commit`, `branch`, `checkout`/`switch`, `merge`, `rebase`, `reset`, `revert`, `stash`, `tag`, `push`, `pull`, `fetch`, `cherry-pick`, `add`, `rm`, `mv`, `restore`, `clean`, modification de `.git/` ou de la config Git, création de worktree.

Autorisé (lecture seule) : `git status`, `git diff`, `git log`, `git show`, `git blame`.

En fin de tâche, l'agent **propose** un message de commit (anglais, format Conventional Commits, ex. `feat(core): add ElementId parsing and formatting`) et la liste des fichiers concernés. C'est l'utilisateur qui commit.

Fichiers à ne jamais déplacer ou supprimer sans accord : `docs/`, `mockups/`, `LICENSE`, ce fichier.

---

## 4. Déroulé d'une tâche

1. **Lire** la tâche dans `tasks/`, puis les sections de `docs/` qu'elle cite. Vérifier que ses dépendances sont `done` ; sinon le signaler.
2. **Remettre en question** (§5) : si quelque chose cloche dans la tâche ou dans les documents, le dire **avant** de coder.
3. Passer le statut de la tâche à `in-progress` (champ `Statut` en tête de fichier).
4. **Implémenter** par petits incréments, tests compris (§T-14). Écrire d'abord le test d'un bug avant de le corriger.
5. **Vérifier** : build sans avertissement, tests verts, lint. Ne jamais déclarer « terminé » sans avoir exécuté les tests ; si un test échoue ou n'a pas pu être lancé, le dire explicitement.
6. **Mettre à jour la tâche** : cocher les critères atteints, remplir la section « Notes d'implémentation » (décisions prises, écarts, dettes), passer le statut à `review`.
7. **Rendre compte** : résumé, fichiers modifiés, commandes de vérification, message de commit proposé, propositions d'amélioration éventuelles.

L'utilisateur passe la tâche à `done` après relecture et commit.

Une tâche trop grosse pour une session se découpe : proposer le découpage à l'utilisateur, puis créer les sous-tâches (`M1-10a`, `M1-10b`…).

---

## 5. Esprit critique et propositions — attendu, pas optionnel

L'agent n'est pas un exécutant. Il est attendu qu'il **conteste** les choix techniques ou de design qui lui semblent inadaptés, et qu'il **propose** des améliorations.

**Quand le faire**
- un choix des documents ou d'une tâche est risqué, coûteux, obsolète, incohérent avec un autre document, ou contredit le mockup ;
- une bibliothèque prévue est abandonnée, a un équivalent nettement meilleur, ou pose un problème de licence ;
- une règle métier a un cas limite non traité ;
- une exigence de test coûte plus qu'elle ne protège à ce stade ;
- une meilleure UX, une simplification ou une économie de complexité est possible.

**Comment**
- Le signaler **avant** d'implémenter, de façon structurée et brève :
  > **Point à challenger** — *constat* · *impact concret* · *options (avec coût)* · *recommandation*
- Pour un point bloquant ou structurant : **s'arrêter et demander**. Pour un point mineur et local : choisir l'option la plus prudente, avancer, et le consigner dans « Notes d'implémentation ».
- Ne **jamais** dévier silencieusement d'un document validé. Une fois une décision prise, proposer la mise à jour du document (nouvelle décision `Q<n>` dans §A-13 si c'est structurant).
- Les idées hors du périmètre de la tâche vont dans la section « Propositions » de la tâche (ou dans `tasks/README.md` § Propositions) ; on ne les implémente pas sans accord.
- Être factuel : vérifier (doc officielle, version, test rapide) plutôt qu'affirmer de mémoire. Dire « je ne sais pas » quand c'est le cas.

---

## 6. Invariants d'architecture (non négociables sans décision explicite)

1. **`ElementId`** : `ulong` aléatoire non nul, un seul espace pour tous les types. **Toujours une chaîne hexa de 16 caractères hors de la base** (JSON, URL, TS). Type marqué côté TS, obtenu uniquement via `parseElementId()`. Jamais de `number` (§A-4).
2. **`Erable.Core` est pur** : aucune dépendance IO, base, HTTP ou horloge système (utiliser `TimeProvider`). Toute logique métier y vit.
3. **Le frontend ne parle que HTTP/WebSocket au backend**, jamais à l'OS. Les rares fonctions natives passent par le shell desktop derrière une interface.
4. **Les références passent par l'ID** : renommer ou déplacer ne casse jamais rien.
5. **HTML brut désactivé** dans le Markdown, rendu sanitisé ; SVG et HTML en pièce jointe jamais rendus inline (§A-9).
6. **La validation avertit, elle ne bloque pas** (§A-5.4).
7. **Une spécification, deux implémentations** : ID, grammaire Markdown, règles de validation sont pilotés par `spec/fixtures/` et exécutés par xUnit **et** Vitest (§T-5).
8. **Aucune couleur en dur** dans les composants (tokens CSS), **aucune chaîne d'UI en dur** (i18n).
9. **Client API généré** depuis l'OpenAPI ; jamais de types d'API écrits à la main.
10. **Migrations de schéma** : toute modification ajoute une migration, met à jour `spec/projects/legacy/` et `erable doctor`. On ne casse jamais un projet existant.
11. **Mode desktop** : écoute sur `127.0.0.1` uniquement, jeton de session exigé, en-tête `Host` vérifié.
12. Les points d'appui `/_test/*` n'existent qu'avec `ERABLE_TEST_MODE=1` et **jamais** dans un build Release.

---

## 7. Arborescence cible

```
Erable/
├─ Erable.sln
├─ Directory.Build.props · Directory.Packages.props · .editorconfig · global.json
├─ src/
│  ├─ Erable.Core/        domaine pur
│  ├─ Erable.Storage/     SQLite + Dapper, migrations, FTS5, BlobStore
│  ├─ Erable.Server/      ASP.NET Core Minimal API, SignalR, auth, OpenAPI, CLI (serve, doctor)
│  ├─ Erable.Desktop/     shell Photino
│  └─ web/                SPA React/TS (pnpm + Vite) ; e2e Playwright dans web/e2e/
├─ tests/                 Erable.Core.Tests, Erable.Storage.Tests, Erable.Server.Tests, …
├─ spec/
│  ├─ fixtures/           cas partagés C# ↔ TS (ids.json, rules.json, markdown/)
│  └─ projects/           chroniques/, legacy/vN/, users.json
├─ docs/ · mockups/ · tasks/
```

---

## 8. Conventions

### C# (.NET 10)
- `Nullable` activé, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, versions centralisées (`Directory.Packages.props`).
- Namespaces *file-scoped*, `sealed` par défaut, `record` pour les valeurs, `switch` exhaustifs.
- Minimal API groupées par ressource ; erreurs au format `ProblemDetails`.
- SQL explicite avec Dapper, paramètres toujours liés, pas d'EF Core.
- Tests : xUnit v3 + Shouldly ; CsCheck pour les propriétés ; nommage `Method_Context_ExpectedResult` ; un comportement par test ; vrai SQLite en dossier temporaire, pas de mock de ce qu'on possède (§T-1).

### TypeScript / React
- `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` ; ESLint `strict-type-checked` ; pas de `any` hors code généré.
- Composants fonctionnels, CSS Modules + tokens ; Radix UI pour les primitives accessibles.
- État serveur : TanStack Query ; état UI : Zustand ; routage : TanStack Router.
- Tests : Vitest + Testing Library (requêtes par rôle et libellé), MSW typé, fast-check ; extensions CodeMirror testées sur `EditorState`, sans DOM.
- e2e : Playwright, page objects, aucun `waitForTimeout`.

### Général
- Préférer la solution la plus simple qui respecte les invariants. Pas d'abstraction « pour plus tard » sans besoin identifié dans les documents.
- Pas de nouvelle dépendance sans la justifier (maintenance, licence, taille du bundle).
- Accessibilité dès le premier composant : rôles ARIA, focus visible, tout au clavier.

---

## 9. Commandes

> Tenu à jour par les tâches. Tant qu'une commande n'est pas listée ici, la découvrir dans le dépôt plutôt que l'inventer.

Raccourcis (`.bat` sous Windows, `.sh` ailleurs) dans `scripts/` :

```bash
scripts/build.sh [Debug|Release]            # dépendances web, SPA, solution .NET
scripts/run.sh [desktop|server [dossier] [port]|dev]
scripts/test.sh [--e2e] [--desktop]         # contrôles du pipeline PR ; --desktop : publication + fumée
scripts/serve.sh [dossier] [--port 5080] [--lan] [--rebuild] [-- options]   # héberger sans Docker : publie en Release (artifacts/server, refait si le commit change) puis erable serve ; dossier par défaut data/
scripts/compose.sh [--litestream] setup|up|update|down|status|logs|backup|restore|doctor   # héberger avec Docker Compose + Caddy (deploy/.env)
```

Détail des commandes :

```bash
# .NET (SDK 10, tests sur Microsoft.Testing.Platform : `--solution` obligatoire)
dotnet build Erable.sln
dotnet test --solution Erable.sln
dotnet test --project tests/Erable.Core.Tests          # un seul projet
dotnet test --solution Erable.sln -c Release           # vérifie aussi l'absence de /_test en Release
dotnet test --project tests/Erable.Storage.Tests -- --filter-method "*GenerateChroniques" --explicit only   # régénère spec/projects/chroniques
dotnet run --project src/Erable.Server -- serve <dossier> [--port 5080] [--lan]   # dossier de projet (partagé tel quel) ou de données ; ERABLE_ADMIN_LOGIN/ERABLE_ADMIN_PASSWORD pour le premier administrateur ; ERABLE_TEST_MODE=1 pour /_test ; réglages ERABLE_* : docs/deployment.md
dotnet run --project src/Erable.Server -- healthcheck [--port 8080]       # code 0 si le serveur local répond (santé du conteneur)
docker build -t erable .                                                  # image du serveur (docs/deployment.md)
dotnet run --project src/Erable.Server -- doctor <projet> [--json] [--fix]   # intégrité d'un projet fermé (0 sain, 1 anomalies, 2 illisible)
dotnet run --project src/Erable.Desktop                 # app desktop (après pnpm -C src/web build)

# SPA (pnpm épinglé par `packageManager` : utiliser Corepack, `corepack pnpm …` si les shims ne sont pas activés)
pnpm -C src/web install
pnpm -C src/web dev              # Vite sur :5173, proxy /api → http://localhost:5080
pnpm -C src/web build            # affiche la taille gzip du bundle initial
pnpm -C src/web test             # Vitest
pnpm -C src/web lint             # ESLint strict-type-checked, 0 avertissement
pnpm -C src/web typecheck
pnpm -C src/web format:check     # Prettier
pnpm -C src/web api:generate     # exporte l'OpenAPI (dotnet) puis régénère src/api/schema.ts
pnpm -C src/web api:check        # CI : échoue si openapi.json ou schema.ts sont périmés
pnpm -C src/web e2e              # Playwright Chromium + WebKit contre le vrai backend (build SPA + erable serve, ERABLE_TEST_MODE=1)
pnpm -C src/web e2e:pr           # seulement les tests @pr
pnpm -C src/web e2e:perf         # mesures de temps (@perf), seules, un worker (exclues des autres projets)
pnpm -C src/web e2e:trace        # traçabilité E2E-* ↔ tests ; `-- --enforce M1` échoue s'il manque un scénario ≤ M1
pnpm -C src/web exec playwright install chromium webkit   # une fois par machine

# Performance (M3-06, docs/testing.md §11) : toujours en Release
dotnet run -c Release --project tests/Erable.Benchmarks -- generate artifacts/perf/large [--light] [--pages 10000 --categories 300 --refs 50000 --attachments-size 2GB --seed 42]   # --light : 16 Mo de pièces jointes (CI)
dotnet run -c Release --project tests/Erable.Benchmarks -- budgets [--folder artifacts/perf/large] [--light] [--json fichier]   # budgets serveur (p95) en HTTP ; code 1 si dépassé
dotnet run -c Release --project tests/Erable.Benchmarks -- bench [--pages 10000] [--update-baseline]   # BenchmarkDotNet ; écrit artifacts/perf/, compare à spec/perf/baseline.json (code 1 au-delà de +15 %)
ERABLE_LARGE_PROJECT=<dossier généré avec --light> pnpm -C src/web e2e:perf   # budgets navigateur (arbre, page, palette, frappe) ; sans la variable, seuls les tests @perf du projet de référence tournent
pnpm -C src/web bundle-size                # échoue au-delà de 500 Ko gzip

# Robustesse (M4-09, docs/testing.md §6.1 et §11.2)
ERABLE_CRASH_ITERATIONS=100 dotnet test --project tests/Erable.Server.Tests -- --filter-class "*CrashTests"   # tue le vrai serveur pendant des écritures (8 itérations par défaut ; ERABLE_CRASH_SEED rejoue un tirage)
dotnet run -c Release --project tests/Erable.Benchmarks -- load [--users 50] [--minutes 10 | --seconds N] [--light] [--json fichier] [--reports dossier]   # charge NBomber (80 % lecture / 20 % écriture) ; code 1 si erreur, p95 au-delà du budget ou mémoire qui croît

# Desktop
node build/publish-desktop.mjs win-x64     # ou linux-x64, osx-arm64, osx-x64 → artifacts/desktop/
pnpm -C src/web e2e:desktop                # Windows : E2E-16-01/02 sur le binaire publié (CDP)
artifacts/desktop/<rid>/Erable/Erable.Desktop [dossier] --self-test [--self-test-report rapport.json]   # code de sortie 0/1
# Desktop : ERABLE_STARTUP_BUDGET_MS=<ms> remplace le budget de démarrage de 3 s (5000 en CI) ; ERABLE_E2E_WEBVIEW_LANG change la langue forcée des e2e (fr-FR)
# Desktop : ERABLE_PROFILE_DIR=<dossier> remplace le profil applicatif (tests) ; avec ERABLE_TEST_MODE=1,
# ERABLE_TEST_PICK_FOLDER=<dossier> remplace le sélecteur de dossier natif (E2E-16-03)
```

Environnement de développement principal : Windows (PowerShell). Le code doit rester portable (chemins via `Path.Combine`, pas de séparateur en dur, fins de ligne gérées par `.gitattributes`).

---

## 10. Définition de « terminé » (rappel de §T-14)

- logique métier dans Core, testée (propriétés si invariants) ;
- cas partagés ajoutés dans `spec/fixtures/` si syntaxe ou règle partagée ;
- endpoints testés en intégration et présents dans la matrice d'autorisation (à partir de M4) ;
- schéma modifié → migration + `legacy/` + `doctor` ;
- scénarios `E2E-*` de la tâche implémentés et verts ;
- textes traduits dans les 4 langues ;
- utilisable au clavier, sans violation axe-core grave ;
- aucun budget de performance dégradé (à partir de M3).
