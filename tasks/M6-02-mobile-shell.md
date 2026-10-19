# M6-02 — Coque mobile : barre supérieure, tiroir de l'explorateur, barre d'état

- **Statut** : todo
- **Jalon** : M6
- **Dépend de** : M6-01
- **Références** : F18.R1, F18.R2, F02, F15, Mockup « Page — lecture »

## Objectif

Que la coque du projet tienne sur un téléphone : une barre supérieure d'une ligne, l'explorateur en tiroir, une barre d'état réduite.

## Périmètre

- `ProjectShell` en mise en page étroite : explorateur dans un tiroir (Radix Dialog : fond, focus piégé, Échap) fermé à l'ouverture et à chaque navigation ; l'état « ouvert » et la largeur mémorisés (`layout`) ne sont ni lus ni écrits.
- Barre supérieure : accueil, explorateur, nom du projet tronqué, recherche en icône, menu « Plus » (précédent / suivant, langue → préférences, thème, compte et déconnexion) ; présence réduite.
- Barre d'état : connexion et mode seulement.
- Accueil (`HomeScreen`) : barre latérale en en-tête compact.

## Critères d'acceptation

- [ ] `F18.R1`, `F18.R2`.
- [ ] `E2E-18-01`, `E2E-18-09` verts.
- [ ] Vitest : le tiroir se ferme à la navigation ; le layout du bureau n'est pas modifié.
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

_À remplir par l'agent._
