# M1-08 — Écran d'accueil et projets

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-05, M0-05, M0-06
- **Références** : F01, Mockup « Accueil — ouvrir un projet », §A-7.1

## Objectif

Créer, ouvrir et retrouver ses projets.

## Périmètre

- Mise en page du mockup : colonne gauche (logo, version, navigation Projets / Modèles de projet / Préférences / Aide et raccourcis, langue et thème), zone principale (« Bonjour, <nom> », trois actions, projets récents, zone de dépôt).
- **Nouveau projet** : nom + langue du contenu (F01.R4), emplacement (sélecteur natif via le pont desktop, saisie de chemin en mode web local).
- **Ouvrir un dossier…** (`Ctrl+O`) ; dossier sans manifeste → proposition de création, rien écrit sans confirmation (F01.R1).
- **Projets récents** : initiales, nom, chemin, mode (Local / Serveur), date relative ; introuvable → grisé + « Retirer » (F01.R2).
- Format trop récent → message clair (F01.R3) ; trop ancien → flux de migration (M2-09).
- « Modèles de projet » et « Se connecter à un serveur » : présents mais désactivés avec mention « bientôt » (M4-07), sauf décision contraire.

## Critères d'acceptation

- [ ] `E2E-01-01`, `E2E-01-02`, `E2E-01-03`, `E2E-01-04` verts.
- [ ] Écran utilisable au clavier, aucune violation axe-core grave.
- [ ] Textes traduits (fr complet, en/de/es).

## Points d'attention

- Le mockup affiche « Bonjour, Arthur » : en local, d'où vient le nom ? Proposer (nom du compte OS, préférence, ou rien).
- E2E-01-02 attend l'ouverture « sur la dernière page consultée » : à stocker par projet dans le profil (cf. P-03).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
