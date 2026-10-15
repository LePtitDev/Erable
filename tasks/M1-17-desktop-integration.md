# M1-17 — Intégration desktop native

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-08, M0-11
- **Références** : F16.R2–R3, E2E-16-03 à E2E-16-05, E2E-16-07, §T-10, P-04

## Objectif

Que l'application desktop se comporte comme une vraie application native.

## Périmètre

- Sélecteur de dossier natif (`Ctrl+O`), ouvrir avec l'application système, révéler dans l'explorateur.
- Mono-instance par projet : la seconde instance transmet sa demande à la première (IPC local) qui passe au premier plan (F16.R2, P-04).
- Fermeture de fenêtre : demande à la SPA s'il y a des modifications non enregistrées, affiche Enregistrer / Abandonner / Annuler, puis arrête proprement (checkpoint WAL, aucun processus résiduel) (F16.R3).
- Glisser un dossier sur l'accueil (E2E-16-07).
- Titre de fenêtre = projet courant ; icône d'application provisoire.
- Scénario de fumée desktop enrichi : créer un projet, une page, une référence, une image.

## Critères d'acceptation

- [ ] `E2E-16-03`, `E2E-16-04`, `E2E-16-05` verts sur Windows (CDP) ✅ ; self-test macOS/Linux enrichi ✅ (vérifié sous Windows seulement) ; **`E2E-16-07` non fait** : voir P-15.
- [ ] `E2E-16-01` (fumée complète hors recherche) vert sur **Windows** (CDP et `--self-test`) ; Linux et macOS non vérifiés (pas de machine, branche non poussée).

## Points d'attention

- Le chemin d'un dossier déposé n'est pas toujours accessible depuis le WebView : passer par le shell natif si nécessaire (cf. M0-08).

## Notes d'implémentation

- **Fonctions natives par l'API** (`NativeEndpoints`, invariant 3) : `POST /api/v1/native/pick-folder`, `PUT /native/window` (titre + modifications non enregistrées), `POST /native/window/close`, `GET /native/events` (SSE), `POST …/attachments/{id}/open|reveal`. `INativeBridge` gagne `IsAvailable`, `SetWindowTitle`, `BringToFront`, `AllowForeground`, `CloseWindow` ; en mode serveur tout répond 404 `native.unavailable` et la SPA masque ces actions (`useIsDesktop`, d'après `/api/info`).
- **Ouvrir / révéler une pièce jointe** (reporté de M1-14) : les blobs n'ont ni nom ni extension, le backend écrit une copie nommée dans `<temp>/erable-open/<projet>/<id>/<nom>` et l'ouvre ou la révèle. **Programmes et scripts refusés** (422 `attachment.executable`, liste dans `AttachmentFileName.IsExecutable`, Core) : un clic ne doit jamais exécuter un fichier reçu d'un tiers. « Afficher dans l'explorateur » reste permis.
- **`Ctrl+O`** : sur le desktop, le dialogue « Ouvrir un dossier » lance aussitôt le sélecteur natif (annuler ferme le dialogue) ; le champ chemin et un bouton « Parcourir… » restent. Le dossier choisi suit le même chemin que la saisie (inspection, création confirmée, format trop récent).
- **Fermeture (F16.R3)** : `WindowCoordinator`. La SPA signale ses modifications non enregistrées ; à la fermeture, sans modification la fenêtre se ferme aussitôt ; sinon elle reste ouverte et la SPA, prévenue par SSE, affiche « Enregistrer / Abandonner / Annuler » dans l'éditeur, puis confirme. **Sans SPA à l'écoute, la fenêtre se ferme** (rien ne peut demander ; les brouillons F05.R4 sont déjà enregistrés). Le flux SSE s'arrête avec l'application (jeton lié à `ApplicationStopping`), sans exception journalisée. L'arrêt ferme les projets : checkpoint WAL, verrou libéré.
- **Mono-instance (F16.R2, P-04 complété)** : `InstanceRegistry` écrit `<profil>/instances/<pid>-<port>.json` (port + jeton IPC de 256 bits, distinct du jeton de session, qui ne permet que `POST /ipc/activate`). `Erable.Desktop <dossier>` interroge d'abord les instances vivantes : celle qui a le projet ouvert passe au premier plan (`AllowSetForegroundWindow` par l'appelant puis `SetForegroundWindow`, Windows ; ailleurs `SetMinimized(false)` + bascule `TopMost`) et la seconde sort avec 0 **sans créer de fenêtre ni de backend**. Même chose depuis l'accueil d'une autre instance (erreur `project.locked` + mise au premier plan). Fichiers périmés (processus mort) supprimés au passage.
- **Ligne de commande** : `Erable.Desktop [dossier]` ouvre le projet au démarrage (fragment `&open=`, ouvert avant le premier rendu du routeur).
- **Titre de fenêtre** = « <projet> — Erable » (aussi `document.title` en mode serveur). **Icône provisoire** (feuille blanche sur le vert d'accent) : `Assets/erable.ico|png` (icône de l'exécutable et de la fenêtre) et `favicon.ico` de la SPA (dette M0-07 soldée).
- **Fumée enrichie** : `selfTestSteps` crée un projet (dossier temporaire fourni par le shell, supprimé ensuite), deux pages avec une référence, une image (PNG dessiné sur un canvas), puis vérifie l'affichage. `waitFor` sonde aussi toutes les 100 ms (le chargement d'une image ne modifie pas le DOM). Le budget de démarrage se calcule désormais à l'affichage de l'accueil (`ReportedAtMs` ajouté au rapport), plus à l'arrivée du rapport. `E2E-16-01` (CDP) fait le même parcours par l'interface : dialogue Nouveau projet, explorateur, palette `[[`, collage d'image.
- **Tests desktop isolés** : `ERABLE_PROFILE_DIR` remplace le profil applicatif (jamais celui de la personne qui lance les tests) ; avec `ERABLE_TEST_MODE=1`, `ERABLE_TEST_PICK_FOLDER` remplace le sélecteur natif (une dialogue OS n'est pas pilotable en CDP). Sans effet dans un usage normal.
- **Corrigé au passage** : `TestEndpointsTests` (M1-07) échouait en Release (`dotnet test -c Release`, 9 échecs) car les endpoints `/_test` y sont compilés en dehors : ces tests sont désormais ignorés en Release. Erreur de lint du spec e2e de M1-15 (`void freshProfile`) corrigée.
- **Non fait — `E2E-16-07` (glisser un dossier sur l'accueil)** : le WebView ne donne jamais le chemin absolu d'un dossier déposé, et Photino.NET n'expose ni événement de dépôt ni de navigation. PhotinoX (évalué en M0-08) expose `NavigationStarting` : en laissant le WebView « naviguer » vers le `file:///…` du dossier déposé, le shell intercepterait l'URL, annulerait la navigation et ouvrirait le projet. Décision structurante (Q6) → **P-15**, à trancher par l'utilisateur.
- **Non vérifié** : Linux et macOS (`BringToFront`, icône PNG, self-test enrichi).

**Vérification** : `dotnet test --solution Erable.sln` (449, Debug) et `-c Release` (449, 10 ignorés) · `dotnet format --verify-no-changes` · `pnpm -C src/web api:generate`, `typecheck`, `lint`, `format:check`, `test` (340), `exec playwright test --project chromium --project webkit` (108) · `node build/publish-desktop.mjs win-x64` puis `pnpm -C src/web e2e:desktop` (5/5 : E2E-16-01 à 05) et `Erable.Desktop --self-test` (code 0, accueil à 1,6 s) · `e2e:trace -- --enforce M1` : 47/48, seul E2E-16-07 manque.

**Commit** : `feat(desktop,server,web): native folder picker, single instance, clean shutdown and native attachment actions (M1-17)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
