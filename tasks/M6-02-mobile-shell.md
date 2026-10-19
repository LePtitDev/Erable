# M6-02 — Coque mobile : barre supérieure, tiroir de l'explorateur, barre d'état

- **Statut** : review
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

- [x] `F18.R1`, `F18.R2`.
- [x] `E2E-18-01`, `E2E-18-09` verts.
- [x] Vitest : le tiroir se ferme à la navigation ; le layout du bureau n'est pas modifié.
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

- **Tiroir** : Radix Dialog (fond, focus piégé, Échap). Il est ouvert « pour » l'URL où on l'a ouvert (`drawerAt === href`) : toute navigation le ferme sans effet ni `setState` dans un effet. Le `layout` du bureau n'est lu ni écrit en mise en page étroite.
- **Dossiers dans le tiroir** : toucher un dossier ou une racine le **déplie** au lieu d'ouvrir sa vue (sinon le tiroir se refermait aussitôt) ; ajouté à F18.R1. Une page s'ouvre toujours.
- **Barre supérieure** : accueil, explorateur, nom tronqué, recherche en icône, présence (1 avatar), menu « Plus » (précédent, suivant, préférences · langue, thème, compte et déconnexion en mode serveur). Barre d'état : connexion, avertissements, lecture seule, mode.
- **Accueil** : la barre latérale devient une ligne d'en-tête ; Préférences / Aide / Déconnexion gardent leur icône, libellé masqué visuellement (nom accessible conservé). La zone de dépôt de dossier est masquée avec un pointeur grossier.
- **Écart relevé** : l'écran de connexion déborde à 412 px (sélecteur langue / thème) → M6-03.
- **Vérifications** : Vitest `ProjectShell.test.tsx` (7/7) et dossiers voisins, `playwright test --project mobile` (E2E-18-01, 18-09, 18-11), lint, typecheck, Prettier.
