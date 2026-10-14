# M0-11 — Artefacts desktop et test de fumée

- **Statut** : review
- **Jalon** : M0
- **Dépend de** : M0-07, M0-10
- **Références** : §A-10 (Packaging), §A-12 (M0), §T-10, E2E-16-01, E2E-16-02, P-06

## Objectif

Produire un binaire desktop autonome par OS et vérifier automatiquement qu'il démarre et fonctionne.

## Périmètre

- `dotnet publish` autonome par RID (`win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`) avec la SPA embarquée.
- Trancher P-06 : en M0, archives autonomes ; installeur Velopack en M5-03.
- **Windows** : lancement avec `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=…`, Playwright `connectOverCDP`.
- **macOS / Linux** : option `--self-test` : l'app ouvre un projet temporaire, la SPA déroule un scénario scripté, rapporte le résultat au backend, le processus sort avec 0 ou 1 (Linux sous `xvfb`).
- Job CI qui exécute ces fumées sur les artefacts publiés.
- Vérification de fin : plus aucun processus Erable après fermeture.

## Critères d'acceptation

- [ ] `E2E-16-01` (version M0 : démarrage < 3 s et affichage de l'accueil « hello ») vert sur les 3 OS. **Vert sous Windows ; Linux et macOS en attente du premier passage CI.**
- [x] `E2E-16-02` vert sur le binaire packagé. (Windows ; Linux et macOS via `--self-test` en CI.)
- [x] `--self-test` est absent ou inopérant hors des builds de test, ou ne permet rien de plus qu'un démarrage normal (à justifier). (Justifié dans les notes.)

## Points d'attention

- Le scénario de fumée complet d'E2E-16-01 (projet, page, référence, image, recherche) s'enrichira au fil des jalons : prévoir un scénario extensible.

## Notes d'implémentation

- **P-06 tranché** : en M0, archives autonomes (`Erable-win-x64.zip`, `Erable-<rid>.tar.gz`) ; Velopack en M5-03.
- **Publication** : `node build/publish-desktop.mjs <rid>` — build de la SPA, `dotnet publish` Release autonome **ReadyToRun** (démarrage plus rapide), contrôle de la présence de `wwwroot/index.html`, archive (bsdtar système sous Windows pour le zip, `tar.gz` ailleurs). Windows : 111 Mo décompressé, 51 Mo zippé, 365 fichiers.
- **`--self-test`** (shell `SelfTest.cs` + SPA `src/selftest/`) :
  - le shell vérifie lui-même E2E-16-02 **pendant** le chargement de la fenêtre : écoute sur `127.0.0.1` uniquement et refus via les autres interfaces (timeout de connexion 500 ms), `/api/info` sans jeton → 401, `Host` étranger → 400 ;
  - la SPA (fragment `#session=…&selftest=1`, module chargé à la demande, absent du bundle initial) exécute une liste d'étapes **extensible** (`selfTestSteps` : API authentifiée par le cookie, écran d'accueil visible) et poste `POST /api/self-test/report` ; le shell ferme la fenêtre, imprime le rapport JSON (et l'écrit avec `--self-test-report <fichier>`), sort en 0/1. Budget E2E-16-01 : accueil < 3 s après le lancement du processus.
  - **Justification de sa présence en Release** : la fumée doit tourner sur l'artefact livré. `--self-test` ne permet rien de plus qu'un démarrage normal : même jeton, même vérification `Host`/`Origin`, et `/api/self-test/report` (404 hors self-test, 401 sans jeton) ne fait que décider du code de sortie. Tests : `SelfTestTests` (4).
- **Windows** : `pnpm -C src/web e2e:desktop` (config `playwright.desktop.config.ts`, `e2e/desktop/`) lance le binaire publié avec `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port`, s'y attache par `connectOverCDP`, exécute **E2E-16-01** (accueil visible, < 3 s, fragment effacé) et **E2E-16-02** (401 sans jeton, 400 `Host` étranger, injoignable via les autres interfaces, SPA authentifiée par cookie invisible de JS), ferme la fenêtre comme un utilisateur (`WM_CLOSE`) et vérifie qu'aucun processus `Erable.Desktop` ne subsiste.
- **Vérifié localement (Windows)** : `e2e:desktop` 2/2 vert sur le binaire publié (≈ 1 s jusqu'à l'accueil) ; `--self-test` sur l'archive décompressée → code 0, accueil à 1,5 s à froid (0,7–0,8 s à chaud) ; aucun processus résiduel.
- Traçabilité : `e2e:trace --enforce M0` passe désormais (2/2) et est appliqué dans le job *checks*.
- **Job CI `desktop`** (dans `pr.yml`) : win-x64 (CDP + self-test), linux-x64 (`xvfb-run`, `libwebkit2gtk-4.1-0`, plugins GStreamer pour H.264), osx-arm64 (self-test), osx-x64 publié sans fumée (pas de runner Intel). Archives publiées en artefacts (14 jours). **Non exécuté** (branche non poussée) : Linux et macOS restent **non vérifiés**.
- **Risque Linux identifié** : Ubuntu 24.04 restreint les espaces de noms utilisateur non privilégiés dont a besoin le bac à sable bubblewrap de WebKitGTK ; le job CI assouplit `kernel.apparmor_restrict_unprivileged_userns` plutôt que de désactiver le bac à sable. Pour les utilisateurs finaux, à traiter avec le paquet Linux (M5-03 : profil AppArmor ou AppImage).
- Dette : `E2E-16-01` (M0) ne couvre que « démarrer et afficher l'accueil » ; le parcours complet (projet, page, référence, image, recherche) s'ajoute dans `selfTestSteps` et dans `desktop.spec.ts` au fil de M1–M3.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
