# M1-10 — Explorateur (arbre)

- **Statut** : todo
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
- [ ] Défilement fluide et ouverture d'un nœud < 50 ms sur un arbre de 10 000 éléments (mesure locale, budget formel en M3-06).
- [ ] Tests de composant par rôle et libellé.

## Points d'attention

- Le glisser-déposer HTML5 est la zone de risque WebKit (M0-08) : réutiliser les conclusions du spike.
- Prévoir l'alternative clavier au glisser-déposer (couper / coller ou « Déplacer vers… »).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
