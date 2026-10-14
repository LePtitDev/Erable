# M3-04 — Récemment consultées / modifiées

- **Statut** : todo
- **Jalon** : M3
- **Dépend de** : M3-01
- **Références** : F11, §A-7.4 (`user_activity`, `revisions`), Mockups « Page — lecture » (sous l'arbre) et « Recherche »

## Objectif

Retrouver ce sur quoi on travaillait.

## Périmètre

- Consultation comptée après 2 s d'affichage (F11.R1), enregistrée dans `user_activity` par utilisateur.
- `me/recent` : consultées (par utilisateur) et modifiées (communes, avec auteur et date, issues des révisions).
- Affichage : sous l'explorateur (type, nom, temps relatif) et dans l'écran Recherche (onglets Consultées / Modifiées).
- Accueil : « dernière page consultée » du projet (E2E-01-02) si ce n'est pas déjà fait.

## Critères d'acceptation

- [ ] `E2E-11-01`, `E2E-11-02` verts (`E2E-11-03` en M4).
- [ ] Le minuteur de 2 s est testé avec une horloge simulée, sans attente réelle.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
