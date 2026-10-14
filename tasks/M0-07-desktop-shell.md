# M0-07 — Shell Photino et sécurité loopback

- **Statut** : review
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

- [x] `E2E-16-02` (version test d'intégration) : requête sans jeton → 401 ; `Host` différent → refus ; écoute limitée à `127.0.0.1`.
- [x] L'application s'ouvre sur Windows et affiche `/api/info` via la SPA.
- [x] Aucun processus résiduel après fermeture (vérifié manuellement, automatisé en M0-11).

## Points d'attention

- Vérifier l'état de maintenance actuel de Photino.NET (dernière version, issues ouvertes WebView2 / WebKitGTK) et le signaler s'il est préoccupant : Electron reste le repli (§A-10, Q6).
- Le jeton ne doit jamais apparaître dans les journaux.

## Notes d'implémentation

- **Point à challenger tranché avec l'utilisateur — maintenance de Photino.NET** : dernière version 4.0.16 et dernier commit de code le 23/01/2025, pas de cible .NET 10 (fonctionne néanmoins sous .NET 10), issue « Is this project dead? » ouverte. Forks actifs : PhotinoX (Apache-2.0, NuGet 5.3.5, un mainteneur) et InfiniFrame. **Décision : rester sur Photino.NET 4.0.16**, tout isoler dans `PhotinoShell.cs` (seul fichier qui référence Photino) et comparer avec PhotinoX dans le spike M0-08. Consigné sous Q6 dans `docs/architecture.md`.
- **P-03 tranché (recommandation retenue)** : aucun état durable dans le navigateur ; décision **Q7** ajoutée à §A-13, §A-3.1 et §A-9 mis à jour.
- **Transmission du jeton (écart assumé avec « en-tête ou script injecté »)** : Photino ne sait ni ajouter d'en-tête ni injecter de script avant le chargement. Le shell ouvre `http://127.0.0.1:<port>/#session=<jeton>` : un **fragment** n'est jamais envoyé au serveur, donc jamais journalisé. La SPA l'efface (`history.replaceState`) puis l'échange via `POST /api/session` (en-tête `X-Erable-Session`) contre un cookie `HttpOnly; SameSite=Strict; Path=/`. Ce cookie couvre aussi la connexion WebSocket et les `<img>` sans exposer le jeton à JavaScript. Mécanisme indépendant du shell (fonctionnerait tel quel sous Electron).
- **`DesktopSecurityMiddleware`** (mode Desktop uniquement), en tête du pipeline :
  1. `Host` exactement `127.0.0.1:<port local>`, sinon 400 (ni `localhost`, ni le port omis) ;
  2. méthode non GET/HEAD avec un `Origin` différent de `http://127.0.0.1:<port>` → 403 ;
  3. `/api/**`, `/hubs/**`, `/_test/**` exigent le jeton (cookie ou en-tête, comparaison à temps constant) → sinon 401 `ProblemDetails`.
  La coquille statique de la SPA reste servie sans jeton (aucune donnée utilisateur) : **écart** avec « exigé sur chaque requête HTTP », documenté en §A-9.
- **Garde-fous** dans `ErableHost.Build` : en mode Desktop, adresse ≠ `127.0.0.1` ou jeton absent / < 32 caractères → `InvalidOperationException`. Jeton du shell : 256 bits (`RandomNumberGenerator`).
- **Tests d'intégration E2E-16-02** (`DesktopSecurityTests`, vrai Kestrel sur loopback) : sans jeton → 401 ; mauvais jeton sur `/api`, `/api/openapi`, `/hubs`, `/_test` → 401 ; `Host` étranger (y compris `localhost` et port omis) → 400, coquille comprise ; `Origin` étranger → 403 ; cookie `HttpOnly`/`SameSite=Strict` qui authentifie seul ; aucune connexion acceptée via les autres interfaces réseau ; adresses `0.0.0.0`, `::`, `192.168.1.10` refusées ; jeton faible refusé ; **le jeton n'apparaît dans aucun journal** (journalisation capturée au niveau Trace). `/api/session` répond 404 en mode serveur.
- `INativeBridge` (sélecteur de dossier, ouvrir avec l'application système, révéler dans l'explorateur) : interface côté serveur, implémentation `PhotinoShell`, `UnavailableNativeBridge` par défaut en mode serveur. Pas encore d'endpoint (M1-17).
- Shell : fenêtre 1280×820 (min. 720×480), menu contextuel désactivé, DevTools en Debug ou avec `ERABLE_DEVTOOLS=1`, `SetLogVerbosity(0)` (Photino journalise sinon les URL chargées, fragment compris). Profil applicatif : `%APPDATA%\Erable`, `~/Library/Application Support/Erable`, `$XDG_CONFIG_HOME/erable`.
- **Vérifié sur Windows** (binaire de Debug, WebView2 piloté en CDP via `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`) : écoute sur `127.0.0.1:<port aléatoire>` uniquement ; fragment retiré de l'URL ; cookie invisible de JS ; la SPA affiche « Version 0.1.0 · API v1 · Application locale » moins d'une seconde après le lancement ; thème Système sombre suivant Windows. **Fermeture** (`CloseMainWindow`) : processus terminé en 78 ms, plus aucun processus `Erable.Desktop` ni port en écoute.
- Correctif au passage : `unwrap()` traitait une réponse 204 sans corps comme une erreur (trouvé par le test de `establishDesktopSession`). L'échec de l'échange de session ne bloque plus le premier rendu.
- Dette : `/favicon.ico` répond 404 (pas encore d'icône d'application ; à prévoir avec le packaging, M0-11 / M5-03).

**Vérification** : `dotnet test --solution Erable.sln` (Debug et Release) · `pnpm -C src/web test`, `lint`, `api:check`, `build` · lancement manuel de `Erable.Desktop` + contrôle CDP.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
