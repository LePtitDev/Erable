# M1-10 — Explorateur (arbre)

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-09
- **Références** : F02, F17 (E2E-17-01), §A-3.1 (Arbre), Mockup (panneau Explorateur), §T-11.1

## Objectif

Un explorateur de type IDE : rapide sur 10 000 éléments, au clavier comme à la souris.

## Périmètre

- Composant maison virtualisé (`@tanstack/react-virtual`), rôle ARIA `tree` / `treeitem`, `aria-expanded`, `aria-level`.
- Racines avec compteurs (F02.R1, E2E-02-08), badges de type, indicateur discret de doublon (F02.R6).
- Créer dossier / page (menu contextuel et boutons), renommer (`F2`, double-clic), réordonner et déplacer par glisser-déposer avec dépôt refusé visible (F02.R2–R3).
- Navigation clavier : ↑ ↓ → ← Entrée F2, recherche de saisie (type-ahead).
- État déplié mémorisé par utilisateur et par projet (F02.R5, cf. P-03).
- Mises à jour optimistes avec retour arrière en cas d'erreur API.
- Section « Récemment consultées » sous l'arbre : emplacement prévu, alimenté en M3-04.

## Critères d'acceptation

- [ ] `E2E-02-01` à `E2E-02-08` et `E2E-17-01` verts.
- [x] Défilement fluide et ouverture d'un nœud < 50 ms sur un arbre de 10 000 éléments (mesure locale, budget formel en M3-06).
- [x] Tests de composant par rôle et libellé.

## Points d'attention

- Le glisser-déposer HTML5 est la zone de risque WebKit (M0-08) : réutiliser les conclusions du spike.
- Prévoir l'alternative clavier au glisser-déposer (couper / coller ou « Déplacer vers… »).

## Notes d'implémentation

- **Composant maison** (`src/features/explorer/`) : `Explorer.tsx` (panneau : en-tête avec Nouvelle page / Nouveau dossier / Tout replier, filtre, arbre, « Récemment consultées » en emplacement vide pour M3-04), `treeModel.ts` (fonctions pures), `useTreeMutations.ts`.
- **Règles côté client** : `checkMove` reprend `ElementTree.CheckParent` de Core (même racine, page sous page jamais sous sa descendante, dossier sous racine ou dossier, rien sous catégorie / propriété / type de lien) pour le **retour immédiat** du glisser-déposer ; le serveur reste l'autorité (un refus serveur annule la mise à jour optimiste et s'affiche). Testé par exemples et par un test de propriétés (fast-check : toute suite de déplacements acceptés garde un parent unique, aucune boucle, chaque élément sous sa racine).
- **ARIA** : `role="tree"` focalisable avec `aria-activedescendant` (l'élément actif peut ne pas être monté quand l'arbre est virtualisé), lignes `treeitem` à plat avec `aria-level`, `aria-posinset`, `aria-setsize`, `aria-expanded`, `aria-selected` (élément affiché). Doublon de nom (F02.R6) : icône discrète et `aria-describedby` vers un texte explicatif.
- **Clavier** (E2E-17-01) : ↑ ↓ Début Fin, → déplie puis entre, ← replie puis remonte au parent, Entrée ouvre, F2 renomme, recherche par saisie (préfixe tapé dans la demi-seconde, sans accents). **Alternative au glisser-déposer** : Ctrl+X marque l'élément, Ctrl+V le déplace dans la ligne active (Échap annule), aussi dans le menu contextuel (« Déplacer… » / « Déplacer ici »).
- **Glisser-déposer** HTML5 : haut / milieu / bas de la ligne = avant / dedans / après ; dépôt refusé = pas de `preventDefault` (curseur interdit du navigateur) et ligne teintée en rouge. Vérifié dans Chromium **et** WebKit par Playwright (E2E-02-03 à 02-06).
- **Création** : ligne de saisie en place sous le conteneur actif (ou son plus proche ancêtre capable de contenir ce type) ; une page créée s'ouvre avec `?edit=true` (consommé par l'éditeur, M1-12). Renommage en place (F2, double-clic, menu).
- **Mises à jour optimistes** (renommer, déplacer) avec retour arrière sur erreur ; après chaque écriture, invalidation de tout ce qui montre noms et chemins (`queryKeys.projectContent`).
- **État déplié** (F02.R5) : document `tree-<projectId>` du profil (`me/ui-state`), racines dépliées par défaut ; l'élément affiché est révélé (ancêtres dépliés). **Correctif** sur `useUiState` : l'écriture différée est envoyée tout de suite (`fetch` `keepalive`) au `pagehide` et au démontage, sinon un rechargement immédiat la perdait (E2E-02-07).
- **Virtualisation** : `@tanstack/react-virtual@3.14.13` (MIT, déjà prévu par §A-3.1), activée au-delà de 200 lignes visibles ; en dessous, toutes les lignes sont rendues (tests et lecteurs d'écran plus simples). Mesure indicative : indexer 10 000 éléments et calculer 5 101 lignes visibles < 50 ms (test unitaire) ; budget formel en M3-06.
- **Projet de référence** : « Point à challenger » levé en début de tâche — E2E-02-04 et les compteurs supposent des catégories ; les catégories, propriétés et types de liens de features §2.1 sont ajoutés comme **éléments nus** de l'arbre (aucune donnée métier : M2). Le domaine (M1-02) n'autorise pas une catégorie sous une catégorie : l'héritage (Entité › Personnage › PNJ) est un attribut de la catégorie (M2), elles sont donc **à plat** sous la racine en M1 → **P-14** ouvert. `ProjectBuilder` accepte une clé distincte du nom (deux « Région », deux dossiers « Personnages »). Projet régénéré.
- **Tests** : 10 tests de composants (racines et compteurs, flèches, Entrée, F2, création en place, refus vers une autre racine, couper-coller, recherche par saisie, révélation, filtre sans accents), 16 tests du modèle ; e2e `E2E-02-01`, `02-03` à `02-08`, `E2E-17-01`, axe. Mocks d'un projet factorisés (`src/test/projectMocks.ts`). Le dossier par défaut des nouveaux projets est propre à chaque test en mode test (deux navigateurs créaient « Atlas » au même endroit).
- **P-13** tranché comme recommandé : E2E-17-02 commence par Ctrl+K, rattaché à M3 (features §F17 corrigé).
- **En attente** : `E2E-02-02` vérifie le nom affiché dans la page d'Ilsa : vert avec M1-11.

**Vérification** : `pnpm typecheck`, `lint`, `format:check`, `test` (284) · `dotnet test --solution Erable.sln` (412) · `playwright test f01-home f02-tree f17-a11y a11y` (Chromium + WebKit ; tout vert sauf E2E-02-02, attendu).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
