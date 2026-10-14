# M3-01 — Recherche plein texte et filtres (backend)

- **Statut** : todo
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

- [ ] Tests de stockage : accents, filtres combinés en ET, valeurs héritées, « hors règles ».
- [ ] p95 < 100 ms sur le projet généré de 10 000 pages (M3-06).
- [ ] Aucune injection possible via la syntaxe FTS5 (requête utilisateur échappée).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
