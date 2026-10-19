# M6-08 — Packaging Android et release

- **Statut** : todo
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

- [ ] APK construit et signé en local.
- [ ] Workflows valides, actions épinglées par SHA.
- [ ] Documentation à jour.

## Notes d'implémentation

_À remplir par l'agent._
