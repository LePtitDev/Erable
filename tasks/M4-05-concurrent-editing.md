# M4-05 — Concurrence optimiste et fusion à trois voies

- **Statut** : review
- **Jalon** : M4
- **Dépend de** : M4-03, M3-05
- **Références** : §A-8.3, F14.R5, §T-4.1 (Fusion), §T-15, P-05, décision Q3

## Objectif

Deux personnes éditent la même page : **aucun texte n'est jamais perdu**.

## Périmètre

- Trancher P-05 (fusion côté serveur dans Core recommandée).
- Fusion à trois voies (base / la mienne / la leur) au niveau des lignes, affinée au niveau des mots dans un bloc modifié des deux côtés ; propriétés fusionnées champ par champ.
- `PUT content` avec révision de base : fusion propre → enregistrée, réponse indique « fusionné avec les modifications de X » ; conflit → `409` avec les blocs en conflit.
- Vue de résolution : les deux versions de chaque bloc, choix par bloc ou édition manuelle, puis enregistrement.
- Session expirée pendant l'édition : reconnexion sans perdre les modifications (E2E-14-09).

## Critères d'acceptation

- [x] Propriétés CsCheck : `merge(b,b,x)=x`, `merge(b,x,b)=x`, `merge(b,x,x)=x`, symétrie, sections disjointes → pas de conflit et les deux présentes, **aucun caractère perdu sans conflit signalé** (`ThreeWayMergePropertyTests`).
- [x] Test d'intégration : N écritures parallèles sur des pages différentes → aucune perte ; huit éditeurs d'une même page partis de la même révision → toutes les modifications survivent (`ConcurrentEditingApiTests`, `ConcurrentSaveStorageTests`).
- [x] `E2E-14-07`, `E2E-14-08`, `E2E-14-09` écrits (`f14-concurrency.spec.ts`) ; exécutés en fin de M4.

## Points d'attention

- Tâche critique (§T-15) : privilégier un algorithme simple et prouvé (diff3) plutôt qu'une heuristique maison ; le dire si une bibliothèque existante convient.

## Notes d'implémentation

**P-05 tranché : fusion côté serveur, dans Core** (recommandation retenue, `docs/architecture.md` §8.3 mis à jour). Une seule implémentation, testée par propriétés, valable pour tous les clients ; le client n'a plus de logique de fusion, seulement la vue de résolution.

**Algorithme** (`Erable.Core.Merge.ThreeWayMerge`) : diff3 classique. Bibliothèque : **DiffPlex** (déjà utilisé pour l'historique, MIT) fournit la différence à deux (Myers) ; la fusion à trois voies par-dessus est la nôtre, courte, et c'est elle que les propriétés du produit exigent (DiffPlex a bien un `ThreeWayDiffer`, non retenu : comportement aux bords non vérifié, et il n'offre pas le raffinement par mots). Les deux éditions sont calculées contre la base ; les places où elles ne se rencontrent pas sont reprises telles quelles ; celles où elles se rencontrent (lignes communes **ou contiguës**, comme git et diff3) forment un amas : un seul côté → ce côté ; même changement → une fois ; sinon **second passage mot à mot** (découpage lossless en mots / espaces / ponctuation) ; il ne reste de conflit que si les mêmes mots ont été changés différemment. Les textes sont découpés en lignes qui gardent leur fin de ligne : les blocs recollés redonnent le texte exactement (CRLF compris).

**Champs** (`FieldMerge`) : catégories = ensemble (ajouts et retraits des deux côtés se combinent, jamais de conflit) ; valeurs de propriétés champ par champ (un seul côté a changé → ce côté ; même valeur → elle ; sinon conflit de valeur).

**Protocole** : `PUT pages/{id}/content` + `If-Match` inchangé. Si la page a avancé, la fusion se fait **dans la transaction de l'enregistrement** (`ProjectService.MergeStaleSave`, base = instantané de la révision d'origine, « leur » = état vivant) : rien ne peut s'intercaler, et N écritures parallèles sont sérialisées par la porte d'écriture.
- sans conflit : enregistré comme nouvelle révision de l'auteur ; la réponse porte `merged { markdown, categories?, properties?, mergedWith[{id,name}] }` (le client affiche « Fusionné avec les modifications de Camille R. ») ;
- avec conflit : rien n'est écrit, **409** `page.revisionConflict` avec `current { revision, markdown, … }`, `blocks[]` (`stable` / `conflict { base, mine, theirs }`), `valueConflicts[]`, `mergedStructure` (catégories et valeurs telles qu'elles seraient, conflits en faveur de l'autre) et `mergedWith`. Une révision d'origine inconnue donne le 409 sans détails d'avant.
- L'événement `ElementChanged` de M4-03 part après la fusion ; l'éditeur d'une page est prévenu qu'un autre l'a enregistrée (« … a enregistré cette page pendant que vous la modifiez »).

**Interface** : `PageEditor` : l'enregistrement devient `persist` ; un conflit ouvre `ConflictResolution` (dialogue large : contexte, les deux versions de chaque passage, *Garder la mienne / la leur / les deux / Modifier à la main*, valeurs en conflit ; « Enregistrer » exige que tout soit tranché). Enregistrer = nouvel enregistrement contre la révision en conflit (qui peut lui-même être fusionné ou rouvrir la vue si quelqu'un a encore écrit) ; la version non retenue reste dans l'historique. « Revenir à l'éditeur » ne perd rien. Les messages qui survivent à l'écran (`useNoticeStore`, `Notices`) portent « Fusionné avec… » puisque l'éditeur se ferme après l'enregistrement.
**E2E-14-09** : une 401 pendant le travail ouvre `ReloginDialog` (M4-01) ; `apiFetch` rejoue la requête (JSON, pas les envois multipart) après la reconnexion ; le cache n'est **pas** réinitialisé quand c'est le même utilisateur (sinon l'éditeur serait démonté) ; un autre utilisateur recharge la page.

**Dettes**
- Tests non exécutés (consigne : fin de M4). Les propriétés CsCheck (`ThreeWayMergePropertyTests`) sont à surveiller au premier passage : générateurs écrits sans exécution.
- Après une fusion réussie l'éditeur se ferme : le texte fusionné n'est pas réinjecté dans l'éditeur (inutile aujourd'hui, nécessaire si un enregistrement sans fermeture apparaît).
- Un conflit sur le titre (renommage) n'existe pas : le dernier gagne, l'historique garde l'autre.

**Message de commit proposé** : `feat(core,server,web): add the three-way merge of concurrent saves and the resolution view (M4-05)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
