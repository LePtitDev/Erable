# Plan d'implémentation — Erable

Ce dossier découpe les jalons de `docs/architecture.md` §12 en tâches exécutables par un agent ou un développeur. Lire d'abord `CLAUDE.md` à la racine.

## Conventions

- **Un fichier par tâche** : `M<jalon>-<nn>-<slug>.md`. Modèle : [`_template.md`](_template.md).
- **Statuts** : `todo` → `in-progress` → `review` (l'agent a terminé, en attente de relecture et de commit par l'utilisateur) → `done`. Aussi : `blocked` (avec la raison).
- **Dépendances** : une tâche ne démarre que si ses dépendances sont `done` ou `review`. Les tâches sans dépendance commune peuvent avancer en parallèle.
- **Critères d'acceptation** : ils reprennent les règles `Fnn.Rn` et les scénarios `E2E-nn-nn` de `docs/features.md`. Une tâche est terminée quand ses scénarios passent et que la définition de « terminé » (`CLAUDE.md` §10) est respectée.
- **Découpage** : si une tâche dépasse une session de travail raisonnable, la découper en `M1-10a`, `M1-10b`… après accord de l'utilisateur.
- **Points relevés** : un problème transverse découvert pendant l'implémentation est ajouté à la section « Points à trancher » ci-dessous avec un nouvel ID `P-nn`.

## Index

### M0 — Squelette
| ID | Tâche | Dépend de | Statut |
|---|---|---|---|
| [M0-01](M0-01-dotnet-solution.md) | Solution .NET, projets, analyseurs | — | review |
| [M0-02](M0-02-web-scaffolding.md) | SPA React/TS : outillage | — | review |
| [M0-03](M0-03-server-host.md) | Hôte serveur, `/api/info`, service de la SPA | M0-01, M0-02 | review |
| [M0-04](M0-04-openapi-client.md) | OpenAPI, client TS généré, contrôles de dérive | M0-03 | review |
| [M0-05](M0-05-design-tokens-theme.md) | Tokens de design et thèmes | M0-02 | review |
| [M0-06](M0-06-i18n.md) | Internationalisation | M0-02 | review |
| [M0-07](M0-07-desktop-shell.md) | Shell Photino et sécurité loopback | M0-03 | review |
| [M0-08](M0-08-webview-spike.md) | Spike WebKitGTK / WKWebView | M0-07 | review |
| [M0-09](M0-09-test-infrastructure.md) | Infrastructure de test | M0-01, M0-02, M0-03 | review |
| [M0-10](M0-10-ci-pipeline.md) | Pipeline CI PR (3 OS) | M0-09 | review |
| [M0-11](M0-11-desktop-smoke.md) | Artefacts desktop et test de fumée | M0-07, M0-10 | review |

### M1 — Cœur local
| ID | Tâche | Dépend de | Statut |
|---|---|---|---|
| [M1-01](M1-01-element-id.md) | `ElementId` (C# + TS + fixtures) | M0-09 | review |
| [M1-02](M1-02-tree-domain.md) | Domaine : éléments et arbre | M1-01 | review |
| [M1-03](M1-03-project-storage.md) | Format de projet, SQLite, migrations | M1-02 | review |
| [M1-04](M1-04-markdown-references.md) | Grammaire `[[ID]]` : parseurs C# et TS | M1-01 | review |
| [M1-05](M1-05-pages-api.md) | API arbre, éléments, pages, refs, révisions | M1-03, M1-04, M0-04 | review |
| [M1-06](M1-06-attachments-backend.md) | Blobs et pièces jointes (backend) | M1-05 | review |
| [M1-07](M1-07-reference-project.md) | Projet « Chroniques d'Aube », builders, `/_test` | M1-05, M1-06 | review |
| [M1-08](M1-08-home-screen.md) | Écran d'accueil et projets | M1-05, M0-05, M0-06 | review |
| [M1-09](M1-09-app-shell.md) | Coque de l'application et routage | M1-08 | review |
| [M1-10](M1-10-explorer-tree.md) | Explorateur (arbre) | M1-09 | review |
| [M1-11](M1-11-page-view.md) | Lecture d'une page | M1-09 | review |
| [M1-12](M1-12-page-editor.md) | Éditeur CodeMirror | M1-11 | review |
| [M1-13](M1-13-insert-palette.md) | Palette d'insertion `[[` | M1-12 | review |
| [M1-14](M1-14-attachments-ui.md) | Pièces jointes (interface) | M1-12, M1-06 | review |
| [M1-15](M1-15-preferences.md) | Préférences : langue et thème | M1-08 | review |
| [M1-16](M1-16-doctor.md) | Commande `erable doctor` | M1-06 | review |
| [M1-17](M1-17-desktop-integration.md) | Intégration desktop native | M1-08, M0-11 | review |

### M2 — Structure
| ID | Tâche | Dépend de | Statut |
|---|---|---|---|
| [M2-01](M2-01-category-domain.md) | Domaine : catégories, héritage, résolution | M1-02 | review |
| [M2-02](M2-02-property-rules.md) | Définitions de propriétés et moteur de règles | M2-01 | review |
| [M2-03](M2-03-structure-storage-api.md) | Stockage et API : catégories, propriétés, valeurs | M2-02, M1-05 | review |
| [M2-04](M2-04-category-screen.md) | Écran Catégorie | M2-03, M1-11 | review |
| [M2-05](M2-05-property-screen.md) | Écran Propriété | M2-03, M1-11 | review |
| [M2-06](M2-06-page-properties-panel.md) | Propriétés sur la page (lecture et édition) | M2-03, M1-12 | review |
| [M2-07](M2-07-typed-links.md) | Types de liens et liens | M2-03 | review |
| [M2-08](M2-08-trash.md) | Suppression et corbeille | M2-03, M1-10 | review |
| [M2-09](M2-09-format-migration.md) | Migration de format à l'ouverture | M2-03, M1-08 | review |

### M3 — Retrouver
| ID | Tâche | Dépend de | Statut |
|---|---|---|---|
| [M3-01](M3-01-search-backend.md) | Recherche plein texte et filtres (backend) | M2-03 | review |
| [M3-02](M3-02-quick-search.md) | Recherche rapide Ctrl+K | M3-01 | todo |
| [M3-03](M3-03-search-screen.md) | Écran Recherche et filtres | M3-01 | todo |
| [M3-04](M3-04-recent-items.md) | Récemment consultées / modifiées | M3-01 | review |
| [M3-05](M3-05-revision-history.md) | Historique des révisions | M1-05 | todo |
| [M3-06](M3-06-performance.md) | Générateur de gros projets et budgets | M3-01 | todo |

### M4 — Serveur
| ID | Tâche | Dépend de | Statut |
|---|---|---|---|
| [M4-01](M4-01-accounts-auth.md) | Comptes, authentification, rôles, multi-projets | M2-03 | todo |
| [M4-02](M4-02-authorization-matrix.md) | Matrice d'autorisation générée | M4-01 | todo |
| [M4-03](M4-03-signalr-events.md) | Événements temps réel | M4-01 | todo |
| [M4-04](M4-04-presence.md) | Présence | M4-03 | todo |
| [M4-05](M4-05-concurrent-editing.md) | Concurrence optimiste et fusion à trois voies | M4-03, M3-05 | todo |
| [M4-06](M4-06-server-distribution.md) | `erable serve` et image Docker | M4-01 | todo |
| [M4-07](M4-07-desktop-connected.md) | Desktop en mode connecté | M4-01, M1-17 | todo |
| [M4-08](M4-08-members-ui.md) | Gestion des membres | M4-01 | todo |
| [M4-09](M4-09-robustness-load.md) | Crash tests et charge | M4-05 | todo |

### M5 — Finition
| ID | Tâche | Dépend de | Statut |
|---|---|---|---|
| [M5-01](M5-01-shortcuts.md) | Raccourcis configurables | M1-13, M3-02 | todo |
| [M5-02](M5-02-a11y-visual.md) | Accessibilité, captures visuelles, pseudo-locale | M3-03 | todo |
| [M5-03](M5-03-release-pipeline.md) | Velopack, signature, pipeline release | M0-11 | todo |
| [M5-04](M5-04-quality-nightly.md) | Nightly, mutation testing, fuzzing | M3-06 | todo |
| [M5-05](M5-05-i18n-backup-docs.md) | i18n complète et documentation d'exploitation | toutes | todo |

## Chemin critique

```
M0-01 ─┬─ M0-03 ─ M0-04 ─────────────┐
M0-02 ─┘    └─ M0-07 ─ M0-11          │
M0-09 ─ M1-01 ─ M1-02 ─ M1-03 ─┬─ M1-05 ─ M1-06 ─ M1-07
             └─ M1-04 ─────────┘    └─ M1-08 ─ M1-09 ─ M1-11 ─ M1-12 ─ M1-13
M1-02 ─ M2-01 ─ M2-02 ─ M2-03 ─ M3-01 ─ … ─ M4-01 ─ M4-03 ─ M4-05
```

Le risque technique le plus tôt possible : **M0-08** (rendu WebKit) et **M1-12** (éditeur à pastilles).

---

## Points à trancher

Relevés pendant la planification, en relisant les documents et le mockup. Chacun indique la tâche où il doit être tranché au plus tard. Les recommandations sont celles de l'agent ; **la décision appartient à l'utilisateur**.

| ID | Constat | Recommandation | À trancher dans |
|---|---|---|---|
| **P-01** | Emplacement de la SPA incohérent : `src/web/` (§A-3) contre `web/e2e/` (features §1.3). | `src/web/`, e2e dans `src/web/e2e/`. Corriger features §1.3. | ✅ Tranché en M0-02 (features §1.3 corrigé) |
| **P-02** | Racines de l'arbre : §A-5.1 cite une racine optionnelle « (Pièces jointes) », F02.R1 en fixe quatre. | Quatre racines en v1 ; les pièces jointes restent rattachées à leur page et se trouvent via la palette et la recherche. | ✅ Tranché en M1-02 : recommandation retenue (architecture §5.1 corrigé) |
| **P-03** | **Le port aléatoire du mode desktop change l'origine de la SPA à chaque lancement** (`http://127.0.0.1:<port>`) : `localStorage`/IndexedDB sont alors perdus d'un lancement à l'autre. Or F05.R4 (brouillons), F02.R5 (état déplié) et F15.R3 (thème sans flash) supposent un stockage persistant. | Ne rien stocker de durable côté navigateur : brouillons, état de l'arbre et préférences passent par l'API (`me/…`), stockés dans le profil applicatif en local et dans `server.db` en mode serveur. Le thème est injecté dans `index.html` par le backend. Alternative : schéma personnalisé Photino à origine fixe (plus fragile). | ✅ Tranché en M0-07 : recommandation retenue, décision **Q7** (§A-13) |
| **P-04** | Verrou mono-instance (F16.R2) absent du format de projet (§A-7.1). | Fichier `erable.lock` (PID, port, jeton IPC) dans le dossier du projet, ignoré par `doctor` ; la seconde instance contacte la première pour la mettre au premier plan. | ✅ Tranché en M1-03 : `erable.lock` ouvert sans partage, PID seulement (port et jeton IPC via le profil applicatif en M1-17) |
| **P-05** | Fusion à trois voies : §A-8.3 la place **côté client**, §T-4.1 la teste **dans Core** (C#). | Fusion **côté serveur dans Core** : `PUT` avec révision de base → fusion automatique si sans conflit, sinon `409` avec les blocs en conflit. Une seule implémentation, testée par propriétés, valable pour tous les clients. | M4-05 |
| **P-06** | Critère M0 « l'app **s'installe** sur les 3 OS » alors que Velopack est prévu en M5. | M0 : artefacts `dotnet publish` autonomes (archive) + fumée ; installeur et mise à jour en M5. | ✅ Tranché en M0-11 : archives `.zip` / `.tar.gz` |
| **P-07** | Filtres de recherche sur valeurs **héritées** (F10.R3) : résoudre l'héritage à la volée par `WITH RECURSIVE` sur 10 000 pages risque de dépasser le budget de 100 ms. | Table dérivée `resolved_property_values` maintenue à l'écriture (comme `refs`), reconstruite par `doctor`. | ✅ Tranché en M2-03 : tables dérivées `resolved_values` et `validation_issues` |
| **P-08** | `projectId` dans `erable.json` : format non précisé. | Même format que `ElementId` (16 hexa), pour la cohérence des URL `/p/:projectId/…`. | ✅ Tranché en M1-03 : recommandation retenue |
| **P-09** | Le mockup d'édition affiche encore « Alt+Espace » et « fusionnés en direct », écartés par §A-11.3 et §A-8.3. | Les documents priment ; le mockup sert pour la mise en page, pas pour ces libellés. | ✅ Tranché en M1-12 : libellés des documents retenus (Ctrl+Espace, « fusionnés à l'enregistrement » en M4) |
| **P-10** | La stratégie de test est ambitieuse pour le début du projet (Stryker, NBomber, captures visuelles, fumée 3 OS dès M0). | Garder le plan, mais n'activer un outil qu'au jalon prévu en §T-16 et accepter de reporter les outils « nightly » si le coût de maintenance dépasse le bénéfice observé. | ✅ Appliqué en M0-09 : seul l'outillage M0 est installé |
| **P-11** | pnpm 7 installé localement, version ancienne. | Fixer `packageManager: pnpm@10.x` dans `package.json` et utiliser Corepack. | ✅ Tranché en M0-02 : `pnpm@10.34.6` (pnpm 12 n'est pas lançable par le Corepack 0.33 livré avec Node 22) |
| **P-12** | Titres des tests e2e en français dans features §1.3, alors que le code est en anglais. | Titre anglais précédé de l'ID (`E2E-05-03 · Insert a link via the palette`) ; la vérification de traçabilité ne porte que sur l'ID. Déjà inscrit dans `CLAUDE.md`. | ✅ Tranché en M0-09 (features §1.1 et §1.3 corrigés) |
| **P-13** | `E2E-17-02` est rattaché à M1 mais commence par `Ctrl+K`, qui relève de F10 (M3). | Rattacher `E2E-17-02` à M3 (tâche M3-02) et corriger features §F17. Alternative : une recherche par nom minimale dès M1, à partir de l'arbre chargé côté client. | ✅ Tranché en M1-10 : rattaché à M3, features §F17 corrigé |

| **P-14** | L'arbre de features §2.1 imbrique les catégories (Entité › Personnage › PNJ), mais le domaine (M1-02) n'autorise pas une catégorie sous une catégorie : l'héritage est un attribut (`parent_category_id`, §A-5.3). En M1, elles sont à plat sous la racine. | Garder l'arbre pour le rangement (dossiers) et montrer l'héritage dans l'écran Catégorie ; ou afficher les catégories selon leur héritage dans l'explorateur (le parent devient alors la catégorie, ce qui change F02.R2). | ✅ Tranché en M2-01 : l'arbre range (dossiers), l'héritage est montré dans l'écran Catégorie |
| **P-15** | Glisser un dossier sur l'accueil (E2E-16-07) : le WebView ne donne pas le chemin d'un dossier déposé et Photino.NET n'expose ni dépôt ni navigation. PhotinoX expose `NavigationStarting` (intercepter le `file:///` du dossier déposé). | Passer à PhotinoX (recommandé dès M0-08, migration ≈ 10 lignes dans `PhotinoShell.cs`) et implémenter E2E-16-07 ; ou reporter le scénario (P2) à M5-03. | ✅ Tranché en M1-17 : PhotinoX retenu (Q6 mis à jour), E2E-16-07 implémenté |

## Propositions

Améliorations proposées par les agents en cours de route, hors périmètre des tâches. Format : date · tâche d'origine · proposition · statut (proposée / acceptée / refusée).

- 2026-10-14 · M1-09 · Onglets « éléments ouverts » du mockup, absents des tâches · proposée
