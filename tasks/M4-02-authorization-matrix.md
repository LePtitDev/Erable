# M4-02 — Matrice d'autorisation générée

- **Statut** : todo
- **Jalon** : M4
- **Dépend de** : M4-01
- **Références** : §T-7 (Matrice d'autorisation), §T-15

## Objectif

Rendre impossible l'oubli d'autorisation sur un endpoint.

## Périmètre

- Test qui énumère **tous** les endpoints via `EndpointDataSource` et les appelle avec chaque profil : anonyme, lecteur, éditeur, administrateur, membre d'un autre projet.
- Tableau attendu déclaré (métadonnée d'endpoint ou fichier), comparé au résultat réel.
- Un endpoint sans métadonnée d'autorisation explicite fait échouer la suite.
- Le même test vérifie le mode desktop (jeton exigé partout sauf ressources statiques explicitement listées).

## Critères d'acceptation

- [ ] Ajouter un endpoint sans règle fait échouer le test (démontré par un test dédié).
- [ ] Matrice ajoutée à la définition de « terminé » pour tout nouvel endpoint.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
