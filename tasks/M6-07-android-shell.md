# M6-07 — Shell Android (.NET for Android)

- **Statut** : todo
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

- [ ] `F19.R1` à `F19.R7`.
- [ ] Tests xUnit de la logique pure verts.
- [ ] `dotnet build src/Erable.Android -c Release` sans avertissement.

## Notes d'implémentation

_À remplir par l'agent._
