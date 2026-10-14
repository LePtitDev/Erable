# M1-11 — Lecture d'une page

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-09
- **Références** : F03, F04, §A-6, §A-9, Mockup « Page — lecture »

## Objectif

Afficher une page fidèle au mockup, avec des références toujours à jour et un rendu sûr.

## Périmètre

- En-tête : fil d'Ariane, titre, bouton « Modifier », ID affiché `XXXX XXXX XXXX XXXX` copiable au format canonique avec confirmation (F03.R2), pastilles de catégories (vides jusqu'à M2), « Modifiée il y a … par … · créée le … ».
- Rendu unified : `remark-parse` + `remark-gfm` + plugin Erable (M1-04) → composants React ; `rehype-sanitize` ; HTML brut affiché échappé (F04.R5).
- Références : nom **actuel** via `resolve` groupé et mis en cache ; `[[ID|texte]]` ; ancres ; lien cassé stylé avec infobulle (F04.R2).
- Intégrations `![[ID]]` : image, vidéo, audio, PDF, sinon carte de fichier ; légende « Pièce jointe de <page> » si elle appartient à une autre page.
- Sommaire (niveaux 2 et 3), défilement, ancre dans l'URL, entrée active (F04.R3).
- Aperçu au survol d'une référence (E2E-04-03).
- Section « Sous-pages » + « Nouvelle sous-page ».
- Panneau latéral : pièces jointes en M1 ; Propriétés et Liens (F04.R4) ajoutés en M2-06 et M2-07, emplacements prévus.
- Retour arrière : restauration de la position de défilement.
- Corps des pages en Source Serif 4 (mockup).

## Critères d'acceptation

- [x] `E2E-03-01`, `E2E-03-02`, `E2E-04-01` à `E2E-04-04`, `E2E-04-06`, `E2E-04-07` verts. (`E2E-04-05` en M2-08.)
- [x] Corpus XSS (§T-8.1) : aucun élément exécutable produit.
- [x] Ouverture d'une page < 200 ms (mesure via `performance.mark`).

## Points d'attention

- Une page qui référence 200 éléments ne doit déclencher qu'un appel `resolve`.

## Notes d'implémentation

- **Rendu** (`src/features/page/markdown/`) : l'arbre mdast du parseur de M1-04 (remark-parse + remark-gfm + extension Erable) est rendu **directement en éléments React** (`MarkdownView`). **Écart local à §A-3.1** (qui prévoit `rehype-sanitize`) : sans passer par du HTML, il n'y a rien à assainir — React échappe tout texte, le HTML brut est affiché comme texte (F04.R5), aucun `dangerouslySetInnerHTML`, et les URL de liens et d'images passent par une liste blanche (`http`, `https`, `mailto`, liens internes ; `javascript:`, `data:`… supprimés). Deux dépendances de moins (`remark-rehype`, `rehype-sanitize`) et une surface d'attaque plus petite. Preuve : corpus XSS de 11 cas (§T-8.1) en test de composant, plus E2E-04-07.
- **Références** (F04.R1–R2) : une **seule** requête `resolve` par page, quel que soit le nombre de références (testé) ; la clé de cache est sous le projet, donc renommer ou déplacer un élément rafraîchit toutes les pages qui l'affichent (E2E-02-02). Libellé `[[ID|texte]]`, ancre `[[ID#ancre]]` ; cible supprimée ou inconnue → lien cassé (style dédié, infobulle, libellé accessible « … (lien cassé : Élément supprimé) »), jamais d'erreur. Dans un lien Markdown, la référence n'est pas cliquable (R8).
- **Aperçu au survol** (E2E-04-03) : `HoverCard` Radix, carte avec badge, nom, ID abrégé `7A1C…D615`, début du contenu (texte brut coupé sur un mot, page chargée au premier survol) et chemin.
- **Intégrations** (`Embed`) : image, vidéo (`preload="metadata"`, lecture en flux par `Range`), audio, PDF (`iframe`), sinon **carte de fichier** téléchargeable (`FileCard`) — SVG et HTML compris, jamais insérés comme balisage (F06.R2). Légende : libellé, puis « Pièce jointe de <page> » (lien) si le fichier appartient à une autre page, puis le nom du fichier ; attributs `width` et `align`. Une intégration de page s'affiche comme une référence ; une pièce jointe supprimée, comme un emplacement « Pièce jointe supprimée ». Un paragraphe fait uniquement d'intégrations devient une rangée de figures (une `<figure>` ne peut pas être dans un `<p>`).
- **En-tête** : `ElementHeader` de M1-09 (fil d'Ariane, titre, ID copiable, « Modifiée il y a … par … · créée le … ») + bouton « Modifier » (`?edit=true`, M1-12) + bascule du panneau latéral. Pastilles de catégories : emplacement prévu (`chips`), remplies en M2-06.
- **Sommaire** (F04.R3) : titres de niveaux 2 et 3 (ancres de §3, mêmes fixtures que le backend) + entrée « Sous-pages » ; un clic fait défiler, met l'ancre dans l'URL et marque l'entrée (`aria-current="location"`) ; le défilement met à jour l'entrée courante (verrouillé une seconde après un clic pour que le défilement provoqué ne déplace pas la marque). Ouvrir `…#ancre` fait défiler au titre une fois le contenu affiché.
- **Retour arrière** (E2E-04-04) : la zone de travail porte `data-scroll-restoration-id`, restaurée par le routeur.
- **Sous-pages** (E2E-04-06) : cartes + « Nouvelle sous-page » (page « Nouvelle page » créée sous la page, ouverte avec `?edit=true`).
- **Panneau latéral** (F04.R4) : Sommaire et Pièces jointes (extension, nom, taille en Ko/Mo, lien) ; Propriétés et Liens arrivent avec M2-06 / M2-07. Repliable, état dans la mise en page (`me/ui-state`).
- **Corps** en Source Serif 4, `lang` = langue du contenu du projet (décision Q1).
- **Performance** : `performance.mark("erable:page-open")` à l'ouverture, `measure("erable:page")` quand les noms sont résolus ; test Playwright `perf · …` < 200 ms (navigation interne, Chromium et WebKit). La fixture accepte `fakeClock: false` : l'horloge figée de Playwright remplace aussi `performance`, dont les marques ne mesurent alors rien.
- **Tests** : 20 tests de composants (`PageView.test.tsx` : en-tête, références, liens cassés, images et légendes, SVG en carte, sommaire, sous-pages, une seule résolution, corpus XSS) ; e2e `E2E-03-01`, `03-02`, `04-01` à `04-04`, `04-06`, `04-07`, mesure de performance, axe (clair et sombre). `E2E-02-02` (nom à jour dans la page d'Ilsa) est vert. Le presse-papiers est intercepté dans les e2e (même comportement dans les deux moteurs).

**Vérification** : `pnpm typecheck`, `lint`, `format:check`, `test` (305) · `playwright test f02-tree f03-ids f04-page a11y` (Chromium + WebKit).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
