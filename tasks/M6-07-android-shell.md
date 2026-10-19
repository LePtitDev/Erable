# M6-07 — Shell Android (.NET for Android)

- **Statut** : review
- **Jalon** : M6
- **Dépend de** : M6-01
- **Références** : F19, décision Q11, §A-9, §T-10.1

## Objectif

Une application Android qui garde une liste de serveurs Erable et ouvre la SPA du serveur choisi, sans rien embarquer du backend.

## Périmètre

- `src/Erable.Android` (`net10.0-android`, API 26 minimum), hors d'`Erable.sln` ; écran « Serveurs » (liste, ajout, oubli, avertissement HTTP) puis activité WebView.
- Logique pure (`ServerAddress`, `ServerCompatibility`, `NavigationPolicy`) sans dépendance Android, compilée aussi par `tests/Erable.Android.Tests` (xUnit, dans `Erable.sln`).
- WebView : JavaScript, stockage DOM, cookies persistés, navigation restreinte à l'origine, liens externes vers le navigateur, sélecteur de fichiers, téléchargements par `DownloadManager` avec les cookies, retour Android, `network_security_config` (clair permis, signalé).
- Ressources traduites `values` (en), `values-fr`, `values-de`, `values-es` ; icône dérivée du logo.

## Hors périmètre

- Projets locaux sur le téléphone, notifications, Play Store.

## Critères d'acceptation

- [x] `F19.R1` à `F19.R7` implémentées ; **non vérifiées sur appareil** (pas d'émulateur ici : liste de contrôle de `docs/release.md`).
- [x] Tests xUnit de la logique pure verts (32, `tests/Erable.Android.Tests`) ; `ServerAddress` / `ServerHandshake` testés dans Core (21).
- [x] `dotnet build src/Erable.Android` et publication Release sans avertissement ; `dotnet format` propre.

## Notes d'implémentation

- **Partage avec le desktop** : la normalisation d'adresse et la lecture de `/api/info` sortent de `Erable.Server/Servers/RemoteServers` vers `Erable.Core/Servers/ServerAddress.cs` (`ServerAddress.Normalize`, `ServerHandshake.Parse`), pur et utilisé par les deux clients. **Bug corrigé au passage** (desktop) : un `formatVersion` non numérique levait `InvalidOperationException` (`TryGetInt32` sur une chaîne), non rattrapée → 500 au lieu de « pas un serveur Erable ».
- **F19.R1 précisé** : l'app n'exige pas la même version d'API (la SPA affichée vient du serveur) ; elle exige une réponse Erable en `mode: "server"` (une app desktop est refusée). `docs/features.md` mis à jour.
- **Espace de noms** `Erable.Mobile` (et non `Erable.Android`, qui masquerait `Android.*`). `ApplicationId` provisoire : `fr.leptitdev.erable` (à confirmer : il ne change plus après une première diffusion).
- **Logique pure** (`Shell/`, sans type Android) : `NavigationPolicy` (même origine → WebView ; http(s) ailleurs, `mailto:`, `tel:` → système ; le reste bloqué), `ServerList` (JSON des préférences, une entrée par origine, lecture tolérante), `ServerErrors.Check`. Compilée aussi par `tests/Erable.Android.Tests` (lien `Compile Include`), qui est dans `Erable.sln` ; `src/Erable.Android` n'y est pas (workload requis).
- **Interface native** construite en code (pas de layout XML) : liste à deux lignes (hôte ; « Erable x.y.z », « Non chiffré (http) »), ajout avec vérification avant enregistrement (la boîte reste ouverte sur un refus, message traduit), appui long → oublier (cookies expirés). Ressources `values` (en), `values-fr|de|es`.
- **WebView** : JS et stockage DOM, pas d'accès fichiers / contenus, contenu mixte refusé, cookies tiers refusés et cookies vidés sur disque en pause ; sélecteur de fichiers (`OnShowFileChooser`) ; téléchargements par `DownloadManager` avec le cookie (Téléchargements publics dès Android 10, dossier de l'app avant, sans permission) ; retour : historique puis liste (`OnBackInvokedDispatcher` dès 13, `OnBackPressed` avant) ; bord à bord : marges des barres, de l'encoche et du clavier (Android 11+) ; rotation sans rechargement.
- **Icônes** : PNG par densité (`scripts/generate-icons.mjs`) et icône adaptative vectorielle (même feuille, arcs écrits en entier).
- **Outillage** : le workload `android` n'était installé que pour la bande SDK 10.0.100 alors que `global.json` impose 10.0.4xx ; installation par l'utilisateur (`dotnet workload install android`, droits administrateur), puis la plateforme API 36 (`-t:InstallAndroidDependencies`). L'avertissement « an error occurred trying to accept the Android SDK licenses » de cette commande est sans effet ici : la licence était déjà acceptée (Visual Studio) et l'API 36 s'est installée.
- **APK contrôlé** (`aapt2 dump`, `apksigner verify`) : `fr.leptitdev.erable`, min API 26, cible 36, seule permission `INTERNET`, `WebActivity` non exportée, `networkSecurityConfig` présent, langues de/es/fr (+ défaut), arm64-v8a / armeabi-v7a / x86_64, icône adaptative ; ≈ 17 Mo.
- **Corrections à la première compilation** : commentaire XML contenant `--` ; arguments de `GetString` passés en `Java.Lang.String` (nullabilité du binding) ; `SetDecorFitsSystemWindows` appelé seulement avant Android 15 (obsolète, et le bord à bord y est imposé).

## Propositions

- Vérifier le parcours sur un appareil réel avant la première release (liste de `docs/release.md`).
- Confirmer l'identifiant d'application `fr.leptitdev.erable` : il ne pourra plus changer après une diffusion.
- Android : un bouton retour pendant qu'une boîte de dialogue de la SPA est ouverte remonte l'historique au lieu de fermer la boîte ; la SPA pourrait fermer ses dialogues sur `popstate`.
