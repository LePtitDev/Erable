# M3-06 — Générateur de gros projets et budgets de performance

- **Statut** : review
- **Jalon** : M3
- **Dépend de** : M3-01
- **Références** : §T-4.3, §T-11, §T-16 (M3)

## Objectif

Mesurer la performance pour qu'elle ne se dégrade pas sans qu'on le voie.

## Périmètre

- Générateur à graine fixe : `--pages 10000 --categories 300 --refs 50000 --attachments-size 2GB --seed 42` (option pour des pièces jointes factices plus légères en CI).
- BenchmarkDotNet sur les chemins chauds : parseur, résolution des propriétés, enregistrement, recherche. Résultats historisés (fichier JSON) ; régression > 15 % signalée.
- Marques `performance.mark` dans la SPA et tests Playwright de budget (§T-11.1).
- Contrôle de la taille du bundle (< 500 Ko gzip) en échec bloquant.
- Rapport des budgets actuels dans les notes de la tâche ; tout budget dépassé est signalé avec une proposition.

## Critères d'acceptation

- [x] Tous les budgets de §T-11.1 mesurés ; écarts documentés (tableau ci-dessous).
- [x] Commandes ajoutées à `CLAUDE.md` §9.

## Notes d'implémentation

- **Générateur** (`tests/Erable.Testing/LargeProject.cs`, commande `generate`) : graine fixe, mêmes IDs d'une exécution à l'autre ; 100 dossiers, 300 catégories (12 racines, les autres héritent d'une catégorie antérieure), 5 propriétés avec règles, valeurs et catégories par page, références réparties au hasard (50 000), dates de modification sur 90 jours, pièces jointes de contenu pseudo-aléatoire (`--attachments-size 2GB` par fichiers de 1 Mo ; `--light` = 16 Mo en fichiers de 64 Ko pour la CI). 10 000 pages en 10 s, 2 Go de pièces jointes en 1 min 15 ; `erable doctor` sain. Le projet n'est pas versionné (`artifacts/`).
- **Outil** : nouveau projet `tests/Erable.Benchmarks` (exécutable, hors des tests), commandes `generate`, `budgets`, `bench`, `compare` (voir `CLAUDE.md` §9). `budgets` démarre le vrai backend sur la boucle locale et mesure en HTTP (p95 sur 30 à 60 échantillons après 2 appels de chauffe). `bench` : BenchmarkDotNet (`ShortRun`, en processus, donc sans projet auxiliaire à construire), historique dans `artifacts/perf/history.json`, comparaison à `spec/perf/baseline.json` (**versionné**) et échec au-delà de **+15 %**. Le planning nightly est de M5-04 ; en attendant la commande se lance à la main.
- **Navigateur** : `performance.mark("erable:tree-shown")` dans l'explorateur et `e2e/specs/perf-large.spec.ts` (`@perf`, ignoré sans `ERABLE_LARGE_PROJECT`, que `/_test/reset?fixture=large` copie). La frappe dans une page de 5 000 lignes et l'ouverture d'une page du projet de référence existaient (M1).
- **Bundle** : `pnpm bundle-size` échoue au-delà de 500 Ko gzip et tourne déjà dans `pr.yml` (bloquant) ; mesure du jour : 430,4 Ko.

**Mesures du jour** (machine de développement Windows, projet généré 10 000 pages / 300 catégories / 50 000 références / 2 Go ; p95 sauf mention) :

| Budget (§T-11.1) | Mesure | Budget | |
|---|---|---|---|
| Ouverture du projet → arbre affiché | 490–620 ms (navigateur, `erable:tree-shown` depuis le début de la navigation) ; 546 ms côté API à froid | < 1 s | ok |
| Chargement de l'arbre (API) | 77 ms (199 ms avant correction, voir ci-dessous) | < 150 ms | ok |
| Ouverture d'une page (API + rendu) | 97 ms côté API ; 124 ms avec le rendu (navigateur) | < 200 ms | ok |
| Enregistrement d'une page | 67 ms | < 100 ms | ok |
| Recherche plein texte + filtres | 49 ms texte seul ; 70 ms avec type et catégorie ; 47 ms condition sur valeur héritée ; 7 ms « hors règles » ; 10 ms recherche rapide | < 100 ms | ok |
| Palette `[[` : frappe → propositions | 37–39 ms (aller-retours Playwright compris) | < 50 ms | ok |
| Frappe, page de 5 000 lignes | aucune tâche longue > 50 ms | aucune frame > 50 ms | ok |
| Bundle JS initial | 430,4 Ko gzip | < 500 Ko | ok |

**Écarts trouvés et traités**
- **Arbre (API) 199 ms > 150 ms** : `HasDuplicateName` listait et triait les frères de chacun des 10 000 nœuds (85 ms à lui seul, pour 13 ms de chargement). Remplacé par `ElementTree.DuplicateNames()`, un seul passage (test : même réponse que `HasDuplicateName` pour chaque nœud). 199 → 77 ms.
- **Analyse Markdown d'un seul paragraphe de milliers de lignes (non corrigé)** : `ErableMarkdown.Analyze` met environ 1,9 s pour 5 000 lignes sans ligne vide (1 s sans aucune référence), contre 18 ms pour les mêmes lignes en paragraphes de cinq lignes. Le coût vient de Markdig (traitement des inlines avec `UsePreciseSourceLocation`), il est plus que linéaire. Une sauvegarde de ce texte collé tel quel dépasserait donc le budget de 100 ms. Suivi par le benchmark `ParseOneHugeParagraph` (1 000 lignes : 41 ms). **Proposition** : scinder en blocs les paragraphes de plus de N lignes avant l'analyse, ou analyser par tranches ; à décider, cela touche la grammaire partagée C#/TS.
- **Surcoût du chargement de l'arbre à chaque requête** : l'ouverture d'une page (≈ 80 ms de p50) et d'autres appels rechargent les 10 000 nœuds depuis SQLite. Dans les budgets, mais c'est le premier poste si M4 ajoute des utilisateurs : **proposition** de garder l'arbre en mémoire dans `ProjectService`, invalidé par les écritures.

**Chemins chauds (BenchmarkDotNet, 10 000 pages, valeurs de `spec/perf/baseline.json`)** : analyse d'une page de 5 000 lignes 19,6 ms ; résolution des propriétés d'une page 36 ms (dont le chargement de l'état de structure) ; enregistrement 43 ms ; chargement de l'arbre 12 ms ; recherche 34 ms (texte), 54 ms (avec condition), 5 ms (« hors règles »).

- Limite : une seule machine, un seul passage par mesure. Les budgets du navigateur sont mesurés en Chromium ; WebKit n'a pas été mesuré.
- Vérification faite pendant la tâche : `budgets` (serveur), `e2e:perf` avec le grand projet, `bundle-size`, `bench`, tests Core de `DuplicateNames`.
- Commit proposé : `feat(testing,server,web): add the large project generator, benchmarks and performance budgets (M3-06)`.

## Propositions

- Cache de l'arbre dans `ProjectService` et traitement des très longs paragraphes (voir plus haut).
- Planifier `bench` et `budgets --light` dans le nightly (M5-04) et publier `artifacts/perf/history.json` comme artefact.
