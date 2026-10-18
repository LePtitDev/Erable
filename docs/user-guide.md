# Erable — Guide de l'utilisateur

Erable est un wiki structuré, « façon IDE » : des pages en Markdown, rangées dans un arbre, reliées entre elles par leur identifiant, décrites par des catégories et des propriétés. Ce guide tient en quatre points ; le reste de l'application se découvre en l'utilisant.

## 1. Démarrer

1. **Accueil.** Au premier lancement, choisissez *Nouveau projet* : un nom, la **langue du contenu** (les pages seront écrites dans cette langue ; elle ne change plus ensuite) et un emplacement. *Ouvrir un dossier…* rouvre un projet existant (`Ctrl+O`), *Projets récents* retrouve les derniers.
2. **L'arbre** (à gauche) a quatre racines fixes : **Pages**, **Catégories**, **Propriétés**, **Liens**. Le menu contextuel (clic droit) crée un dossier ou une page ; `F2` renomme l'élément sélectionné ; on déplace par glisser-déposer, ou au clavier avec `Ctrl+X` puis `Ctrl+V`.
3. **Une page** s'ouvre en lecture ; *Modifier* passe en édition. `Ctrl+S` enregistre. Un brouillon est gardé automatiquement : si l'application se ferme avant l'enregistrement, il vous est proposé à la réouverture.
4. **Retrouver** : `Ctrl+K` ouvre la recherche rapide (un nom, `Entrée`) ; la page *Recherche* ajoute des filtres sur les catégories et les propriétés.
5. Rien n'est perdu par erreur : les éléments supprimés vont dans la **corbeille** et chaque enregistrement crée une **révision** que l'on peut comparer et restaurer (*Historique*).
6. **Sauvegarder** un projet local, c'est copier son dossier quand l'application est fermée. Pour travailler à plusieurs, un administrateur installe un serveur (`docs/deployment.md`).

Sous Linux, l'application a besoin de WebKitGTK 4.1 et de libnotify (Debian / Ubuntu : `sudo apt install libwebkit2gtk-4.1-0 libnotify4`).

Les préférences (langue de l'interface, thème clair ou sombre, raccourcis) se règlent depuis l'accueil (*Préférences*) comme depuis la barre du haut d'un projet.

## 2. Relier les pages : la syntaxe `[[ID]]`

Chaque élément — page, catégorie, propriété, pièce jointe — a un **identifiant** de 16 caractères hexadécimaux, qui ne change jamais. On y renvoie en l'écrivant entre doubles crochets :

| Vous écrivez | Résultat |
|---|---|
| `[[3F2A9C14E07B5D21]]` | un lien vers l'élément, affiché sous **son nom actuel** |
| `[[3F2A9C14E07B5D21\|la marchande]]` | le même lien avec un libellé de votre choix |
| `[[3F2A9C14E07B5D21#histoire]]` | un lien vers le titre « Histoire » de la page |
| `![[3F2A9C14E07B5D21]]` | l'élément **intégré** (une image, un fichier) |
| `![[3F2A9C14E07B5D21]]{width=320 align=right}` | une intégration à 320 px de large, alignée à droite |

On n'écrit presque jamais un identifiant à la main : tapez `[[` (ou `![[`) dans l'éditeur, la **palette d'insertion** s'ouvre. Tapez quelques lettres du nom, `Tab` change de type (Tout, Pages, Pièces jointes, Catégories, Propriétés), `Entrée` insère un lien, `Maj+Entrée` une intégration, `Échap` ferme la palette. Dans l'éditeur, l'identifiant s'affiche sous la forme d'une pastille avec le nom ; *Afficher les ID bruts* montre le texte réel.

Comme les liens passent par l'identifiant, **renommer ou déplacer un élément ne casse jamais rien** : tous les liens montrent le nouveau nom. Le détail de la syntaxe (tableaux, code, cas limites) est dans `docs/markdown-syntax.md`. Le HTML brut est désactivé : une balise écrite dans une page s'affiche comme du texte.

Glisser un fichier dans l'éditeur, ou le coller, l'ajoute comme pièce jointe de la page et insère l'intégration.

## 3. Raccourcis clavier

| Raccourci | Action |
|---|---|
| `Ctrl+K` (`⌘K`) | Recherche rapide * |
| `Ctrl+Espace` (`⌘⇧K`) | Ouvrir la palette d'insertion, dans l'éditeur * |
| `Ctrl+S` (`⌘S`) | Enregistrer la page * |
| `Ctrl+O` (`⌘O`) | Ouvrir un dossier de projet (accueil) * |
| `[[` ou `![[` | Ouvrir la palette d'insertion en tapant |
| `F2` | Renommer l'élément sélectionné de l'arbre |
| `Ctrl+X`, `Ctrl+V` | Couper, coller un élément de l'arbre |
| `Suppr` | Mettre l'élément à la corbeille |
| `Échap` | Fermer une fenêtre ou la palette |

Les raccourcis marqués d'un astérisque sont **configurables** : *Préférences → Raccourcis clavier*. Cliquez sur la combinaison à changer puis appuyez sur la nouvelle ; si elle est déjà prise, Erable le dit (« Déjà utilisé par : Recherche ») et propose de la remplacer. *Aide et raccourcis*, depuis l'accueil, affiche la liste à jour.

## 4. Dossiers et sous-pages

Erable propose deux façons de hiérarchiser, avec deux rôles différents :

- Un **dossier** sert à **ranger**. Il ne porte pas de contenu : on peut le déplacer ou le renommer sans que le sens de quoi que ce soit change. Exemple : un dossier « Personnages » qui regroupe cinquante pages.
- Une **sous-page** donne un **sens**. C'est une page placée sous une autre page : elle apparaît dans la section *Sous-pages* de son parent et en fait partie. Exemple : « Dialogues » et « Inventaire » sous la page d'un personnage.

Pour décider : *« si je supprime ou déplace le parent, la page doit-elle suivre ? »* Si oui, c'est une sous-page ; sinon, un dossier suffit. Les catégories, elles, ne se rangent pas dans l'arbre des pages : une catégorie décrit ce que sont les pages (un personnage, un lieu…) et leur apporte des **propriétés** ; une page peut en avoir plusieurs.
