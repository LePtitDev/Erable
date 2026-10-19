# M6-08 — Packaging Android et release

- **Statut** : review
- **Jalon** : M6
- **Dépend de** : M6-07, M5-03
- **Références** : décision Q11, docs/release.md, §T-10.1

## Objectif

Produire un APK signé à chaque release, et pouvoir le construire en local.

## Périmètre

- `build/publish-android.mjs [--version x.y.z]` → `artifacts/android/Erable-<version>.apk` (`versionName` et `versionCode` dérivés de la version) ; signature par un keystore fourni par variables d'environnement (`ERABLE_ANDROID_KEYSTORE`, …), sinon clé de debug avec un avertissement.
- Raccourci `scripts/publish.sh|.bat --android`.
- Job `android` dans `release.yml` (workload `android`, keystore en secret) ; construction de l'APK dans `pr.yml` quand `src/Erable.Android` change.
- Documentation : `docs/release.md` (signature, liste de contrôle sur appareil), `docs/user-guide.md` (mobile et app Android), `CLAUDE.md` §9.

## Critères d'acceptation

- [x] APK construit et signé en local (`node build/publish-android.mjs --version 0.1.0-dev.1` → `artifacts/android/Erable-0.1.0-dev.1.apk`, versionCode 100001, clé de debug faute de keystore ; signature vérifiée par `apksigner`). Signature par keystore de release non essayée (pas de keystore).
- [x] Workflows valides (YAML relu ; actionlint tournera dans la CI), uniquement des actions déjà épinglées du dépôt.
- [x] Documentation à jour (`docs/release.md`, `docs/user-guide.md`, `scripts/README.md`, `CLAUDE.md`).

## Notes d'implémentation

- **`build/publish-android.mjs`** : `dotnet publish -c Release -f net10.0-android`, `versionName` = version, `versionCode` = `((maj × 1000 + min) × 1000 + corr) × 100 + n` (`n` = 99 pour une release, numéro du build pour `-dev.N`), majeure ≤ 20 (plafond Android 2 100 000 000). Signature par `ERABLE_ANDROID_KEYSTORE` / `_KEY_ALIAS` / `_KEYSTORE_PASSWORD` / `_KEY_PASSWORD`, mots de passe passés à MSBuild en `env:` (jamais sur la ligne de commande) ; sinon clé de debug et avertissement.
- **`scripts/publish.sh|.bat --android`** : l'APK seul.
- **CI** : job `android` dans `pr.yml` (workload, `InstallAndroidDependencies`, APK en artefact 7 jours ; pas de filtre de chemins : le dépôt n'en a pas, et le job tourne en parallèle) et dans `release.yml` (keystore décodé depuis `ERABLE_ANDROID_KEYSTORE_BASE64` ; **sans keystore, APK construit mais non joint** : une clé de debug change à chaque runner et rendrait les mises à jour impossibles). `publish` ne dépend pas du job Android.
- **Non fait par l'agent** : créer le keystore et les secrets GitHub (procédure dans `docs/release.md`).
