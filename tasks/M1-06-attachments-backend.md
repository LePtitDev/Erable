# M1-06 — Blobs et pièces jointes (backend)

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-05
- **Références** : §A-7.3, §A-9, F06.R2–R3, F06.R5, §T-6, §T-6.1

## Objectif

Stocker les pièces jointes de façon sûre, dédupliquée et diffusable en streaming.

## Périmètre

- `IBlobStore` + implémentation disque : `blobs/ab/cd/<sha256>` ; écriture dans un fichier temporaire, `fsync`, `rename` atomique, **puis** insertion en base.
- Table `attachments` (id = `ElementId`, page propriétaire, nom, MIME, taille, sha256).
- `POST pages/{id}/attachments` (multipart, en flux, sans tout charger en mémoire) ; limite de taille configurable, dépassement refusé sans fichier partiel (F06.R5).
- `GET attachments/{id}` : `Range` (206), `ETag`, `Content-Disposition`, `X-Content-Type-Options: nosniff` ; SVG et HTML forcés en téléchargement (F06.R2).
- Détection du MIME par signature, pas seulement par extension.
- Ramasse-miettes : supprime les blobs que ni pièce jointe ni révision ne référence plus.

## Critères d'acceptation

- [x] Même fichier attaché à deux pages → deux pièces jointes, un blob (base d'E2E-06-08).
- [x] Requête `Range` → 206 avec le bon segment.
- [x] Un blob encore référencé par une révision n'est pas collecté.
- [x] Interruption simulée pendant l'écriture : aucun blob partiel référencé.

## Points d'attention

- Ne jamais servir un fichier utilisateur avec un `Content-Type` qui permettrait l'exécution de script.

## Notes d'implémentation

- **Core** : `MediaTypes` (détection par signature, puis balisage HTML/SVG/XML dans le contenu quelle que soit l'extension, puis extension ; affichage `image|video|audio|pdf|file`), `AttachmentFileName` (dernier segment du chemin + règles des noms d'élément). Les tests ont révélé un bug : la signature PNG écrite `"PNG…"u8` encode U+0089 sur deux octets ; remplacée par un tableau d'octets.
- **Storage** : `IBlobStore` + `DiskBlobStore` (`blobs/ab/cd/<sha256>`). Écriture en flux dans `blobs/tmp/<guid>` avec SHA-256 incrémental et capture des 512 premiers octets, `Flush(true)` (fsync), puis `File.Move` atomique ; contenu déjà présent → le temporaire est supprimé (déduplication). Dépassement de la limite → `null`, fichier temporaire supprimé.
- **Ordre des écritures** : blob d'abord, puis élément `attachment` (parent = page) + ligne `attachments` + révision 1 (`blob_sha256` renseigné) dans **une** transaction. Une écriture interrompue ne laisse rien ; un échec après le renommage laisse au pire un blob non référencé, collecté plus tard.
- **Ramasse-miettes** (`ProjectService.CollectGarbageAsync`) : sous le verrou d'écriture, supprime les blobs que ni `attachments` (corbeille comprise) ni `revisions.blob_sha256` ne référencent. Un **bail** (`StoredBlob`, libéré après le commit) protège un téléversement en cours entre le renommage et l'insertion. Exécuté à l'ouverture d'un projet, avec le nettoyage de `blobs/tmp` (aucune écriture ne peut alors être en cours).
- **API** :
  - `POST pages/{id}/attachments` : `multipart/form-data`, **un fichier par requête** (l'interface enverra une requête par fichier, ce qui donne une progression par fichier). Lecture par `MultipartReader`, sans `IFormFile` (qui copie tout en mémoire ou sur disque avant le traitement). `Content-Length` trop grand → 413 immédiat ; sinon la limite est appliquée pendant le flux. 413 avec `code = attachment.tooLarge` et `maxSize`, 415 si le corps n'est pas multipart, 404 si la cible n'est pas une page.
  - `GET attachments/{id}` : `Range` (206), `ETag` fort = sha256, `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox`, `Content-Disposition` avec `filename*`. Seuls les types affichés inline (images hors SVG, vidéo, audio, PDF) gardent leur type et `inline` ; **tout le reste**, SVG et HTML compris, est servi en `application/octet-stream` + `attachment`, pour qu'aucun fichier utilisateur ne puisse exécuter de script dans notre origine.
  - `resolve` renvoie désormais `attachment` (nom, type, taille, affichage) pour les pièces jointes : une page qui intègre N fichiers ne fait toujours qu'**un** appel. `AttachmentItem` gagne `display`.
- **Limite** : `ErableHostOptions.MaxAttachmentSize` (256 Mio par défaut), `erable serve --max-attachment-mb`, et `AttachmentSettings` modifiable à chaud (le point d'appui de test de M1-07 s'en servira pour E2E-06-07).
- **OpenAPI** : l'upload n'a pas de schéma de corps (`Accepts<IFormFile>` ajoute une contrainte de routage qui transforme un mauvais `Content-Type` en 404 via le repli `/api/*`). Le client TS fera l'upload en `XMLHttpRequest` (progression), avec le type de réponse `AttachmentItem` généré.
- **Dette** : suppression d'une pièce jointe → M2-08 ; quotas par projet → M4.

**Vérification** : `dotnet test --solution Erable.sln` (364 réussis, 2 ignorés) · `dotnet format --verify-no-changes` · `pnpm api:generate` puis `typecheck`, `lint`, `format:check`, `test` (251).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
