# M1-09 — Coque de l'application et routage

- **Statut** : todo
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

- [ ] `E2E-03-03` vert.
- [ ] Primitive de boîte de dialogue (Radix) partagée : `E2E-17-03` vert (focus piégé, Échap, retour du focus).
- [ ] Panneaux repliables au clavier ; cibles cliquables ≥ 32 px.
- [ ] Aucune couleur ni chaîne en dur.

## Points d'attention

- Ne pas anticiper les vues Catégorie / Propriété : un emplacement générique suffit.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
