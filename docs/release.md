# Publier une version

Une version est un **tag Git `vX.Y.Z` ou `X.Y.Z`**. Deux façons de la créer, au choix ; le workflow `.github/workflows/release.yml` fait le reste :

- **pousser le tag** (`git tag v1.2.0 && git push origin v1.2.0`) : le workflow crée une release **brouillon**, y joint les installeurs, puis la publie quand tout est vert ;
- **créer la release sur GitHub** (*Releases → Draft a new release*, nouveau tag, *Publish release*) : GitHub pousse le tag, le workflow garde la release telle quelle (titre, notes, publiée) et y **joint les installeurs** une fois construits. Si la release n'a pas de notes, il y met celles générées. Tant que le workflow tourne (≈ 30 min), la release publiée n'a pas encore ses fichiers ; si un job échoue, elle reste sans eux (relancer le workflow, ou le rattrapage ci-dessous).

**Rattrapage** d'une release existante sans installeurs (tag créé avant ce workflow, échec) : *Actions → Release → Run workflow*, `version` = le tag exact (ex. `1.0.0`), cocher **attach**. Le workflow construit le code **du tag** et joint les fichiers à sa release, sans changer son état (brouillon ou publiée).

| Étape | Ce qui est vérifié ou produit |
|---|---|
| `notes` | notes de version générées depuis les Conventional Commits depuis le tag précédent (`build/release-notes.mjs`) |
| `package` (×4) | `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64` : publication autonome (binaires estampillés de la version du tag) puis **Velopack** (`build/pack-desktop.mjs`) et `erable-cli` ; fichiers joints à la release |
| `android` | APK de l'application Android (`build/publish-android.mjs`), signé par le keystore de release et joint à la release ; sans keystore, construit mais **non joint** (avertissement). Ce job ne bloque pas `publish` |
| `image` | image Docker du serveur, scan Trivy (HIGH/CRITICAL corrigeables = échec), publication sur `ghcr.io/<propriétaire>/erable:<version>` et `latest` |
| `publish` | passage du brouillon en release publiée (sans effet si elle l'est déjà) |

Le workflow ne lance **aucun test** : le commit tagué est déjà passé par le pipeline PR (tests, migration des projets `legacy/`, fumée desktop sur les 3 OS), et un push de tag ne relance pas ce pipeline.

Un lancement manuel (*Actions → Release → Run workflow*, avec une version, sans **attach**) construit tout **sans rien publier** : les paquets restent en artefacts du workflow.

Pour produire un installeur sur sa machine, sans CI : `scripts/publish.sh` (ou `scripts\publish.bat`), voir `scripts/README.md` ; la version vient du dernier tag Git si elle n'est pas donnée.

## Livrables par système

| Système | Fichiers (dans `artifacts/release/<rid>/`) |
|---|---|
| Windows | `Erable-win-Setup.exe` (installeur par utilisateur, sans droits administrateur), paquets `.nupkg`, `Erable-win-Portable.zip` |
| macOS | `Erable-osx-<arch>-Setup.pkg`, `Erable-osx-<arch>-Portable.zip`, paquets de mise à jour ; chaque architecture a son **canal** Velopack (`osx-arm64`, `osx-x64`) pour que leurs fichiers ne s'écrasent pas dans la release |
| Linux | `Erable-linux-x86_64.AppImage`, `Erable-linux-Portable.zip`, paquets de mise à jour |
| Tous | `releases.<canal>.json` et `RELEASES-*` : le manifeste que l'application interroge |
| Tous | `erable-cli-<rid>.zip` (Windows) ou `.tar.gz` : l'outil `erable-cli` en un exécutable autonome (`docs/cli.md`), dans `artifacts/cli/` |

| Android | `Erable-<version>.apk` (dans `artifacts/android/`) : à installer à la main (*Sources inconnues*), Android 8 ou plus récent ; c'est un client des serveurs Erable (décision Q11) |

Les noms exacts sont ceux de `vpk` (Velopack).

**Dépendances Linux.** L'AppImage n'embarque pas le moteur web ni les bibliothèques du bureau : la machine doit fournir WebKitGTK 4.1 et libnotify (PhotinoX s'arrête au démarrage sans `libnotify.so.4`). Sous Debian / Ubuntu : `sudo apt install libwebkit2gtk-4.1-0 libnotify4` ; sous Fedora : `sudo dnf install webkit2gtk4.1 libnotify`.

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
| Android | rien de payant : un **keystore** que vous créez et gardez (voir ci-dessous) | `ERABLE_ANDROID_KEYSTORE_BASE64` (le fichier en base64), `ERABLE_ANDROID_KEY_ALIAS`, `ERABLE_ANDROID_KEYSTORE_PASSWORD`, `ERABLE_ANDROID_KEY_PASSWORD` (facultatif : celui du keystore par défaut) |

### Keystore Android

Android n'installe une mise à jour que si elle est signée par **la même clé** que la version installée : le keystore se crée une fois et ne se perd pas (sans lui, les utilisateurs devraient désinstaller l'application pour passer à la version suivante). À faire par vous, avec le `keytool` d'un JDK 17 :

```bash
keytool -genkeypair -v -keystore erable-release.keystore -alias erable -keyalg RSA -keysize 4096 -validity 10000
```

Puis créer les secrets GitHub (`base64 -w0 erable-release.keystore` donne la valeur de `ERABLE_ANDROID_KEYSTORE_BASE64`) et garder une copie du fichier et des mots de passe hors du dépôt. En local, les mêmes variables sans `_BASE64` : `ERABLE_ANDROID_KEYSTORE` (chemin du fichier), `ERABLE_ANDROID_KEY_ALIAS`, `ERABLE_ANDROID_KEYSTORE_PASSWORD`, puis `scripts/publish.sh --android`. Sans elles, l'APK est signé par la clé de debug de la machine : bon pour un essai, pas pour une diffusion.

`versionCode` (ce qu'Android compare) vient de la version : `((majeure × 1000 + mineure) × 1000 + correctif) × 100 + n`, avec `n = 99` pour une release et le numéro du build pour `x.y.z-dev.N` (au plus 98) ; une version majeure au plus 20.

### Vérifier l'application Android sur un appareil

La CI construit l'APK mais n'a pas d'émulateur : avant une release, sur un téléphone (ou un émulateur Android Studio) avec un serveur de test (`scripts/serve.sh`, option `--lan` pour le réseau local) :

1. Installer l'APK, l'ouvrir : la liste des serveurs est vide, dans la langue du téléphone (fr, en, de, es ; anglais sinon).
2. *Ajouter un serveur* avec une adresse fausse (`exemple.invalid`) : « injoignable » ; avec l'adresse d'un site quelconque : « ne répond pas comme un serveur Erable » ; avec celle du serveur : il s'ouvre. En `http://`, la liste affiche « Non chiffré (http) ».
3. Se connecter, ouvrir une page, la modifier : le clavier ne cache pas *Enregistrer* ; les encoches et la barre de gestes ne cachent rien.
4. Ajouter une pièce jointe depuis l'éditeur (sélecteur Android), puis télécharger une pièce jointe : elle arrive dans *Téléchargements*.
5. Un lien vers un autre site s'ouvre dans le navigateur ; le bouton retour remonte l'historique, puis revient à la liste.
6. Fermer l'application (la tuer depuis les applications récentes), la rouvrir : la session est toujours là.
7. Appui long sur le serveur → *Oublier* : il quitte la liste ; ajouté de nouveau, il redemande la connexion.

Les étapes de câblage (authentification Azure, import du trousseau Apple) dépendent de comptes qui n'existent pas encore : elles ne sont donc pas écrites dans `release.yml` et s'ajouteront avec les secrets. `build/pack-desktop.mjs` lit déjà les variables ci-dessus et signe quand elles sont présentes.

## Avant le premier tag

1. Rejouer la vérification de mise à jour ci-dessus.
2. Lancer le workflow à la main avec la version `0.0.0-test` pour vérifier que les installeurs se construisent.
3. Vérifier `docs/deployment.md` (image publiée sous `ghcr.io/...`).
4. Créer le keystore Android et ses secrets, puis dérouler la vérification sur appareil ci-dessus.
