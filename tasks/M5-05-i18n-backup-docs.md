# M5-05 — i18n complète et documentation d'exploitation

- **Statut** : in-progress
- **Jalon** : M5
- **Dépend de** : toutes les tâches fonctionnelles
- **Références** : §A-7.2 (Litestream), §A-12 (M5), F15.R1

## Objectif

Finaliser ce qui fait d'Erable un produit distribuable.

## Périmètre

- Relecture des traductions `en`, `de`, `es` ; liste des chaînes marquées « à relire » remise à l'utilisateur.
- Libellés par défaut du contenu (racines, modèles de projet) dans les 4 langues de contenu.
- Documentation utilisateur minimale : démarrage, syntaxe `[[ID]]`, raccourcis, dossiers vs sous-pages (§A-11.1, point 6).
- Documentation d'exploitation serveur : installation Docker, sauvegarde continue avec Litestream (exemple testé), restauration, mise à jour.
- Vérification finale de la traçabilité : tous les `E2E-*` ont un test vert.

## Critères d'acceptation

- [x] Script de traçabilité sans scénario manquant.
- [ ] Restauration depuis une sauvegarde Litestream testée une fois de bout en bout.

## Notes d'implémentation

- **Traductions** : `docs/i18n-review.md`. Vérifications automatiques (parité des clés et des paramètres, balayage des valeurs `en`/`de`/`es` restées en français ou identiques à `fr` : rien d'anormal), terminologie comparée entre langues, et **liste des 39 phrases longues** à faire relire par une personne de chaque langue (pas de marqueur « à relire » dans les JSON : la liste vit dans le document). Point à trancher : `de` écrit « Link » pour le lien dans le texte et « Verknüpfung » pour le lien typé. Je n'ai modifié aucune traduction : aucune erreur certaine n'a été trouvée, et une retouche sans locuteur natif aurait été une devinette.
- **Libellés par défaut du contenu** : les quatre racines existaient déjà en `fr`/`en`/`de`/`es` (`Roots.Label`) ; test `RootsTests` ajouté (libellés distincts et non vides, repli sur l'anglais, `fr-CA`). Il n'y a pas de modèles de projet dans le produit : rien à traduire pour l'instant.
- **Documentation utilisateur** : `docs/user-guide.md` (démarrage, `[[ID]]`, raccourcis, dossiers et sous-pages).
- **Exploitation** : `docs/deployment.md` §6 + `deploy/docker-compose.litestream.yml` (Litestream pour `server.db` et les `erable.db`, rclone pour `erable.json` et `blobs/`, que Litestream ne voit pas), `deploy/litestream/{litestream.yml,restore.sh,test-restore.sh}` (test de bout en bout avec MinIO). Versions d'images figées (Litestream 0.3.13, rclone 1.68.2).
- **Traçabilité** : `pnpm e2e:trace --enforce M5` → 117 scénarios sur 117 ont un test (aucun manquant). La CI de PR passe de `--enforce M0` à `--enforce M5`. « Vert » reste à confirmer par l'exécution des e2e.
- **Non vérifié, à faire** : la restauration Litestream **n'a pas été exécutée** (ni Docker ni MinIO lancés ; télécharger des images et lancer des conteneurs n'a pas été demandé). La syntaxe de réplication par répertoire (`dir`/`pattern`/`recursive`/`watch`) de la configuration Litestream est à confirmer à la première exécution de `deploy/litestream/test-restore.sh`. Le critère « restauration testée une fois de bout en bout » reste donc ouvert.
- Message de commit : `docs: add the user guide, the Litestream backup guide and the translation review (M5-05)`

## Propositions

- **Nom des nouvelles pages et dossiers** : « Nouvelle page » / « Nouveau dossier » suivent la langue de l'interface alors que les noms des racines suivent celle du contenu. Un projet en allemand édité par un utilisateur français mélange donc les deux. Option : proposer ces noms dans la langue du projet (coût faible : clés `Roots`-like côté Core ou envoi de la langue du contenu au client).
- **Marqueur « à relire »** : si une revue par langue est souhaitée, un fichier `spec/i18n/to-review.json` (clés à relire par langue) rendrait la liste vérifiable par un test, au lieu de vivre dans `docs/i18n-review.md`.
