# M1-06 — Blobs et pièces jointes (backend)

- **Statut** : todo
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

- [ ] Même fichier attaché à deux pages → deux pièces jointes, un blob (base d'E2E-06-08).
- [ ] Requête `Range` → 206 avec le bon segment.
- [ ] Un blob encore référencé par une révision n'est pas collecté.
- [ ] Interruption simulée pendant l'écriture : aucun blob partiel référencé.

## Points d'attention

- Ne jamais servir un fichier utilisateur avec un `Content-Type` qui permettrait l'exécution de script.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
