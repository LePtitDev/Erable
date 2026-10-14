# M0-08 — Spike WebKitGTK / WKWebView

- **Statut** : todo
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

- [ ] Les 6 points testés sur Windows ; Linux et macOS testés via CI (M0-10) ou machine disponible, sinon explicitement marqués « non vérifiés ».
- [ ] Recommandation argumentée.

## Points d'attention

- Le code du spike n'est pas destiné à rester : ne pas le peaufiner.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
