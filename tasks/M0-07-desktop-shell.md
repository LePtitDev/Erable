# M0-07 — Shell Photino et sécurité loopback

- **Statut** : todo
- **Jalon** : M0
- **Dépend de** : M0-03
- **Références** : §A-2, §A-9 (serveur loopback), §A-10, F16.R1, E2E-16-02, §T-7 (sécurité du mode local), P-03

## Objectif

Ouvrir la SPA dans une fenêtre native avec le backend hébergé dans le même processus, sans exposer les projets aux autres processus ni aux sites web.

## Périmètre

- `Erable.Desktop` : démarre `ErableHost` sur `127.0.0.1`, port 0 (aléatoire), récupère le port effectif, ouvre une fenêtre Photino sur l'URL locale.
- Jeton de session aléatoire (≥ 128 bits) généré au lancement, transmis au WebView sans passer par une URL journalisée (en-tête ou script injecté), exigé sur chaque requête HTTP **et** sur la connexion WebSocket.
- Vérification de l'en-tête `Host` (exactement `127.0.0.1:<port>`) contre le DNS rebinding ; `Origin` vérifié pour les requêtes non GET.
- Impossible d'écouter sur `0.0.0.0` en mode desktop (garde-fou testé).
- Interface `INativeBridge` (sélecteur de dossier, ouvrir avec l'application système, révéler dans l'explorateur) côté backend ; la SPA y accède par l'API, jamais directement.
- Arrêt propre : fermer la fenêtre arrête Kestrel et libère le processus.
- **Trancher P-03** (persistance navigateur impossible avec un port aléatoire) : présenter les options à l'utilisateur et consigner la décision dans `docs/architecture.md`.

## Critères d'acceptation

- [ ] `E2E-16-02` (version test d'intégration) : requête sans jeton → 401 ; `Host` différent → refus ; écoute limitée à `127.0.0.1`.
- [ ] L'application s'ouvre sur Windows et affiche `/api/info` via la SPA.
- [ ] Aucun processus résiduel après fermeture (vérifié manuellement, automatisé en M0-11).

## Points d'attention

- Vérifier l'état de maintenance actuel de Photino.NET (dernière version, issues ouvertes WebView2 / WebKitGTK) et le signaler s'il est préoccupant : Electron reste le repli (§A-10, Q6).
- Le jeton ne doit jamais apparaître dans les journaux.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
