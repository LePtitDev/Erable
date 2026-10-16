# Erable — Stratégie de qualité et de tests

> Statut : **validé** · Version 0.1 · 13 octobre 2026
> Complète `docs/architecture.md` (les renvois « §A-x » désignent ses sections).
> Objectif : des fonctionnalités **qui marchent**, **qui restent rapides**, avec un **risque de régression faible**, sans ralentir le développement.

---

## 1. Principes

1. **Tester là où un bug coûte cher.** Ce qui est irréversible passe avant tout : perte de données, référence cassée, fusion qui écrase un texte, fuite de données hors de l'utilisateur. Un bouton mal aligné se corrige en deux minutes, ces bugs-là non.
2. **La logique va dans `Erable.Core`, pure et testée exhaustivement.** Le reste (stockage, API, UI) reste aussi fin que possible et se teste par intégration. L'architecture a été pensée pour ça : Core n'a aucune dépendance IO (§A-3).
3. **Pas de mock de ce qu'on possède.** On teste contre un vrai SQLite (fichier temporaire), un vrai serveur Kestrel en mémoire, un vrai navigateur. On ne simule que ce qui est externe ou lent.
4. **Une seule spécification pour deux implémentations.** Tout ce qui existe en C# *et* en TypeScript (syntaxe Markdown, règles de validation, format des ID) est piloté par les mêmes fichiers de cas dans `spec/fixtures/`.
5. **Tout bug corrigé laisse un test.** On écrit d'abord le test qui échoue, puis le correctif.
6. **Des tests déterministes ou pas de tests.** Un test instable (*flaky*) est mis en quarantaine dans la journée puis corrigé ou supprimé. Il ne reste jamais « à relancer ».

---

## 2. Vue d'ensemble

| Niveau | Outils | Ce qu'il couvre | Volume | Durée cible |
|---|---|---|---|---|
| Analyse statique | compilateur C# (nullable, warnings = erreurs), analyseurs .NET, `tsc --strict`, ESLint | Classes entières de bugs, avant exécution | — | < 1 min |
| Unitaires Core | xUnit v3, Shouldly, **CsCheck** (property-based) | Domaine : ID, parseur, règles, héritage, fusion | beaucoup | < 30 s |
| Spécification partagée | `spec/fixtures/*.json` exécutés par xUnit **et** Vitest | Parité C# ↔ TS | moyen | inclus |
| Stockage | xUnit + SQLite réel en dossier temporaire | Migrations, requêtes, FTS, blobs, intégrité | moyen | < 1 min |
| API / intégration | `WebApplicationFactory`, client HTTP et SignalR réels | Endpoints, auth, concurrence, événements | moyen | < 2 min |
| Frontend unitaire | Vitest, Testing Library, **fast-check**, MSW | Composants, extensions CodeMirror, stores | beaucoup | < 1 min |
| End-to-end | **Playwright** (TypeScript) contre un vrai backend | Parcours utilisateur complets, multi-utilisateur | peu, ciblés | < 8 min (PR) |
| Visuel et accessibilité | Playwright `toHaveScreenshot`, `@axe-core/playwright` | Thèmes clair/sombre, langues, contrastes, clavier | peu | nightly |
| Desktop | Fumée sur binaire packagé, 3 OS | Démarrage, WebView, loopback, fichiers natifs | très peu | < 3 min / OS |
| Performance | BenchmarkDotNet, budgets Playwright, NBomber | Temps de réponse et de rendu sur gros projet | ciblé | nightly |
| Robustesse | crash tests, mutation testing (Stryker), fuzz | Ce que les autres niveaux ne voient pas | ciblé | nightly / hebdo |

---

## 3. Analyse statique et typage

Le moyen le moins cher d'éviter un bug, c'est qu'il ne compile pas.

**C#**
- `Nullable` activé, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `.editorconfig` partagé.
- `ElementId` est un type dédié (§A-4) : impossible de passer un `long` ou un `string` à la place d'un ID.
- Un `enum` par `ElementKind` et par type de propriété, `switch` exhaustifs (avertissement CS8509 → erreur).

**TypeScript**
- `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`.
- ESLint `typescript-eslint/strict-type-checked`. `any` interdit hors fichiers générés.
- **`ElementId` est un type marqué** (`string & { __brand: "ElementId" }`), qu'on ne peut obtenir que via `parseElementId()`. C'est ce qui rend l'erreur « ID en `number` » (§A-4) impossible à écrire.
- Le client API est **généré** depuis l'OpenAPI (§A-3.1). En CI, on le régénère et on échoue si le fichier versionné diffère : un changement d'API non répercuté côté front casse le build au lieu de casser l'application.

---

## 4. Tests unitaires du domaine (`Erable.Core`)

C'est ici que se joue l'essentiel de la fiabilité. Cible : **90 % de couverture de lignes et un score de mutation ≥ 70 %** sur Core (§12). Les deux chiffres ensemble : la couverture seule ne prouve pas que les assertions vérifient quelque chose.

### 4.1 Tests par propriétés (property-based)

Plutôt que quelques exemples choisis à la main, on décrit un invariant et **CsCheck** génère des milliers d'entrées (et réduit automatiquement un échec au plus petit cas). Côté TS, **fast-check** fait de même.

| Sujet | Invariants vérifiés |
|---|---|
| `ElementId` | `Parse(Format(id)) == id` pour tout `ulong ≠ 0` ; minuscules acceptées ; `long ↔ ulong` sans perte ; 0 et longueurs ≠ 16 rejetés |
| Parseur de références | Toute référence générée insérée dans du Markdown arbitraire est retrouvée, à la bonne position ; le texte hors références est préservé ; aucune exception sur entrée aléatoire |
| Règles de validation | `between(a,b)` ⇔ `greater_or_equal(a) ∧ less_or_equal(b)` ; une valeur valide le reste après aller-retour JSON |
| Héritage de catégories | Aucun cycle accepté, quelle que soit la séquence de reparentages ; la résolution d'une valeur est indépendante de l'ordre de déclaration des catégories |
| Fusion à trois voies | `merge(b, b, x) == x` ; `merge(b, x, b) == x` ; deux éditions de sections disjointes → aucun conflit et les deux présentes ; **aucun caractère perdu sans conflit signalé** |
| Arbre | Après toute suite de déplacements valides : un seul parent par élément, aucune boucle, aucun élément hors de sa racine |

### 4.2 Tests par exemples

Pour les cas métier lisibles, en particulier tout ce qu'illustre le mockup :
- PNJ hérite de Personnage qui hérite d'Entité : la valeur vient de la catégorie la plus spécifique.
- Ilsa est PNJ + Marchand, deux défauts différents pour la même propriété : valeur marquée **en conflit** (§A-5.3).
- `Niveau = 72` avec `less_or_equal 60` : un avertissement, et l'enregistrement **n'est pas** bloqué (§A-5.4).

Nommage : `Methode_Contexte_ResultatAttendu`. Un test vérifie un seul comportement.

### 4.3 Données de test

- **Builders fluides** : `Project.Build().WithCategory("PNJ", parent: "Personnage").WithPage("Ilsa Varenne", c => c.InCategory("PNJ"))`. Les tests restent lisibles et ne dépendent pas des détails de construction.
- **Projet de démonstration « Chroniques d'Aube »**, repris du mockup, versionné dans `spec/projects/`. Il sert à la fois de jeu de données e2e, de base pour les captures visuelles et de démo. Un seul monde de test, que tout le monde connaît.
- **Générateur de gros projets** à graine fixe (`--pages 10000 --seed 42`), pour la performance (§11).

---

## 5. Spécification partagée C# ↔ TypeScript

Trois logiques existent en double (§A-5.4, §A-6.3) : parseur de références, règles de validation, format des ID. Elles sont décrites une seule fois :

```
spec/fixtures/
├─ ids.json            { "input": "3f2a9c14e07b5d21", "valid": true, "canonical": "3F2A9C14E07B5D21" }
├─ rules/              evaluation.json { "type": "int", "rules": [{"op":"less_or_equal","value":60}], "value": 72, "violations": [{"op":"less_or_equal"}] }
│                      definitions.json, values.json, regex.json
└─ markdown/
   ├─ refs.json        { "md": "Voir [[3F2A9C14E07B5D21|lui]].", "refs": [{ "id": "…", "label": "lui", "embed": false, "start": 5 }] }
   └─ edge-cases.json  refs dans du code, échappements, crochets imbriqués, ID de 15/17 caractères…
```

- Un **seul lanceur** par langage lit tous les fichiers ; ajouter un cas = ajouter une ligne JSON, sans code.
- Les deux suites tournent dans la même CI : si l'une passe et l'autre échoue, la divergence est visible immédiatement.
- Les cas trouvés par property-based testing (§4.1) sont ajoutés ici, pour que l'autre implémentation en profite.

---

## 6. Stockage

Toujours contre un **vrai SQLite**, dans un dossier temporaire par test (rapide : quelques millisecondes).

- **Migrations** : pour chaque `formatVersion` publiée, un projet figé est conservé dans `spec/projects/legacy/vN/`. Le test l'ouvre, migre vers la version courante, vérifie `PRAGMA integrity_check` et le contenu. **On ne casse jamais un projet existant d'un utilisateur.**
- **Requêtes** : filtres sur propriétés (y compris valeurs héritées), recherche FTS (accents : « col gele » trouve « Col gelé »), `WITH RECURSIVE` sur l'héritage.
- **Blobs** : déduplication par SHA-256, ramasse-miettes (un blob encore référencé par une révision n'est **pas** supprimé), requêtes `Range`.
- **Commande `erable doctor`** : vérifie intégrité SQLite, blobs orphelins ou manquants, références cassées, cycles, index FTS désynchronisé. Elle est testée sur des projets volontairement corrompus, et exécutée à la fin de chaque test e2e pour vérifier que la base laissée est saine.

### 6.1 Résistance aux interruptions

Un wiki local doit survivre à une coupure de courant.
- Test qui **tue le processus** (`Process.Kill`) à des instants aléatoires pendant une rafale d'écritures, puis rouvre le projet : `integrity_check` OK, et chaque enregistrement est soit entièrement présent, soit absent.
- Écriture des blobs : fichier temporaire, puis `rename` atomique, puis insertion en base. Un blob incomplet ne doit jamais être référencé.

---

## 7. API et intégration

`WebApplicationFactory<Program>` démarre le vrai serveur en mémoire, avec un vrai projet SQLite.

- **Un test par endpoint** pour le cas nominal et les erreurs (404, 409, 422), avec des réponses au format `ProblemDetails`.
- **Matrice d'autorisation générée** : le test énumère *tous* les endpoints exposés via l'`EndpointDataSource` et les appelle avec chaque rôle (anonyme, lecteur, éditeur, administrateur, membre d'un autre projet). Un nouvel endpoint sans règle d'autorisation explicite **fait échouer la suite**. C'est la protection contre le bug de sécurité le plus courant : l'endpoint oublié.
- **Contrat** : le document OpenAPI est comparé à un instantané versionné (bibliothèque **Verify**). Une modification de l'API apparaît dans la revue de code sous forme de diff lisible ; une suppression de champ dans `/api/v1` est refusée.
- **Concurrence** : deux clients enregistrent la même page depuis la même révision → l'un reçoit `409` avec la version courante ; N écritures parallèles sur des pages différentes → aucune perte.
- **SignalR** : deux clients connectés au hub ; une modification par A produit `ElementChanged` chez B, mais pas chez un client d'un autre projet. Présence : connexion, départ, déconnexion brutale (le fantôme disparaît après expiration).
- **Sécurité du mode local** (§A-9) : requête sans jeton → 401 ; `Host` différent de `127.0.0.1:port` → refus ; écoute sur `0.0.0.0` impossible en mode desktop.

---

## 8. Frontend

### 8.1 Unitaires et composants (Vitest)

- **Extensions CodeMirror testées sans DOM** : on construit un `EditorState`, on applique des transactions, on vérifie les décorations (pastilles d'ID), les plages atomiques et les propositions de la palette `[[`. C'est rapide et fiable, contrairement à la simulation de frappe dans un navigateur.
- **Composants** avec Testing Library : on interroge par rôle et par libellé accessible (`getByRole("treeitem", { name: "PNJ" })`), jamais par classe CSS. Bonus : un composant testable de cette façon est aussi accessible.
- **API simulée** par **MSW**, avec des handlers typés à partir du client généré : les mocks ne peuvent pas dériver du vrai contrat.
- **Rendu Markdown** : corpus XSS (`<script>`, `javascript:`, `onerror=`, SVG malicieux) dont le rendu ne doit produire aucun élément exécutable.

### 8.2 Internationalisation

- Test qui vérifie que **chaque clé de `fr` existe dans `en`, `de` et `es`**, et inversement, avec les mêmes paramètres d'interpolation.
- Règle ESLint interdisant les chaînes en dur dans le JSX.
- Une pseudo-locale (`xx`, textes allongés de 40 % et accentués) pour repérer dans les captures visuelles les débordements de mise en page et les textes non traduits.

---

## 9. End-to-end (Playwright)

Contre un **vrai backend** en mode serveur, lancé sur un projet « Chroniques d'Aube » fraîchement copié pour chaque fichier de test (isolation complète, exécution parallèle possible).

### 9.1 Navigateurs

- **Chromium** : moteur du WebView2 (Windows) et des navigateurs web majoritaires.
- **WebKit** : le plus proche de WKWebView (macOS) et de WebKitGTK (Linux), utilisés par Photino. C'est notre meilleur filet pour le risque identifié en §A-10.
- Firefox : nightly seulement (usage web).

### 9.2 Parcours couverts

Un fichier par écran du mockup, plus les parcours transverses. On en garde **peu, mais de bout en bout** :

| Parcours | Vérifie notamment |
|---|---|
| Accueil | Créer un projet, l'ouvrir, le retrouver dans les récents |
| Arbre | Créer dossier et page, glisser-déposer, renommer → **les références `[[ID]]` affichent le nouveau nom partout** |
| Édition | `[[` ouvre la palette, filtre, `Entrée` insère un lien, `Maj+Entrée` une intégration ; collage d'image → pièce jointe ; `Ctrl+S` |
| Page | TOC, sous-pages, propriétés héritées « via PNJ », liens entrants, avertissement de validation |
| Catégorie / Propriété | Héritage, conflit de valeurs, ajout de règle → « 2 pages hors règles » |
| Recherche | `Ctrl+K`, filtres combinés, récemment consultées / modifiées |
| Suppression | Avertissement « N pages référencent… », corbeille, restauration, lien cassé affiché proprement |
| **Multi-utilisateur** | Deux contextes navigateur : présence visible, modification propagée sans rechargement, enregistrements concurrents fusionnés sans perte |
| Historique | Comparer deux révisions, restaurer |

Règles d'écriture :
- sélecteurs par rôle et libellé, ou `data-testid` en dernier recours ;
- jamais de `waitForTimeout` : uniquement des attentes sur un état (`expect(...).toBeVisible()`) ;
- *page objects* légers par écran (`EditorPage`, `TreePanel`) pour que les changements d'UI ne touchent qu'un fichier ;
- trace Playwright conservée en cas d'échec (vidéo, réseau, DOM), en artefact CI.

### 9.3 Visuel et accessibilité (nightly)

- Captures de chaque écran du mockup en **clair/sombre × fr/en × xx**, comparées à des références. Le rendu est stabilisé (polices embarquées, animations coupées, horloge figée) et exécuté dans un conteneur Docker fixe pour éviter les différences d'un poste à l'autre.
- **axe-core** sur chaque écran : zéro violation « serious » ou « critical ». Contraste vérifié dans les deux thèmes.
- Un parcours **100 % clavier** (arbre, édition, palette, recherche), puisque l'outil vise une ergonomie de type IDE.

---

## 10. Application desktop

Playwright ne pilote pas tous les WebViews natifs ; on combine donc deux approches.

- **Windows** : WebView2 expose le protocole DevTools. En lançant l'app avec `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=…`, Playwright s'y connecte (`connectOverCDP`) et rejoue un sous-ensemble des parcours e2e **dans le vrai binaire**.
- **macOS et Linux** : mode **`--self-test`**. L'app démarre sur un projet temporaire, la SPA exécute un scénario scripté (créer une page, insérer une référence, joindre une image, rechercher, enregistrer), envoie le résultat au backend, et le processus sort avec un code 0 ou 1. Sous Linux, ça tourne sous `xvfb`.
- Ce test de fumée s'exécute sur **l'artefact packagé** (sortie Velopack), pas sur le build de dev : c'est l'installeur qu'on livre qui doit fonctionner.
- Vérifié aussi : port aléatoire réellement sur loopback, second lancement avec le même projet (verrou ou réutilisation de l'instance), fermeture propre (WAL fusionné, aucun processus résiduel).

---

## 11. Performance

« Efficace » doit être mesuré, sinon ça se dégrade sans que personne ne s'en aperçoive.

### 11.1 Budgets

Mesurés sur le projet généré **10 000 pages / 300 catégories / 50 000 références / 2 Go de pièces jointes**, sur la machine de CI de référence :

| Opération | Budget (p95) |
|---|---|
| Ouverture du projet → arbre affiché | < 1 s |
| Chargement de l'arbre (API) | < 150 ms |
| Ouverture d'une page (API + rendu) | < 200 ms |
| Enregistrement d'une page (index, refs, FTS, validation) | < 100 ms |
| Recherche plein texte + filtres | < 100 ms |
| Palette `[[` : frappe → propositions | < 50 ms |
| Frappe dans l'éditeur, page de 5 000 lignes | aucune frame > 50 ms |
| Bundle JS initial (gzip) | < 500 Ko |

### 11.2 Mesure

- **BenchmarkDotNet** sur les chemins chauds de Core et Storage (parseur, résolution des propriétés, requêtes de recherche). Résultats historisés ; une régression **> 15 %** fait échouer le nightly.
- **Playwright** mesure les budgets d'UI via `performance.mark` posés dans la SPA.
- **NBomber** (C#) simule 50 utilisateurs (80 % lecture, 20 % écriture) sur un serveur pendant 10 minutes : aucune erreur, p95 dans les budgets, mémoire stable (pas de fuite).
- Taille du bundle vérifiée à chaque PR.

**Outillage (M3-06)** : `tests/Erable.Benchmarks` (Release) fournit `generate` (projet à graine fixe, `--light` pour la CI), `budgets` (p95 serveur en HTTP sur le projet généré), `bench` (BenchmarkDotNet en processus, historique `artifacts/perf/history.json`, comparaison à `spec/perf/baseline.json`, échec au-delà de +15 %) et `compare`. Les budgets du navigateur sont les tests Playwright `@perf` (`pnpm e2e:perf`, avec `ERABLE_LARGE_PROJECT` pour le grand projet). Les commandes sont dans `CLAUDE.md` §9, les mesures du jour dans `tasks/M3-06-performance.md`.

---

## 12. Mesurer la qualité des tests eux-mêmes

- **Mutation testing** : Stryker.NET sur `Erable.Core` et StrykerJS sur les modules TS dupliqués (règles, parseur), chaque semaine. Stryker introduit des bugs volontaires (`<` → `<=`, condition inversée…) et vérifie qu'un test échoue. Le score ≥ 70 % évite des tests qui passent sans rien vérifier.
- **Couverture** : rapportée à chaque PR, mais seuil bloquant **uniquement sur Core**. Ailleurs, elle sert à repérer les trous, pas d'objectif chiffré : un objectif global pousse à écrire des tests inutiles.
- **Fuzzing** du parseur Markdown (C# et TS) avec entrées aléatoires et corpus de cas réels pendant 10 minutes chaque semaine : aucun crash, aucune boucle infinie, temps linéaire en la taille de l'entrée.

---

## 13. Intégration continue

GitHub Actions, trois pipelines :

| Pipeline | Déclencheur | Contenu | Durée cible |
|---|---|---|---|
| **PR** | chaque push | lint + typage, unitaires C# et TS, spec partagée, stockage, API, e2e Chromium + WebKit (parcours critiques), client OpenAPI à jour, taille du bundle. Build + tests unitaires sur **Windows, Linux et macOS** | < 12 min |
| **Nightly** | chaque nuit sur `main` | e2e complet (3 navigateurs), visuel, accessibilité, benchmarks, charge NBomber, desktop fumée sur 3 OS | < 45 min |
| **Release** | tag `v*` | build packagé, signature, fumée desktop sur les installeurs, migration de tous les projets `legacy/`, image Docker + scan de vulnérabilités | — |

- `main` est protégée : merge uniquement si le pipeline PR est vert et qu'une revue a été faite.
- Les dépendances sont mises à jour par **Renovate**, regroupées chaque semaine, et passent par le même pipeline.
- Un échec nightly ouvre automatiquement une issue.

---

## 14. Définition de « terminé » pour une fonctionnalité

Une fonctionnalité est livrable quand :

- [ ] sa logique métier est dans Core avec des tests unitaires (et des propriétés si elle a des invariants) ;
- [ ] toute nouvelle syntaxe ou règle partagée a ses cas dans `spec/fixtures/` ;
- [ ] ses endpoints ont des tests d'intégration et figurent dans la matrice d'autorisation ;
- [ ] si elle modifie le schéma : migration + projet `legacy/` mis à jour + `erable doctor` adapté ;
- [ ] le parcours utilisateur principal a un test e2e (ou complète un existant) ;
- [ ] ses textes sont traduits dans les quatre langues ;
- [ ] elle passe axe-core et s'utilise au clavier ;
- [ ] aucun budget de performance n'a régressé.

---

## 15. Risques principaux et parades

| Risque | Gravité | Parades |
|---|---|---|
| Perte ou corruption d'un projet | Critique | SQLite WAL, crash tests (§6.1), `erable doctor` après chaque e2e, migrations testées sur projets figés |
| Fusion qui écrase silencieusement du texte | Critique | Propriétés de fusion (§4.1), e2e multi-utilisateur, historique restaurable |
| ID corrompu (précision JS) | Élevée | Type marqué TS, convertisseur JSON, fixtures `ids.json` |
| Références cassées après renommage ou déplacement | Élevée | Référence par ID uniquement, e2e « renommer » (§9.2), détection dans `doctor` |
| Parseurs C# et TS divergents | Moyenne | Spécification partagée (§5), fuzz sur les deux |
| Endpoint sans autorisation | Élevée | Matrice générée (§7) |
| XSS entre utilisateurs | Élevée | Sanitisation + corpus XSS (§8.1), pièces jointes non rendues inline |
| Accès au serveur loopback par un autre processus ou un site web | Élevée | Jeton + contrôle `Host`, tests §7 |
| Rendu différent sous WebKitGTK / WKWebView | Moyenne | e2e WebKit à chaque PR, self-test desktop sur 3 OS (§10) |
| Lenteur sur gros projet | Moyenne | Budgets mesurés (§11), projet généré de 10 000 pages |
| Tests instables qui font ignorer la CI | Moyenne | Quarantaine immédiate, aucune attente temporelle dans les e2e |

---

## 16. Mise en place par jalon

| Jalon (§A-12) | Infrastructure de test livrée |
|---|---|
| **M0** | Projets de test, analyseurs, CI PR sur 3 OS, Playwright Chromium + WebKit, self-test desktop, vérification du client OpenAPI, instantané OpenAPI |
| **M1** | Builders, fixtures `ids.json` et `markdown/`, property-based sur ID et parseur, tests CodeMirror, projet « Chroniques d'Aube », premier `legacy/v1`, `erable doctor` |
| **M2** | Fixtures `rules.json`, propriétés sur héritage et arbre, e2e Catégorie / Propriété |
| **M3** | Générateur de gros projets, budgets de performance, BenchmarkDotNet, e2e Recherche |
| **M4** | Matrice d'autorisation, tests de concurrence et SignalR, e2e multi-utilisateur, NBomber, crash tests |
| **M5** | Visuel + axe-core + pseudo-locale, pipeline release, Stryker et fuzzing hebdomadaires |

L'investissement est concentré sur **M0 et M1**. Une fois l'outillage et les conventions en place, ajouter des tests ne coûte presque rien. Les rajouter après coup à du code non conçu pour être testé coûte beaucoup plus.
