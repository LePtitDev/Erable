# M1-09 — Coque de l'application et routage

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-08
- **Références** : §A-3.1, F03.R3, Mockup « Page — lecture » (structure générale), règles « mobile plus tard » §A-3.1

## Objectif

Mettre en place la structure commune à tous les écrans de projet.

## Périmètre

- Grille : barre supérieure (nom du projet, recherche `Ctrl+K` — champ inactif jusqu'à M3-02, avatars de présence — vides jusqu'à M4-04, sélecteur de langue), panneau Explorateur repliable, zone centrale, panneau latéral droit repliable, barre d'état (connexion, compteurs « N pages · N catégories… », mode Lecture / Édition).
- Routes typées TanStack Router : `/`, `/p/:projectId`, `/p/:projectId/e/:elementId` (le type d'élément choisit la vue), ancre `#titre`.
- Page « Élément introuvable » (`E2E-03-03`) et page d'erreur générique sans détail technique.
- Fil d'Ariane réutilisable.
- Client TanStack Query configuré (clés de cache par ressource, invalidation centralisée prête pour SignalR).
- Panneaux redimensionnables, état mémorisé (cf. P-03).

## Critères d'acceptation

- [x] `E2E-03-03` vert.
- [x] Primitive de boîte de dialogue (Radix) partagée : `E2E-17-03` vert (focus piégé, Échap, retour du focus).
- [x] Panneaux repliables au clavier ; cibles cliquables ≥ 32 px.
- [x] Aucune couleur ni chaîne en dur.

## Points d'attention

- Ne pas anticiper les vues Catégorie / Propriété : un emplacement générique suffit.

## Notes d'implémentation

- **Coque** (`src/features/project/ProjectShell.tsx`) : grille barre supérieure / [explorateur | séparateur | zone centrale] / barre d'état, reprise du mockup « Page — lecture ».
  - Barre supérieure : accueil, bouton afficher/masquer l'explorateur (`aria-expanded`, `aria-controls`), nom du projet, précédent / suivant (historique), champ de recherche affiché **inactif** jusqu'à M3-02, emplacement de présence vide (M4-04), langue (ouvre les préférences), bascule clair/sombre, avatar (initiales de `me`). Titre du document = projet.
  - Barre d'état : « Projet local », compteurs de `tree.counts` (pluriels), position du curseur (renseignée par l'éditeur, M1-12), mode Lecture / Édition (`shellStore`, Zustand).
  - Explorateur : panneau repliable et **redimensionnable** (`Splitter` : glisser, ou ← → Début Fin une fois focalisé, rôle `separator` avec `aria-valuenow`). L'arbre arrive en M1-10. Sous 720 px, le panneau passe en surcouche (préparation « mobile plus tard »).
  - Le panneau latéral droit appartient à la vue de page (il porte le sommaire et les pièces jointes) ; son état replié est dans la même mise en page (`sidePanelOpen`), utilisé par M1-11.
- **État d'interface** : nouveau `GET/PUT /api/v1/me/ui-state/{clé}` (documents JSON ≤ 2 Mo dans le profil, clés `[A-Za-z0-9._-]{1,100}`, testé) et hook `useUiState(key, défaut)` (mise à jour immédiate du cache, écriture différée de 400 ms). Rien dans `localStorage` (décision Q7). Il servira aussi à l'état déplié de l'arbre (M1-10) et aux brouillons (M1-12).
- **Routes** : `/`, `/p/$projectId` (redirige vers le dernier élément consulté, sinon la racine Pages), `/p/$projectId/e/$elementId` : le type de l'élément choisit la vue (page → M1-11 ; racine et dossier → `FolderView` ; catégorie, propriété, type de lien, pièce jointe → emplacement générique). L'ancre `#titre` passe par le routeur (M1-11). Chaque élément affiché est mémorisé comme « dernier élément » du projet (E2E-01-02).
- **Introuvable** (E2E-03-03) : ID mal formé ou inconnu → « Élément introuvable » dans la coque (la recherche reste visible), lien vers le projet et l'accueil, aucun détail technique ; page d'erreur générique (`ErrorScreen`) pour un échec de rendu. **Correctif** : TanStack Query réessayait les 404 trois fois (plusieurs secondes de « Chargement… ») ; `createQueryClient()` ne réessaie plus les 4xx.
- **Composants partagés** : `Breadcrumb` (liens vers chaque ancêtre), `KindBadge` (lettres P, C, Pr… traduites, couleurs des tokens), `IdChip` (affichage par groupes de 4, copie au format canonique, confirmation en `role="status"`), `ElementHeader` (fil d'Ariane, titre, ID, « Modifiée il y a … par … · créée le … », « moi » pour l'utilisateur courant).
- **Dialogue** (E2E-17-03) : en mode contrôlé, Radix n'a pas de déclencheur où rendre le focus ; `Dialog` mémorise l'élément focalisé à l'ouverture et le refocalise à la fermeture.
- **Écart** : les onglets « éléments ouverts » du mockup ne sont dans aucune tâche ; non faits, consignés en proposition.
- **Tests** : 6 tests de composants de la coque (compteurs et mode, dossier et fil d'Ariane, ID inconnu, ID mal formé, repli de l'explorateur avec largeur restaurée, redimensionnement au clavier sauvegardé) ; e2e `E2E-03-03`, `E2E-17-03`, axe sur la coque.

**Vérification** : `pnpm typecheck`, `lint`, `format:check`, `test` (259), `api:check` · `dotnet test --solution Erable.sln` · `playwright test f03-ids f17-a11y a11y` (Chromium + WebKit).

## Propositions

- Onglets « éléments ouverts » du mockup (Ilsa Varenne · PNJ · Niveau) : non prévus par les tâches ; à planifier (après M3, ils dépendent des vues Catégorie / Propriété) si l'on veut l'ergonomie IDE complète.
