# M3-01 — Recherche plein texte et filtres (backend)

- **Statut** : review
- **Jalon** : M3
- **Dépend de** : M2-03
- **Références** : §A-7.4 (`search_fts`), §A-8.1 (`search`), F10.R2–R4, §T-6, §T-11.1, P-07

## Objectif

Une recherche qui trouve « Col gelé » en tapant « col gele », combinable avec des filtres structurés, en moins de 100 ms.

## Périmètre

- FTS5 `unicode61 remove_diacritics 2` sur nom + texte brut ; alimentation dans la transaction d'enregistrement ; reconstruction par `doctor --fix`.
- Endpoint `search` : texte, types, catégories (± sous-catégories), conditions sur propriétés **y compris héritées** (`=`, `≠`, `<`, `≤`, `>`, `≥`, contient…), date de modification relative, « hors règles » ; tri pertinence (bm25) / dernière modification / nom ; pagination par curseur.
- Extraits avec passages surlignés (`snippet`/`highlight`) renvoyés en segments structurés (`pre`, `hit`, `post`), jamais en HTML.
- Facettes : compteurs par type (affichés dans le mockup).
- Un élément créé est trouvable immédiatement (F10, E2E-10-11).

## Critères d'acceptation

- [x] Tests de stockage : accents, filtres combinés en ET, valeurs héritées, « hors règles ».
- [ ] p95 < 100 ms sur le projet généré de 10 000 pages : à mesurer en M3-06 (le générateur n'existe pas encore).
- [x] Aucune injection possible via la syntaxe FTS5 (requête utilisateur échappée).

## Notes d'implémentation

- **Format v4** (`Migrations/0004_search.sql`, `ProjectFormat.CurrentVersion = 4`). **Écart avec le plan** (« alimentation dans la transaction d'enregistrement ») : l'index est tenu à jour par la base elle-même. La vue `search_source` définit ce qui est cherchable (éléments vivants hors racines : nom + texte brut d'une page, description d'une catégorie, d'une propriété ou d'un type de lien) et des triggers recopient chaque modification dans `search_fts`, dans la transaction de l'écriture, quel que soit le chemin de code (création, renommage, enregistrement, corbeille, restauration). Aucun chemin d'écriture n'a pu oublier l'index, et la migration le remplit pour les projets existants. `spec/projects/legacy/v4` figé, `chroniques` régénéré, `LegacyProjectTests` vérifie que v1 à v3 sont cherchables après migration. `docs/architecture.md` §7.4 mis à jour.
- **Cherchable** : pages, dossiers, catégories, propriétés, types de lien et pièces jointes (par leur nom de fichier). Le mockup n'a pas de facette « Dossiers » : l'API la renvoie, l'écran (M3-03) décidera de l'afficher.
- **Texte** (`Core/Search/FullTextExpression`) : le texte de l'utilisateur n'est jamais transmis tel quel à FTS5. Il est coupé en mots (lettres, chiffres, marques), chaque mot est entre guillemets, le dernier est un préfixe (recherche en tapant) ; opérateurs, `NEAR`, noms de colonnes ou guillemets tapés deviennent des mots ordinaires (test de propriété sur 2 000 chaînes aléatoires, et cas hostiles contre un vrai FTS5). `nameOnly` limite à la colonne `name` (Ctrl+K).
- **Pertinence** : `bm25(search_fts, 8, 1)` (le nom pèse huit fois le corps), plus un malus par type (pages 0, autres éléments +2, dossiers +4, pièces jointes +6). Sans lui, bm25 favorise les lignes courtes : « portrait_ilsa.png » passait avant « Ilsa Varenne » (E2E-10-01). Même ordre de groupes que le mockup (pages, catégorie, pièce jointe).
- **Filtres** (`SearchFilter`, ET entre filtres, OU à l'intérieur d'un filtre) : types, catégories (± sous-catégories, développées côté service avec `CategoryGraph`), conditions de propriété sur `resolved_values` (valeurs héritées comprises, P-07), date relative (`day`/`week`/`month` calculée avec le `TimeProvider`), « hors règles » (`validation_issues`, avec la règle renvoyée pour chaque page). Catégories, conditions et « hors règles » ne concernent que les pages. Opérateurs : `eq ne lt le gt ge contains set unset` ; l'opérande est lu selon le type de la propriété (`<`/`>` pour int, float, date ; `contains` pour string et text, insensible à la casse et aux accents). Sur une liste, la condition tient si un élément la satisfait. **Choix** : `ne` exige une valeur (« Niveau ≠ 5 » ne liste pas les pages sans Niveau).
- **Tri** : pertinence, dernière modification, nom (collation SQLite `ERABLE_FOLD` enregistrée sur chaque connexion : insensible à la casse et aux accents, comme la recherche).
- **Extraits** : `snippet()`/`highlight()` de FTS5 avec des marqueurs privés (U+E000/E001), découpés côté serveur en segments `{text, hit}` pour le titre et l'extrait (jamais de HTML ; les mots voisins surlignés sont fusionnés : « Col gelé » est un seul passage). **Écart** : une liste de segments plutôt que `pre`/`hit`/`post`, car un extrait peut contenir plusieurs passages. Sans texte, l'extrait est le début de la page.
- **Facettes** : compteurs par type, tous filtres appliqués sauf le filtre de type lui-même (les cases décochées affichent ce qu'elles apporteraient) ; `total` en découle.
- **API** : `GET search` (`q`, `nameOnly`, `kind`*, `category`*, `subCategories`, `where`* = `idPropriété:opérateur:valeur`, `modified`, `outOfRules`, `sort`, `cursor`, `limit`). Chemin, catégories, auteur et pièce jointe sont renvoyés avec chaque résultat. **Curseur** : opaque pour le client, mais il ne porte aujourd'hui que la position du résultat suivant (pagination par décalage, pas par clé) : un résultat peut se décaler si une écriture a lieu entre deux pages. Suffisant à ce stade ; passer à la clé (rang, id) ne change pas le contrat.
- **`erable doctor`** : nouveau code `search.outOfSync` (index différent de `search_source` ou endommagé), réparé par `--fix` (reconstruction). Un `PRAGMA integrity_check` lancé sur une connexion du pool qui a gardé une image périmée de l'index FTS5 signalait une corruption fantôme : le doctor lit d'abord l'index par la commande `integrity-check` de FTS5, ce qui rafraîchit l'image.
- **Points d'attention** : `[AsParameters]` avec des enums en query est sensible à la casse et les types non nullables sont obligatoires : `sort` et `modified` sont des chaînes lues sans tenir compte de la casse, les booléens sont nullables.
- Vérification : `dotnet test --solution Erable.sln` (1 055 verts), `pnpm api:generate` (depuis `src/web`) pour `openapi.json`/`schema.ts`.
- Commit proposé : `feat(core,storage,server): add full-text search and structured filters (M3-01)`.

## Propositions

- Passer le curseur à une pagination par clé (rang, id) si l'écran Recherche fait défiler de longues listes pendant que d'autres utilisateurs écrivent (M4).
- Indexer aussi les valeurs textuelles des propriétés `string`/`text` dans `search_fts` (aujourd'hui seules les conditions `contains` les atteignent).
