# M5-07 — `erable-cli` : extraction d'un projet pour d'autres outils

- **Statut** : review
- **Jalon** : M5
- **Dépend de** : M2-03, M2-07, M5-03
- **Références** : `docs/cli.md`, §A-4, §A-5, §A-9, `docs/release.md`

## Objectif

Un exécutable `erable-cli` (projet `src/Erable.Cli`) qui restitue un projet — pages, propriétés effectives, catégories, liens, références, pièces jointes — en JSON stable (contrat `erable-cli/1`) ou en dossier d'export (JSON, Markdown, fichiers), pour qu'un outil tiers l'exploite. Le contrat de commandes, d'arguments, de codes de sortie et de documents ne doit plus casser une fois fixé.

## Périmètre

- Commandes `info`, `tree`, `page`, `pages`, `schema`, `links`, `attachments`, `attachment`, `export` ; `--pretty` ; erreurs JSON sur la sortie d'erreur, codes 0 à 4.
- Lecture seule par instantané (`ProjectStore.OpenSnapshot`) : marche projet ouvert, migre la copie d'un format ancien.
- Export Markdown avec front matter et `[[ID]]` réécrits en liens relatifs.
- Exécutable autonome par plateforme joint aux releases, produit aussi par `scripts/publish`.

## Hors périmètre

- Écriture ou import dans un projet ; lecture d'un serveur distant (HTTP) ; historique des révisions ; corbeille.

## Critères d'acceptation

- [x] Aucune commande n'écrit dans le dossier du projet (empreinte identique avant et après, projet fermé, ouvert ou d'un format ancien)
- [x] Propriétés effectives avec origine (`own`, `inherited`, `default`, `conflict`), sources et règles non respectées
- [x] Export reproductible (deux exports identiques octet pour octet), `--force` ne touche que les entrées de l'export
- [x] Références dans le code laissées telles quelles par l'export Markdown
- [x] Champs des documents figés par `ContractTests` ; contrat documenté dans `docs/cli.md`
- [x] Archive `erable-cli-<rid>` jointe à la release, vérifiée sur le projet de référence

## Notes d'implémentation

- **Instantané** : sans `-wal`, la base est copiée octet pour octet (une connexion SQLite même en lecture seule créerait `-wal`/`-shm` dans le projet) ; avec un `-wal`, un autre processus peut écrire : copie par l'API de sauvegarde SQLite d'une connexion en lecture seule. La copie vit dans un dossier temporaire supprimé au `Dispose`. `OpenedProject` accepte désormais un verrou nul et une action de fermeture.
- **Lecture groupée** : `ProjectContent` charge tout en une connexion (arbre, éléments, `StructureState`, liens, références via le nouveau `ProjectRepository.GetAllReferences`, pièces jointes) au lieu d'appeler `ProjectService` page par page (chaque appel rechargeait l'arbre).
- **Contrat** : DTO explicites (`Contract.cs`), tous les champs toujours présents (`null` plutôt qu'absent), dates `…000Z`, accents non échappés. Les rétroliens et le graphe viennent de la table dérivée `refs` ; les références d'une page (libellés, attributs) de l'analyse de son Markdown.
- **Noms de fichiers** : `SafeFileName` rend un nom de pièce jointe valide sur les trois systèmes ; le chemin est exposé dans le champ `file` pour qu'un consommateur n'ait pas à reproduire la règle.
- **Distribution** : `build/publish-cli.mjs` (fichier unique, autonome, compressé ≈ 35 Mo) ; la version par défaut vient de Git (`build/version.mjs`, partagé avec `pack-desktop.mjs`).

## Propositions

- `--include-trash` pour exporter aussi la corbeille ; `--since <date>` pour un export incrémental.
- Lire un projet servi par `erable serve` (URL + jeton) avec le même contrat, pour l'outillage d'une équipe.
- Une sortie JSON Lines (`pages --jsonl`) pour les très gros projets, traitée en flux.
