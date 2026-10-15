# M1-14 — Pièces jointes (interface)

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-12, M1-06
- **Références** : F06, Mockup « Page — lecture » (panneau Pièces jointes)

## Objectif

Ajouter des fichiers à une page aussi naturellement qu'en les collant.

## Périmètre

- Coller ou déposer un ou plusieurs fichiers dans l'éditeur → téléversement avec progression, puis `![[ID]]` inséré au curseur (F06.R1).
- Panneau « Pièces jointes » : extension, nom, taille, « + Ajouter » (sans insertion dans le texte).
- Rendu des intégrations par type : image, vidéo (streaming), audio, PDF ; autres types en carte téléchargeable ; SVG et HTML **jamais** inline (F06.R2).
- Limite de taille : message clair, aucune pièce jointe créée (F06.R5).
- Actions : ouvrir avec l'application système / révéler dans l'explorateur (desktop, via le pont natif).

## Critères d'acceptation

- [x] `E2E-06-01` à `E2E-06-05`, `E2E-06-07`, `E2E-06-08` verts. (`E2E-06-06` en M2-08.)
- [x] Échec réseau pendant un téléversement : aucune pastille orpheline dans le texte.

## Points d'attention

- Pendant le téléversement, insérer un marqueur temporaire non enregistrable, remplacé par l'ID final.

## Notes d'implémentation

- **Marqueur temporaire = widget CodeMirror, jamais du texte** (`features/editor/uploads.ts`) : chaque fichier en cours est un `StateField` (clé, position, nom, progression) rendu en pastille `role="progressbar"`. La position suit les modifications faites pendant l'envoi ; à la réussite, `![[ID]]` est inséré à cet endroit (saut de ligne entre fichiers déposés ensemble) ; à l'échec, le widget disparaît et le texte n'a jamais changé. Le document ne peut donc pas être enregistré avec un marqueur. « Enregistrer » (bouton et `Ctrl+S`) est inactif tant qu'un envoi est en cours, sinon l'`![[ID]]` serait perdu. Testé sur `EditorState` (`uploads.test.ts`).
- **Envoi** : `shared/api/attachments.ts`, `XMLHttpRequest` (seul à donner la progression d'envoi), un fichier par requête comme prévu en M1-06. `useUploader` traduit le 413 `attachment.tooLarge` en « « x » dépasse la taille maximale autorisée (10 Mo). Aucune pièce jointe n'a été créée. » avec la limite renvoyée par le backend (`maxSize`). Quitter l'éditeur annule les envois en cours.
- **Collage / dépôt** : `EditorView.domEventHandlers` (paste, drop au point de dépôt) ; le bouton « Pièce jointe » de la barre d'outils, jusqu'ici inactif, ouvre le sélecteur de fichiers et insère au curseur.
- **Panneau** : « + Ajouter » (sélecteur multiple), progression par fichier (`<progress>`), messages d'erreur ; aucune insertion dans le texte.
- **Rendu par type** : déjà livré en M1-11 (`Embed`, `FileCard`) ; vérifié ici par E2E-06-02 et E2E-06-05.
- **Écart** : « ouvrir avec l'application système / révéler dans l'explorateur » dépend du pont natif exposé par l'API, livré en **M1-17** ; les actions du panneau y sont ajoutées.
- **E2E-06-04 sous WebKit** : le WebKit Windows de Playwright lit ce petit fichier (73 Ko) d'un bloc, sans `Range` ; le test y vérifie que le backend annonce `Accept-Ranges: bytes`, et exige le 206 dans Chromium (et Firefox la nuit).
- **Constat** : un téléversement interrompu par le client *après* l'écriture du blob laisse un blob orphelin (avertissement `blob.orphan` de `doctor`, collecté à la réouverture), conforme à M1-06. Les tests attendent donc la fin réelle de l'envoi (le page object ne cible que les pièces jointes enregistrées).
- **Test hors scénario** : « M1-14 · A failed upload leaves no pill in the text » (connexion coupée via `page.route`).

**Vérification** : `pnpm -C src/web typecheck`, `lint`, `format:check`, `test` (338), `exec playwright test --project chromium --project webkit` (102, dont les 8 de `f06-attachments.spec.ts`).

**Commit** : `feat(web): upload attachments by paste, drop and side panel with progress (M1-14)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
