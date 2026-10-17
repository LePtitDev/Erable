# Publier une version

Une version est un **tag Git `vX.Y.Z`** poussé par l'utilisateur. Le workflow `.github/workflows/release.yml` fait le reste ; la release reste un **brouillon** tant que tout n'est pas vert, puis elle est publiée automatiquement.

| Étape | Ce qui est vérifié ou produit |
|---|---|
| `legacy` | chaque projet figé de `spec/projects/legacy/vN` s'ouvre, migre vers le format courant et reste intact |
| `notes` | notes de version générées depuis les Conventional Commits depuis le tag précédent (`build/release-notes.mjs`) |
| `package` (×4) | `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64` : publication autonome puis **Velopack** (`build/pack-desktop.mjs`) ; fumée `--self-test` sur l'application **installée** (installeur Windows, AppImage, bundle macOS) ; fichiers joints à la release |
| `image` | image Docker du serveur, scan Trivy (HIGH/CRITICAL corrigeables = échec), publication sur `ghcr.io/<propriétaire>/erable:<version>` et `latest` |
| `publish` | passage du brouillon en release publiée |

Un lancement manuel (*Actions → Release → Run workflow*, avec une version) construit tout **sans rien publier** : les paquets restent en artefacts du workflow.

## Livrables par système

| Système | Fichiers (dans `artifacts/release/<rid>/`) |
|---|---|
| Windows | `Erable-win-Setup.exe` (installeur par utilisateur, sans droits administrateur), paquets `.nupkg`, `Erable-win-Portable.zip` |
| macOS | `Erable-osx-<arch>-Setup.pkg`, `Erable-osx-<arch>-Portable.zip`, paquets de mise à jour |
| Linux | `Erable-linux-x86_64.AppImage`, `Erable-linux-Portable.zip`, paquets de mise à jour |
| Tous | `releases.<canal>.json` et `RELEASES-*` : le manifeste que l'application interroge |

Les noms exacts sont ceux de `vpk` (Velopack).

## Mise à jour automatique

Au démarrage, l'application installée interroge les releases GitHub. Si une version plus récente existe, elle est téléchargée en arrière-plan et **installée à la prochaine fermeture** : jamais en pleine session. Un build de développement (non installé) ne vérifie rien.

Désactivation :

- variable d'environnement `ERABLE_DISABLE_UPDATES=1` (déploiements gérés) ;
- ou fichier `desktop.json` dans le dossier de profil (`%APPDATA%\Erable`, `~/Library/Application Support/Erable`, `~/.config/erable`) : `{ "checkForUpdates": false }`.

### Vérifier une mise à jour N → N+1 sans rien publier

`ERABLE_UPDATE_SOURCE` remplace GitHub par un dossier (ou une URL) contenant les fichiers de `vpk pack`.

```bash
# 1. Empaqueter N puis N+1 dans le même dossier de flux
node build/pack-desktop.mjs win-x64 --version 1.0.0
mkdir /tmp/erable-feed && cp -r artifacts/release/win-x64/* /tmp/erable-feed
node build/pack-desktop.mjs win-x64 --version 1.0.1 --skip-publish
cp -r artifacts/release/win-x64/* /tmp/erable-feed
```

Installer ensuite 1.0.0 (son `Setup`), lancer l'application avec le flux local, la fermer, la relancer : la version affichée en bas de la barre latérale de l'accueil est 1.0.1.

```bash
ERABLE_UPDATE_SOURCE=/tmp/erable-feed "$LOCALAPPDATA/Erable/current/Erable.Desktop.exe"
```

Cette procédure est à rejouer une fois avant la première release publique.

## Signature (rien n'est acheté ni créé par l'agent)

Sans signature, tout fonctionne mais Windows SmartScreen et macOS Gatekeeper affichent un avertissement (« application non reconnue »). Pour s'en passer, **vous** devez fournir (architecture §A-11.4) :

| Quoi | Où l'obtenir | Secret GitHub à créer |
|---|---|---|
| Signature Windows | **Azure Trusted Signing** (compte Azure, validation d'identité, ~10 $/mois) : un compte de signature et un profil de certificat | `ERABLE_SIGN_AZURE_METADATA` : contenu du fichier `metadata.json` (endpoint, compte, profil). L'authentification Azure (service principal, variables `AZURE_*`) se branche quand le compte existe |
| Signature et notarisation macOS | **Apple Developer Program** (~99 $/an) : certificats *Developer ID Application* et *Developer ID Installer* | `ERABLE_SIGN_APPLE_APP_IDENTITY` (nom du certificat d'application), `ERABLE_SIGN_APPLE_INSTALL_IDENTITY` (nom du certificat d'installeur), `ERABLE_NOTARY_PROFILE` (profil `notarytool` enregistré dans le trousseau du runner) ; les certificats `.p12` doivent être importés dans le trousseau du runner avant l'étape *Package* |
| Linux | rien de payant | — |

Les étapes de câblage (authentification Azure, import du trousseau Apple) dépendent de comptes qui n'existent pas encore : elles ne sont donc pas écrites dans `release.yml` et s'ajouteront avec les secrets. `build/pack-desktop.mjs` lit déjà les variables ci-dessus et signe quand elles sont présentes.

## Avant le premier tag

1. Rejouer la vérification de mise à jour ci-dessus.
2. Lancer le workflow à la main avec la version `0.0.0-test` pour vérifier que les installeurs se construisent.
3. Vérifier `docs/deployment.md` (image publiée sous `ghcr.io/...`).
