# M1-08 — Écran d'accueil et projets

- **Statut** : review
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

- [x] `E2E-01-01`, `E2E-01-02`, `E2E-01-03`, `E2E-01-04` verts.
- [x] Écran utilisable au clavier, aucune violation axe-core grave.
- [x] Textes traduits (fr complet, en/de/es).

## Points d'attention

- Le mockup affiche « Bonjour, Arthur » : en local, d'où vient le nom ? Proposer (nom du compte OS, préférence, ou rien).
- E2E-01-02 attend l'ouverture « sur la dernière page consultée » : à stocker par projet dans le profil (cf. P-03).

## Notes d'implémentation

- **Écran** (`src/features/home/`) : mise en page du mockup (colonne : marque, version et mode, navigation Projets / Modèles de projet (désactivé, « bientôt ») / Préférences (dialogue) / Aide et raccourcis (dialogue) ; langue et thème en bas ; zone principale : « Bonjour, <nom> », trois actions, projets récents filtrables, zone de dépôt). Le thème garde **trois** choix (Clair / Sombre / Système, F15.R2) là où le mockup n'en montre que deux.
- **« Bonjour, <nom> »** (point d'attention) : `GET /api/v1/me` → nom du compte du système en local (`Environment.UserName`, majuscule initiale) ; le compte serveur en M4. `me` donne aussi `defaultProjectsFolder` (desktop : `Documents/Erable` ; serveur : `<data>/projects`).
- **Nouveau projet** : nom, langue du contenu (figée, F01.R4, aide affichée), emplacement (dossier parent, prérempli) ; le dossier final `<emplacement>/<nom nettoyé>` est affiché avant création. Le sélecteur natif arrive avec M1-17 (pont desktop).
- **Ouvrir un dossier…** (`Ctrl+O`, bouton) : chemin saisi → `projects/inspect` (aucune écriture) → projet : ouverture ; dossier sans manifeste : dialogue « Créer un projet dans ce dossier » (nom proposé = nom du dossier), **rien n'est écrit sans confirmation** (F01.R1) ; format trop récent : message avec la version, projet intact (F01.R3) ; introuvable / manifeste illisible : message. Format plus ancien : ouvert (migration automatique) ; la confirmation arrive en M2-09.
- **Récents** : initiales, nom, chemin, mode « Local », date relative ; un dossier absent ou devenu illisible est grisé, avec son statut (« Introuvable »…) et « Retirer » (F01.R2) ; toute la ligne d'un projet disponible est cliquable (un seul bouton accessible, le nom). L'ouverture reprend sur `lastElementId` (E2E-01-02).
- **Primitives partagées** (`src/shared/ui/`) : `Dialog` (Radix : focus piégé, Échap, retour du focus, base d'E2E-17-03), `Button`, styles de champs, `Logo` provisoire. Token `--color-overlay` ajouté (clair et sombre).
- **Erreurs** : `errorMessage()` traduit le `code` métier (`errors.<code>`, toutes les erreurs connues du backend traduites) et n'affiche jamais de détail technique.
- **Dépendances** : `lucide-react@1.47.0` (ISC, maintenu, icônes importées une à une donc tree-shakées) ; `@axe-core/playwright@4.13.0` (dev, MPL-2.0) pour le critère axe. Versions de plus de deux semaines.
- **Client API** : `openapi-fetch` fait passer les réponses par `Readable<T>`, type mappé profond qui **casse le type marqué `ElementId`** (il devient un objet). `unwrap()` renvoie désormais `Rebrand<T>`, qui rétablit `ElementId`. À garder en tête pour toute réponse typée à la main.
- **Routeur** : routes typées `/`, `/p/$projectId`, `/p/$projectId/e/$elementId` ; les ID d'URL sont validés à l'entrée (`parseElementId`) ; `createAppRouter(history)` pour les tests (`renderApp(path)`). Les écrans de projet sont des emplacements vides, remplis par M1-09.
- **Tests** : 6 tests de composants (`HomeScreen.test.tsx` : salutation, récents, introuvable + Retirer, création, format trop récent, dossier sans manifeste) ; e2e `f01-home.spec.ts` ; `a11y.spec.ts` (axe, clair et sombre). `HelloPage` de M0 supprimée, test de fumée et self-test desktop adaptés. Point d'appui `POST /_test/close` ajouté (fermer une copie pour supprimer ou modifier son dossier).
- **État des scénarios** : `E2E-01-03` et `E2E-01-04` verts (Chromium + WebKit). `E2E-01-01` (racines de l'explorateur) et `E2E-01-02` (page affichée) dépendent de l'explorateur et de la vue de page : verts depuis M1-10 (Chromium + WebKit).

**Vérification** : `pnpm typecheck`, `lint`, `format:check`, `test` (253), `api:check` · `dotnet test --solution Erable.sln` (404) · `playwright test f01-home a11y m0-skeleton`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
