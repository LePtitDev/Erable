# M4-09 — Crash tests et charge

- **Statut** : review
- **Jalon** : M4
- **Dépend de** : M4-05
- **Références** : §T-6.1, §T-11.2 (NBomber), §T-15

## Objectif

Prouver qu'Erable survit à une coupure brutale et à une équipe réelle.

## Périmètre

- Crash test : processus serveur tué (`Process.Kill`) à des instants aléatoires pendant une rafale d'écritures (pages, pièces jointes), puis réouverture : `integrity_check` OK, chaque enregistrement entièrement présent ou absent, `doctor` sans erreur.
- NBomber : 50 utilisateurs (80 % lecture, 20 % écriture) pendant 10 minutes : aucune erreur, p95 dans les budgets, mémoire stable.
- Exécution nightly.

## Critères d'acceptation

- [x] Crash test sur 100 itérations sans corruption (6 min 24 s, `ERABLE_CRASH_ITERATIONS=100`).
- [x] Rapport de charge dans les notes de la tâche (ci-dessous) — **mais un budget n’est pas tenu** : voir le rapport.

## Notes d'implémentation

**Décisions**
- **Crash test** (`tests/Erable.Server.Tests/Robustness/CrashTests.cs`, `ServerProcess.cs`) : le vrai serveur (`dotnet erable.dll serve <dossier de données>`, port 0) est un **processus fils** ; 6 écrivains parallèles enregistrent des pages (révision lue, `If-Match`), créent des pages et téléversent des pièces jointes de 20 à 400 Ko ; au bout d’un délai aléatoire (40–900 ms) le processus est tué (`Kill(entireProcessTree)`, ni arrêt propre ni flush). Après chaque coupure : `ProjectDoctor.Diagnose` — aucune anomalie hors **fichiers orphelins ou temporaires** (un téléversement interrompu : le blob est écrit avant d’être référencé, c’est l’ordre voulu par docs/testing.md §6.1), réparables par `--fix` après quoi le projet doit être sain. Au redémarrage suivant, chaque page acquittée doit contenir **exactement** le dernier texte acquitté ou celui qui était en vol (jamais un mélange), chaque pièce jointe acquittée doit se télécharger avec son SHA-256. Le même projet traverse toutes les itérations (les coupures s’accumulent). Un 5xx avant la coupure fait échouer le test.
- Itérations : `ERABLE_CRASH_ITERATIONS` (**8 par défaut** pour ne pas alourdir `dotnet test` — environ 3 s par itération —, **100 la nuit**), `ERABLE_CRASH_SEED` pour rejouer un tirage (le message d’échec donne l’itération et la graine).
- **Charge** (`tests/Erable.Benchmarks/Load.cs`, commande `load`) : NBomber 6.7.1, `Simulation.KeepConstant(50, 10 min)`, 40 % ouvertures de page, 15 % arbre, 15 % recherche, 10 % recherche rapide, 20 % enregistrements (chaque utilisateur n’écrit que sa tranche de pages : un vrai conflit est un 409 voulu, pas ce que l’on mesure) ; temps de réflexion de 0,3 à 1,2 s. Échec si une requête échoue, si un p95 dépasse le budget du §11.1 (arbre 150 ms, page 200, recherches 100, enregistrement 100) ou si la **mémoire croît** : moyenne du jeu de travail du 4ᵉ quart supérieure de plus de 25 % à celle du 2ᵉ quart (le 1ᵉʳ est la montée en charge). Rapport : `--json` + rapports NBomber (`--reports`).
- **Nightly** : `.github/workflows/nightly-robustness.yml` (crash 100 itérations ; charge 10 minutes avec `--light`, rapports en artefact). Le workflow nightly complet de docs/testing.md §13 (e2e 3 navigateurs, visuel…) n’existe pas encore ; ce fichier n’en couvre que la robustesse.

**Rapport de charge** (Windows, Release, serveur et générateur sur la même machine, projet `--light` de 10 000 pages, 50 utilisateurs, 10 minutes, 80 % lecture / 20 % écriture, 0 erreur dans les deux passages) :

| Étape | Réaliste (2–6 s entre actions) : requêtes · p50 · p95 | Stress (0,3–1,2 s) : p50 · p95 | Budget p95 |
|---|---|---|---|
| Enregistrer une page | 1 508 · 61 ms · **116 ms** | 1 669 ms · 2 349 ms | 100 ms |
| Ouvrir une page | 2 898 · 89 ms · 142 ms | 229 ms · 403 ms | 200 ms |
| Arbre | 1 117 · 37 ms · 67 ms | 92 ms · 187 ms | 150 ms |
| Recherche plein texte | 1 104 · 59 ms · 85 ms | 126 ms · 217 ms | 100 ms |
| Recherche rapide | 764 · 11 ms · 24 ms | 25 ms · 57 ms | 100 ms |

Mémoire (jeu de travail) : 302 Mo puis 272 Mo (−10 %) en rythme réaliste ; 393 Mo puis 377 Mo (−4 %) en stress : **stable, pas de fuite**. Constat : en rythme réaliste tout est dans les budgets sauf l’**enregistrement (p95 116 ms pour 100 ms)** ; en stress (≈ 65 requêtes/s, dont ≈ 8 enregistrements/s) l’écriture sature (SQLite à un seul écrivain, index et FTS dans la transaction) et tout dépasse. Le critère « p95 dans les budgets » n’est donc pas entièrement atteint.

**Point à challenger** — *constat* : NBomber 5+ exige une licence pour un usage en organisation (gratuit pour un usage personnel/open source). *Impact* : bloquant si le projet devient commercial. *Options* : (a) garder NBomber (prévu par docs/testing.md) ; (b) k6 (AGPL, binaire séparé) ; (c) un générateur de charge maison de 60 lignes (déjà proche de `budgets`). *Recommandation* : (a) tant que le dépôt est personnel/OSS, à réévaluer sinon.


## Propositions

- **Optimiser l’enregistrement d’une page** (p95 116 ms sous 50 utilisateurs, budget 100 ms ; saturation vers 8 enregistrements/s) : profiler la transaction (index de références, FTS, validation, fusion). À planifier avant d’annoncer le budget tenu en équipe.

**Message de commit proposé** : `test(server): add the crash test and the NBomber load test with a nightly workflow (M4-09)`
