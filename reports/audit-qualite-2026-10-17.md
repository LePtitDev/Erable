# Erable — Audit de qualité, de fiabilité et de préparation au déploiement

> Date : 17 octobre 2026 · Version auditée : `main` @ `68dd869` (tag `0.1.0`) · Périmètre : code, tests, CI, sécurité, exploitation, UX.
> Méthode : lecture des documents (`docs/`, `tasks/`), revue ciblée du code (sécurité, stockage, rendu), exécution locale des suites de tests et de la couverture de `Erable.Core`, analyse des exécutions GitHub Actions, parcours manuel de l'application en mode serveur sur le projet « Chroniques d'Aube ».

---

## 1. Synthèse

**La conception est d'un niveau rare pour un projet de cet âge ; ce qui manque, c'est la preuve qu'elle tient en dehors du poste de développement.**

La stratégie de test (`docs/testing.md`) est exemplaire : property-based, spécification partagée C# ↔ TS, vrai SQLite, matrice d'autorisation générée, crash tests, fuzzing, mutation, budgets de performance, traçabilité des 117 scénarios E2E. Localement, tout est vert (à un test instable près) et la couverture de `Erable.Core` dépasse l'objectif.

Mais le dépôt n'est sur GitHub que depuis aujourd'hui, et **aucune exécution de la CI n'a encore été verte** : 7 tests e2e échouent de façon reproductible, la fumée desktop échoue sous Linux et Windows, le workflow nightly est un YAML invalide et n'a jamais tourné. Mutation testing, restauration de sauvegarde, pipeline de release et signature n'ont jamais été exécutés. Une version `0.1.0` est déjà taguée.

### Verdict

| Usage | Prêt ? | Condition |
|---|---|---|
| Démo, usage personnel en local | ✅ Oui | — |
| Serveur interne, petite équipe de confiance (bêta privée) | 🟡 Presque | Corriger les bloquants B1 à B4, puis une semaine de CI verte (nightly compris) |
| Distribution desktop à des utilisateurs externes | 🔴 Pas encore | B1 à B6 : release exécutée de bout en bout, signature, fumée desktop verte sur les 3 OS |
| Serveur exposé sur Internet | 🔴 Pas encore | En plus : S1 (en-têtes de sécurité), S2 (verrouillage de compte), restauration de sauvegarde testée |

### Tableau de bord

| Dimension | Note | Commentaire |
|---|---|---|
| Architecture et conception | ★★★★★ | Invariants clairs, Core pur, ID unique, références par ID, décisions tracées (Q1…Q10, P-01…P-15) |
| Stratégie de test (sur le papier) | ★★★★★ | Pyramide complète, risques classés par gravité, quarantaine, « tout bug laisse un test » |
| Tests effectivement exécutés | ★★★★☆ | ≈ 2 100 tests unitaires et d'intégration verts en local, plus 131 e2e ; couverture mesurée seulement sur Core |
| Intégration continue | ★★☆☆☆ | Excellente conception, mais jamais verte ; nightly cassé ; mutation jamais lancée |
| Sécurité | ★★★★☆ | Très soignée (desktop, sessions, pièces jointes, XSS) ; manquent CSP/anti-clickjacking et protection contre le verrouillage ciblé |
| Fiabilité des données | ★★★★☆ | WAL + `synchronous=FULL`, crash tests, `doctor`, migrations figées v1→v4 ; restauration Litestream jamais exécutée |
| Exploitation et distribution | ★★☆☆☆ | Docker + Trivy OK ; release, mise à jour et signature jamais exécutées |
| UX et cohérence | ★★★★☆ | Fidèle au mockup, clavier et i18n soignés ; quelques incohérences de vocabulaire et d'éléments « bientôt » |

---

## 2. Chiffres clés

| Mesure | Valeur |
|---|---|
| Code de production C# | Core 4 769 l. · Storage 5 318 l. · Server 7 216 l. · Desktop 612 l. |
| Code de production TS (hors tests) | ≈ 23 300 l. |
| Code de test C# | Core 3 223 l. · Storage 3 500 l. · Server 6 951 l. · outillage 1 062 l. · benchmarks 926 l. |
| Code de test TS | 49 fichiers Vitest (≈ 6 200 l.), 27 fichiers Playwright, 131 tests e2e, 17 page objects |
| Scénarios E2E tracés | **117 / 117** (M0 à M5), contrôlé en CI par `e2e:trace --enforce` |
| Fixtures partagées C# ↔ TS | ids, limites, Markdown (refs, cas limites, titres, slugs, texte), règles (définitions, évaluation, regex, valeurs) |
| Projets figés pour les migrations | `legacy/v1` à `v4` |
| Bundle initial | 446,9 Ko gzip pour un budget de 500 Ko (**89 %** consommés) |
| Historique | 81 commits en 5 jours ; 54 tâches en `review`, 4 en `in-progress`, **0 en `done`** |

### Exécution locale (Windows, 17/10/2026)

| Suite | Résultat |
|---|---|
| `dotnet build Erable.sln` | ✅ 0 avertissement, 0 erreur |
| `Erable.Core.Tests` | ✅ 747 / 747 |
| `Erable.Storage.Tests` | ✅ 259 / 259 (2 tests `Explicit` de génération, normaux) |
| `Erable.Server.Tests` | ✅ 363 / 364 (1 ignoré) |
| Vitest | ⚠️ 757 / 758 — l'échec est un **test chronométré instable** (voir T3) ; il passe 3 fois sur 3 isolé |
| ESLint + Stylelint, `tsc -b` | ✅ |
| `pnpm build` + budget du bundle | ✅ |
| Playwright e2e | non relancé en local ; résultats CI ci-dessous |

### Couverture de `Erable.Core` (Microsoft.Testing.Extensions.CodeCoverage)

**93,6 % des lignes, 89,5 % des branches** : l'objectif de 90 % (§T-4) est atteint. Points faibles : `Element.cs` (54 %), `SearchModel.cs` (74 %), `Roots.cs` (87 %).
Storage, Server et le front ne sont **pas mesurés** : seul `Erable.Core.Tests` référence l'extension de couverture, et Vitest n'a pas de fournisseur de couverture installé.

### État de la CI GitHub (exécutions du 17/10)

| Job | État | Cause |
|---|---|---|
| Workflow lint (actionlint) | 🔴 | `nightly.yml` ligne 152 : `name: Smoke (Windows: WebView2 …)` — les deux-points non quotés rendent le YAML invalide ; plus 2 remarques shellcheck |
| Nightly | 🔴 jamais exécuté | même cause : « workflow file issue » à chaque push |
| E2E (Chromium + WebKit) | 🔴 7 échecs / 150, **identiques sur deux exécutions** | voir T1 |
| Desktop linux-x64 | 🔴 `Aborted (core dumped)` | `libPhotinoX.Native.so` exige `libnotify.so.4`, absent des dépendances installées |
| Desktop win-x64 | 🔴 8 échecs | `connectOverCDP: ECONNREFUSED 127.0.0.1:9600` : le WebView2 n'ouvre plus le port de débogage (probablement depuis le passage à PhotinoX) |
| Desktop osx-arm64 / osx-x64 | ✅ | osx-x64 publié mais jamais testé |
| Tests unitaires Linux / macOS (Release) | ✅ | Windows et Linux-Debug encore en cours au moment de l'audit ; la veille, Vitest échouait sur macOS (`PropertyView.test.tsx`), corrigé depuis |
| Docker (build, Trivy, fumée, absence de `/_test`) | ✅ | — |
| Durée du pipeline PR | ~14–15 min | objectif §T-13 : < 12 min |

---

## 3. Points forts

1. **Des documents qui servent réellement.** Architecture, fonctionnalités avec règles `Fnn.Rn` et scénarios `E2E-nn-nn`, stratégie de test classée par gravité du risque, décisions tracées et questions ouvertes (`P-nn`) tranchées explicitement. La traçabilité scénario → test est vérifiée automatiquement.
2. **Le domaine est isolé et prouvé.** `Erable.Core` sans IO, `TimeProvider` injecté, tests par propriétés (CsCheck / fast-check) sur l'ID, le parseur, l'héritage, l'arbre et la fusion à trois voies, avec l'invariant le plus important : « aucun caractère perdu sans conflit signalé ».
3. **Une spécification, deux implémentations.** Les fixtures JSON pilotent xUnit et Vitest : une divergence entre parseurs C# et TS devient visible au lieu de corrompre des liens.
4. **Pas de mock de ce qu'on possède.** Vrai SQLite en dossier temporaire, vrai serveur en mémoire, vrai navigateur ; `erable doctor` vérifie la base après les tests.
5. **Le fuzzing a déjà payé.** Il a trouvé un dépassement de pile capable d'arrêter tout le serveur (`[[` × 5 000) et des temps quadratiques côté Markdig et micromark ; la réponse (limites partagées `MarkdownLimits` / `limits.ts`, refus en 422, décision Q10) est propre et documentée.
6. **Robustesse des données pensée tôt.** WAL + `synchronous = FULL`, écriture des blobs par fichier temporaire puis `rename`, crash tests qui tuent le vrai processus puis relisent tout ce qui a été acquitté, migrations testées sur des projets figés v1→v4, verrou mono-instance.
7. **Sécurité traitée comme une fonctionnalité** (détail en §5) : matrice d'autorisation qui échoue sur tout endpoint sans règle, sessions hachées et révocables, pièces jointes servies sans risque, Markdown rendu en éléments React et non en HTML.
8. **Chaîne d'approvisionnement soignée.** Actions épinglées par SHA, Renovate, versions centralisées, Trivy sur l'image, `pnpm` épinglé par `packageManager`.
9. **Honnêteté des notes de tâches.** Chaque tâche liste ce qui n'a *pas* été vérifié (release jamais lancée, restauration Litestream jamais jouée, scores de mutation inconnus). C'est grâce à elles que cet audit peut être précis.

---

## 4. Tests et couverture des scénarios

### Ce qui est bien couvert

- **Domaine** : 747 tests, dont des propriétés sur tous les invariants listés en §T-4.1.
- **API** : un fichier de test par ressource (pages, arbre, structure, liens, recherche, corbeille, historique, pièces jointes, membres, présence, édition concurrente, migration, préférences…), plus `AuthorizationMatrixTests` sur six profils.
- **Parcours** : les 117 scénarios ont un test ; 75 tests `@pr` tournent sur Chromium et WebKit à chaque push, tout le reste (dont multi-utilisateur, concurrence, accessibilité complète, visuel) la nuit.
- **Desktop** : sécurité loopback testée (`DesktopSecurityTests`), self-test scripté et pilotage CDP du vrai binaire.

### Constats

| ID | Gravité | Constat | Recommandation |
|---|---|---|---|
| **T1** | 🔴 Haute | **7 e2e `@pr` échouent sur la CI de façon déterministe**. La palette d'insertion (commit `b2568c7` « tell embeds from links ») n'affiche plus l'ID abrégé (`7A1C…D615`) attendu par E2E-05-03, et ses options contiennent désormais des boutons « insérer un lien / intégrer » : axe-core signale `nested-interactive` (violation sérieuse). S'y ajoutent sous WebKit des délais dépassés sur la recherche rapide (E2E-10-01) et l'ouverture d'un dossier (M1-17). Les tests ont fait leur travail : c'est une régression réelle, pas une instabilité. | Corriger la palette (boutons hors du `role="option"`, ou un `listbox` avec actions au clavier), relancer les e2e localement avant chaque commit d'interface. |
| **T2** | 🔴 Haute | **Les filets « nightly » n'ont jamais tourné** : YAML invalide (ligne 152), donc ni e2e Firefox, ni visuel, ni benchmarks, ni fumée nightly, ni ouverture d'issue. Les e2e multi-utilisateur et concurrence (F14), les plus critiques selon §T-15, ne sont pas `@pr` et n'ont donc **jamais été exécutés en CI**. | Quoter la valeur (`name: "Smoke (Windows: …)"`). Envisager de passer un scénario multi-utilisateur et un scénario de concurrence en `@pr`. |
| **T3** | 🟠 Moyenne | **Assertion chronométrée dans un test unitaire** (`palette.test.ts` : `< 50 ms` sur 10 000 éléments). Mesurée à 81 ms sous charge, verte en isolé. C'est exactement le test instable que §T-1.6 proscrit. | Déplacer la mesure dans les tests `@perf` (déjà prévus pour ce budget) ou comparer à une base relative. |
| **T4** | 🟠 Moyenne | **La couverture n'est ni publiée ni bloquante en CI**, contrairement à §T-12 (« rapportée à chaque PR, seuil bloquant sur Core »). Rien n'est mesuré pour Storage, Server et le front. | Ajouter l'extension aux trois projets de test, `@vitest/coverage-v8` côté front, publier le rapport, seuil 90 % sur Core. |
| **T5** | 🟠 Moyenne | **Mutation testing jamais exécuté**, `break: 0`. Or le code et les tests sont écrits par le même agent, dans la même session : le risque principal n'est pas l'absence de tests mais des tests qui confirment l'intention de l'implémenteur plutôt que la spécification. La mutation est le meilleur contre-pouvoir disponible. | Lancer `weekly-quality.yml` à la main maintenant, publier les scores, puis `break: 70`. |
| **T6** | 🟡 Basse | Fumée osx-x64 absente, `.pkg` macOS jamais installé, mise à jour N → N+1 jamais jouée. | Faire une release de test (tag `v0.1.1-rc.1` par exemple) en suivant `docs/release.md`. |
| **T7** | 🟡 Basse | Position R10 des références fausse dans une liste contenant une tabulation (Markdig), connue et non corrigée ; la tabulation est exclue du fuzzer en attendant. | Garder ouvert, mais ajouter le cas dans `edge-cases.json` marqué « attendu en échec » pour ne pas l'oublier. |

---

## 5. Sécurité

### Ce qui est solide

| Domaine | Mesures vérifiées dans le code |
|---|---|
| Desktop loopback | Écoute `127.0.0.1` seulement ; jeton 256 bits par lancement, transmis par fragment d'URL puis échangé contre un cookie `HttpOnly; SameSite=Strict` ; comparaison à temps constant (`FixedTimeEquals`) ; `Host` exact (DNS rebinding) ; `Origin` vérifié sur les méthodes non sûres (CSRF) |
| Comptes (serveur) | Hachage PBKDF2 d'ASP.NET Core Identity avec ré-hachage automatique ; vérification d'un hachage factice pour un login inconnu (pas d'énumération par le temps) ; même réponse pour login inconnu, mauvais mot de passe et compte désactivé |
| Sessions | Jeton aléatoire 256 bits, **seul son SHA-256 est stocké** ; expiration glissante (12 h) ; révocation côté serveur ; sessions supprimées au changement de mot de passe et à la perte des droits d'administration |
| Force brute | Limitation par login (5 échecs / 15 min) et par adresse (40) ; en-têtes `X-Forwarded-*` pris en compte uniquement avec `ERABLE_TRUST_PROXY` |
| Autorisation | Rôles par projet ; métadonnée `AccessRule` obligatoire sur chaque endpoint, vérifiée pour 6 profils par `AuthorizationMatrixTests` ; toute écriture exige un éditeur |
| Contenu | HTML brut désactivé ; Markdown converti en éléments React (aucun `innerHTML`) ; liens limités à `http`, `https`, `mailto`, ancres et chemins relatifs |
| Pièces jointes | `nosniff`, `Content-Security-Policy: default-src 'none'; sandbox`, SVG/HTML et tout type non sûr servis en `application/octet-stream` + `attachment` ; taille maximale et quota par projet |
| Déni de service par contenu | Limites Markdown partagées C#/TS (taille, longueur de ligne, imbrications) issues du fuzzing |
| Points d'appui de test | `/_test` absents en Release, vérifié par la CI .NET et par la fumée Docker |
| Image | Scan Trivy (HIGH/CRITICAL bloquants) |

### Constats

| ID | Gravité | Constat | Recommandation |
|---|---|---|---|
| **S1** | 🟠 Moyenne | **Aucun en-tête de sécurité sur la SPA** : pas de `Content-Security-Policy`, ni `frame-ancestors`/`X-Frame-Options`, ni `Referrer-Policy` (vérifié sur la réponse de `/`). Le `Caddyfile` fourni n'en ajoute pas. Conséquences : clickjacking possible en mode serveur, et aucune défense en profondeur si une XSS apparaissait un jour. | Ajouter dans `SpaIndexEndpoint` (et sur `/api`) : `Content-Security-Policy: default-src 'self'; img-src 'self' data: blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'`, `Referrer-Policy: no-referrer`, et HSTS documenté côté Caddy. |
| **S2** | 🟠 Moyenne | **Verrouillage ciblé d'un compte** : 5 échecs sur un login le bloquent 15 min *quel que soit le mot de passe*. N'importe qui connaissant le login de l'administrateur peut l'empêcher de se connecter indéfiniment. | Compter les échecs par couple (login, adresse) pour le blocage dur, et n'appliquer au login seul qu'un délai progressif ; journaliser les blocages. |
| **S3** | 🟡 Basse | **Images externes autorisées** dans le Markdown (`http`/`https`) : un éditeur peut insérer un pixel de suivi qui révèle l'adresse IP et l'heure de lecture de chaque lecteur, et l'application charge du contenu hors de son origine. | Choix produit à trancher : bloquer par défaut (CSP `img-src 'self'`) avec un réglage par projet, ou proxifier. |
| **S4** | 🟡 Basse | **Pas de changement de mot de passe en libre-service** ni de procédure de réinitialisation : seul un administrateur peut le faire (`PATCH /accounts/{login}`). Pas de MFA ni d'OIDC (prévu « plus tard »). | Ajouter « Changer mon mot de passe » dans les préférences (avec l'ancien mot de passe) ; documenter la récupération du dernier administrateur. |
| **S5** | 🟡 Basse | Le ré-hachage automatique d'un mot de passe à la connexion passe par `SetPassword`, qui **supprime toutes les sessions** de l'utilisateur : une mise à jour de l'algorithme déconnecterait tous les appareils d'un utilisateur. | Une méthode de stockage du hachage sans révocation pour ce cas. |
| **S6** | 🟡 Basse | **Mise à jour automatique sans signature** : la confiance repose sur le compte GitHub ; Windows SmartScreen et Gatekeeper avertiront à l'installation. Déjà documenté dans `docs/release.md`. | Signature avant toute distribution publique ; 2FA matériel sur le compte GitHub et le registre. |
| **S7** | 🟡 Basse | Pas de `SECURITY.md` (comment signaler une vulnérabilité). | Ajouter une politique de divulgation minimale. |

Non constaté : injection SQL (Dapper, paramètres liés partout où j'ai regardé), XSS dans le rendu, exposition de jeton dans les journaux.

---

## 6. Fiabilité des données et exploitation

| ID | Gravité | Constat | Recommandation |
|---|---|---|---|
| **R1** | 🔴 Haute | **La restauration Litestream n'a jamais été exécutée** (`deploy/litestream/test-restore.sh`, noté dans M5-05). Une sauvegarde jamais restaurée n'est pas une sauvegarde. | Jouer le script une fois, puis l'ajouter au nightly ou à la release. |
| **R2** | 🟠 Moyenne | Pièces jointes répliquées toutes les 5 minutes, bases en continu : après restauration, des pages peuvent référencer des blobs absents. `doctor` le détecte, mais la procédure ne le dit pas. | Documenter ce cas dans la procédure de restauration (et la sortie attendue de `doctor`). |
| **R3** | 🟠 Moyenne | **Dépendance Linux non déclarée** : PhotinoX exige `libnotify4`, l'app plante au démarrage sans elle (vu en CI). Les utilisateurs Linux seront touchés de la même façon. | Ajouter le paquet à la CI, aux dépendances de l'AppImage/paquet et au guide. |
| **R4** | 🟡 Basse | Sauvegarde desktop = « copier le dossier quand l'application est fermée ». Copier pendant l'exécution (WAL) donne une copie potentiellement incohérente ; c'est l'erreur que fera un utilisateur. | Une commande « Exporter une sauvegarde » qui utilise l'API de sauvegarde SQLite + copie des blobs. |
| **R5** | 🟡 Basse | Le tag `0.1.0` ne déclenche pas la release (déclencheur `v*`) et a été posé alors que la CI n'était pas verte. | Supprimer ou ignorer ce tag ; taguer `v0.1.0` seulement après une CI verte. |

Points forts confirmés : WAL, `synchronous = FULL`, `busy_timeout`, migrations transactionnelles de `server.db` par `user_version`, crash tests (8 itérations par PR, 100 la nuit), `erable doctor` avec `--fix`, verrou `erable.lock`.

---

## 7. UX et cohérence fonctionnelle

Parcours effectué : connexion, accueil, ouverture de projet, arbre, recherche rapide `Ctrl+K`, lecture et édition de la page « Ilsa Varenne ». L'ensemble est fidèle au mockup, rapide (API < 70 ms), lisible, entièrement pilotable au clavier, avec des états clairs (synchronisation, nombre d'éditeurs, mode Lecture/Édition dans la barre d'état).

| ID | Constat | Proposition |
|---|---|---|
| **U1** | **Vocabulaire de validation incohérent** : la barre d'état dit « 1 erreur de validation », la pastille « hors règles », le champ est en rouge ; or l'invariant 6 est « la validation avertit, elle ne bloque pas ». « Erreur » laisse croire que l'enregistrement est refusé. | Un seul terme, « avertissement » (et couleur d'avertissement, pas d'erreur), partout. |
| **U2** | **Éléments « bientôt » visibles** : « Modèles de projet — bientôt » dans la navigation de l'accueil. Une entrée inactive dans une première version donne une impression d'inachevé. | Masquer jusqu'à ce que la fonctionnalité existe. |
| **U3** | **Sous-titre de l'accueil hors contexte** : « Ouvrez un wiki local ou connectez-vous à un serveur partagé » s'affiche dans le navigateur en mode serveur, où aucune de ces deux actions n'est possible. | Texte selon le mode (desktop / navigateur). |
| **U4** | **Dates relatives ambiguës** : « Modifiée il y a 7 m. » (minutes ou mois ?). | « il y a 7 mois » en toutes lettres, date exacte en info-bulle. |
| **U5** | **En-tête d'édition à largeur moyenne** (≈ 800 px) : le bouton *Enregistrer* passe sous le titre et le panneau Propriétés réduit l'éditeur à une colonne étroite. | Replier le panneau latéral sous un seuil, garder les actions sur la ligne du titre. |
| **U6** | **Palette d'insertion** : les boutons « insérer un lien / intégrer » ajoutés dans chaque option rendent la liste moins accessible (lecteurs d'écran) et ont cassé E2E-05-03 (voir T1). La distinction lien/intégration existait déjà au clavier (`Entrée` / `Maj+Entrée`). | Garder une seule cible par option ; indiquer les deux raccourcis dans le pied de la palette. |
| **U7** | « Chargement… » en texte brut pendant 2 à 3 s à la première ouverture du projet et de la page (build Debug, démarrage à froid). | À mesurer en Release ; sinon un squelette de mise en page plutôt qu'un texte. |
| **U8** | L'auteur s'affiche par son identifiant (« camille ») quand le compte n'existe pas sur ce serveur (projet importé). | Acceptable, mais à distinguer visuellement (« camille (compte inconnu) »). |
| **U9** | Le guide utilisateur (57 lignes) est clair mais court ; README en anglais, toute la documentation en français. | Choisir la langue du public visé ; enrichir le guide (captures, mode serveur, sauvegarde). |
| **U10** | Proposition en attente depuis M1-09 : onglets « éléments ouverts » du mockup. Pour un outil « façon IDE », c'est probablement la fonctionnalité manquante la plus attendue. | À trancher. |

---

## 8. Processus et gouvernance

- **Aucune tâche n'est `done`** : 54 en `review`, 4 en cours. Tout le travail attend une relecture humaine. Le volume (≈ 40 000 lignes de production en 5 jours, sans compter les tests) rend cette relecture difficile ; le mutation testing (T5) et une CI verte sont les garde-fous qui la rendent tenable.
- **L'index `tasks/README.md` est en retard** sur les fichiers : M5-01 y est `todo` alors que le fichier dit `review` ; la colonne « Dépend de » de M4-09 contient un texte de décision.
- **La règle « vert avant de commiter » n'est pas outillée** : le commit `b2568c7` a modifié l'interface sans relancer les e2e. Un hook de pré-push ou l'obligation d'une PR verte (branche `main` protégée, prévue en §T-13) l'aurait bloqué.
- Fichiers attendus d'un projet distribué absents : `SECURITY.md`, `CHANGELOG.md` (les notes sont générées à la release, c'est suffisant si elles sont publiées), modèles d'issue.

---

## 9. Plan d'action recommandé

### Bloquants avant toute mise entre les mains d'utilisateurs

| # | Action | Effort |
|---|---|---|
| B1 | Corriger `nightly.yml` (quoter la ligne 152) et les remarques actionlint | 10 min |
| B2 | Corriger la palette (T1/U6) et les délais WebKit, obtenir une CI PR verte | ½ à 1 j |
| B3 | Fumée desktop : `libnotify4` sous Linux, port CDP sous Windows avec PhotinoX | ½ j |
| B4 | Protéger `main` (PR verte obligatoire) et retirer le tag `0.1.0` | 15 min |
| B5 | Exécuter une release de test complète (installeurs, fumée sur l'installé, mise à jour N → N+1) | 1 j |
| B6 | Jouer la restauration Litestream de bout en bout | ½ j |

### Avant une exposition sur Internet

| # | Action | Effort |
|---|---|---|
| S1 | En-têtes CSP, `frame-ancestors`, `Referrer-Policy`, HSTS | ½ j |
| S2 | Verrouillage par (login, adresse) + délai progressif | ½ j |
| S4 | Changement de mot de passe en libre-service | ½ j |
| T5 | Premier passage Stryker, puis seuil à 70 % | ½ j + corrections |

### Ensuite

T3 (test chronométré), T4 (couverture publiée), U1 à U5, R3/R4, S3, S7, mise à jour de l'index des tâches.

---

## Annexe — Commandes utilisées

```bash
dotnet build Erable.sln
dotnet test --solution Erable.sln --no-build --coverage --coverage-output-format cobertura   # couverture : Core seulement
dotnet test --project tests/Erable.Storage.Tests --no-build
dotnet test --project tests/Erable.Server.Tests --no-build
pnpm -C src/web test        # puis lint, typecheck, build
node src/web/scripts/trace-e2e.mjs
gh run list / gh run view --log-failed     # état de la CI GitHub
```
