# M2-04 — Écran Catégorie

- **Statut** : review
- **Jalon** : M2
- **Dépend de** : M2-03, M1-11
- **Références** : F07, Mockup « Catégorie — définition »

## Objectif

Consulter et modifier une catégorie conformément au mockup.

## Périmètre

- En-tête : fil d'Ariane, badge C, nom, « Catégorie · modifiée le … par … », ID copiable, « Modifier ».
- Description (texte simple), chaîne d'héritage cliquable, sous-catégories.
- Tableau des propriétés : propriété, type, valeur par défaut, redéfinition, règles, source (cette catégorie ou héritée) ; « Ajouter ».
- Choix du parent avec refus des cycles et message de boucle (E2E-07-03).
- « Pages de la catégorie · N », case « Inclure les sous-catégories », « Voir les N pages dans la recherche » (lien vers M3-03 ; désactivé jusque-là).
- Création d'une catégorie depuis l'arbre.

## Critères d'acceptation

- [x] `E2E-07-01`, `E2E-07-02`, `E2E-07-03`, `E2E-07-07` (partie liste ; partie recherche en M3-03) verts. `E2E-07-08` déplacé en M2-06 (il lit la valeur sur une page).
- [x] Clavier et axe-core.

## Notes d'implémentation

- **Écran** (`features/category/CategoryView.tsx`) conforme au mockup : grand badge « C » (nouvelle option `kind` de `ElementHeader`, taille `lg` de `KindBadge`), cartes Description et Héritage (chaîne cliquable, sous-catégories), tableau des propriétés (propriété, type, valeur par défaut, « redéfinie », règles, source), « Pages de la catégorie · N » avec « Inclure les sous-catégories » (coché par défaut, comme le mockup) et filtre. « Voir les N pages dans la recherche » est affiché mais `aria-disabled` jusqu'à M3-03.
- **Mode édition** (« Modifier ») : description, catégorie parente, valeurs par défaut (éditeurs par type), « Redéfinir » une valeur héritée, retrait, « Ajouter » (boîte de dialogue : propriété + valeur). « Enregistrer » envoie `PUT categories/{id}` puis une requête par valeur modifiée : **pas atomique** (dette mineure ; un endpoint groupé est possible si besoin).
- **Boucle** (E2E-07-03) : détectée instantanément côté client (`loopOf`, même forme que l'extension `loop` du backend) ; le choix est refusé et le parent précédent reste. Le backend refuse de toute façon (M2-03).
- **Création depuis l'arbre** : « Nouvelle catégorie » dans le menu contextuel de la racine Catégories et de ses dossiers ; la catégorie s'ouvre en édition (`?edit=true`).
- **Socle réutilisé par M2-05/M2-06** : `shared/api/structure.ts` (requêtes, mutations, clés sous `projectContent`), `features/properties/definitions.ts` (lecture du JSON libre), `format.ts` (valeurs, formes courtes des règles « ≥ 1 », messages « Doit être inférieur ou égal à 60 », forme brute « less_or_equal 60 »), `ValueEditor.tsx` (interrupteur, liste, champ numérique filtré — les lettres sont refusées à la frappe —, texte, date, sélecteur de page filtré par `in_category`, listes).
- **OpenAPI** : `ValueOrigin` et le JSON libre sont typés `unknown` côté TS et relus par `src/shared/properties`.
- **Textes** : 4 langues (`category.*`, `property.*`, `errors.category.*`, `errors.property.*`).
- Vérification : `pnpm -C src/web test` (572 verts), `lint`, `typecheck`, `format:check` ; `pnpm -C src/web e2e:pr` (86 verts, Chromium + WebKit) dont `f07-categories.spec.ts`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
