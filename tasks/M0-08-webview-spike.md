# M0-08 — Spike WebKitGTK / WKWebView

- **Statut** : review
- **Jalon** : M0
- **Dépend de** : M0-07
- **Références** : §A-10 (risque à lever au jalon M0), §T-9.1, décision Q6

## Objectif

Lever au plus tôt le principal risque technique : le comportement des WebViews natifs utilisés par Photino sous Linux (WebKitGTK) et macOS (WKWebView).

## Périmètre

Prototype jetable (dossier `spikes/webview/` ou route de dev), testé sur les trois OS :

- CodeMirror 6 avec une décoration atomique (pastille) : saisie, curseur, suppression, IME (accents, compositions).
- Glisser-déposer HTML5 dans une liste arborescente (dont curseur « interdit »).
- Lecture vidéo MP4 avec avance rapide (requêtes `Range`).
- Collage d'image depuis le presse-papiers et dépôt de fichiers depuis l'explorateur de l'OS.
- Raccourcis `Ctrl+S`, `Ctrl+K`, `Ctrl+Espace` / `⌘+Maj+K` : interceptés par la page, pas par le WebView.
- Mesure : temps de démarrage, mémoire.

## Livrable

- Un compte rendu `docs/spikes/webview.md` : tableau OS × point testé (OK / contournement / bloquant), captures, recommandation **garder Photino** ou **basculer sur Electron**.
- Si un point est bloquant : s'arrêter et présenter les options à l'utilisateur avant toute autre tâche desktop.

## Critères d'acceptation

- [x] Les 6 points testés sur Windows ; Linux et macOS testés via CI (M0-10) ou machine disponible, sinon explicitement marqués « non vérifiés ». (Linux et macOS : **non vérifiés**.)
- [x] Recommandation argumentée.

## Points d'attention

- Le code du spike n'est pas destiné à rester : ne pas le peaufiner.

## Notes d'implémentation

- Compte rendu : **`docs/spikes/webview.md`** (tableau OS × point testé, capture, constats, recommandation). Code jetable : `spikes/webview/` (page Vite autonome, hôtes Photino.NET et PhotinoX réutilisant le vrai `ErableHost`, runner CDP `run-checks.mjs`, runner Playwright `webkit-checks.mjs`). Hors de `Erable.sln` ; vidéo et PNG de test générés par ffmpeg (non versionnés).
- **Windows : les 6 points passent**, avec Photino.NET 4.0.16 **et** PhotinoX 5.3.5 (même WebView2) : pastille atomique (curseur, suppression, annulation), IME simulé (touche morte, japonais), glisser-déposer avec cible interdite, vidéo H.264 avec saut (réponses 206) et lecture, collage d'une image du presse-papiers OS, dépôt de fichier, raccourcis (CDP et vraies frappes OS), fermeture propre. Démarrage 575–800 ms jusqu'à la page prête ; ~445 Mo d'ensemble de travail au total, dont ~70 Mo pour l'hôte.
- **Linux et macOS : non vérifiés** (aucune machine, WSL absent, branche non poussée). Indication WebKit via Playwright : éditeur, glisser-déposer, vidéo et raccourcis OK, avec un **écart** à retenir pour M1-10 : `dragend.dropEffect` vaut `all` dans WebKit (décider dans `drop`).
- Constats transverses : Photino remplace l'agent utilisateur (`Photino WebView`) ; H.264 sous Linux dépend de plugins GStreamer souvent absents (à documenter pour M5-03) ; les deux natifs Linux sont liés à `libwebkit2gtk-4.1`.
- **Aucun point bloquant** → pas d'arrêt. **Recommandation** : rester sur une architecture Photino, mais **basculer vers PhotinoX** dès qu'un premier test Linux ou macOS l'a confirmé (maintenu, .NET 10, migration ≈ 10 lignes dans `PhotinoShell.cs`). Décision à prendre par l'utilisateur, puis à reporter sous Q6.
- Le presse-papiers de l'utilisateur est modifié pendant `run-checks.mjs` (image de test) puis son texte est restauré.

**Vérification** : `node spikes/webview/run-checks.mjs …/Spike.Host.exe [--real-keys]` et `…/Spike.HostX.exe` (tous `ok: true`, `cleanExit: true`) · `node spikes/webview/webkit-checks.mjs`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
