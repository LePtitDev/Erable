# Erable — Fonctionnalités et scénarios end-to-end

> Statut : **validé** · Version 0.1 · 13 octobre 2026
> Complète `docs/architecture.md` (§A-x) et `docs/testing.md` (§T-x).
> Rôle : catalogue **de référence** des fonctionnalités v1 et des scénarios e2e qui les vérifient. Chaque scénario doit pouvoir être traduit tel quel en un test Playwright.

---

## 1. Conventions

### 1.1 Identifiants

- **Fonctionnalité** : `F<nn>` (ex. `F05` — Édition).
- **Règle métier** : `F<nn>.R<n>` : un comportement attendu, vérifiable, indépendant de l'UI.
- **Scénario** : `E2E-<nn>-<nn>` (ex. `E2E-05-03`). L'ID figure **au début du titre du test Playwright**, suivi d'un titre **en anglais** (le code est en anglais), ce qui rend la traçabilité triviale (`grep E2E-05-03`). Le script `pnpm -C src/web e2e:trace` vérifie la correspondance dans les deux sens.
- Un ID n'est jamais réutilisé. Un scénario supprimé est marqué ~~barré~~ avec la raison.

### 1.2 Format d'un scénario

```gherkin
E2E-05-03 · Insérer un lien via la palette            P0 · @pr · M1
Étant donné  la page « Ilsa Varenne » ouverte en édition
Quand        je tape « [[orse »
Alors        la palette s'ouvre avec « Capitaine Orsen » en premier
```

- **Priorité** : `P0` = bloquant (perte de données, parcours central) ; `P1` = important ; `P2` = confort.
- **Tags** :

| Tag | Signification |
|---|---|
| `@pr` | exécuté à chaque PR (Chromium + WebKit) |
| `@nightly` | exécuté seulement la nuit (3 navigateurs) |
| `@multi` | nécessite plusieurs contextes navigateur (plusieurs utilisateurs) |
| `@server` | nécessite le mode serveur (authentification) |
| `@desktop` | rejoué dans le binaire desktop (§T-10) |
| `@mobile` | exécuté dans le projet Playwright `mobile` (téléphone émulé, écran tactile, §T-9.1) ; avec `@pr`, à chaque PR |
| `@a11y` | parcours au clavier uniquement, et contrôle axe-core |

- **Jalon** : jalon à partir duquel le scénario doit passer (§A-12).
- Les textes entre guillemets sont des **libellés en français** (locale `fr` par défaut dans les tests). Les page objects y accèdent par rôle accessible et libellé, via les clés i18n et non des chaînes en dur.

### 1.3 Organisation du code

```
src/web/e2e/
├─ fixtures/            chargement du projet de référence, utilisateurs, horloge
├─ pages/               page objects : HomePage, TreePanel, PageView, EditorPage,
│                       InsertPalette, CategoryView, PropertyView, SearchPanel…
└─ specs/
   ├─ f01-home.spec.ts
   ├─ f02-tree.spec.ts
   └─ …                 un fichier par fonctionnalité, un `test()` par scénario
```

```ts
test("E2E-05-03 · Insert a link via the palette @pr", async ({ editor }) => { … });
```

### 1.4 Points d'appui de test (mode test uniquement)

Le backend démarré avec `ERABLE_TEST_MODE=1` (jamais actif dans un build Release) expose :

- `POST /_test/reset?fixture=chroniques&slot=<test>` : recharge le projet de référence (copie de fichiers, < 100 ms). Chaque `slot` (un par test) reçoit sa propre copie, avec un `projectId` neuf, et son propre profil (cookie `erable-test-slot`) : les tests s'exécutent en parallèle contre un seul backend ;
- `POST /_test/clock` : fixe l'horloge (`TimeProvider`), pour que les dates (« il y a 2 h ») soient stables ;
- `POST /_test/login-as/{user}` : ouvre une session sans passer par le formulaire. Le formulaire de connexion est testé une seule fois, en `E2E-14-01`.
- `GET /_test/blobs?projectId=…` : liste les blobs stockés (vérification de la déduplication) ;
- `POST /_test/limits` : change la taille maximale des pièces jointes (E2E-06-07).

**Horloge par défaut des tests : 13 octobre 2026, 10:00 (Europe/Paris).**

---

## 2. Jeu de données de référence « Chroniques d'Aube »

Repris du mockup, versionné dans `spec/projects/chroniques/`. C'est un sous-ensemble du projet affiché par le mockup : juste assez d'éléments pour couvrir chaque scénario. **Les ID ci-dessous sont figés** : les tests les utilisent pour vérifier la stabilité des références.

### 2.1 Arbre

```
Pages
├─ Personnages/
│  ├─ PNJ/
│  │  ├─ Ilsa Varenne              3F2A9C14E07B5D21
│  │  │  ├─ Dialogues              (sous-page)
│  │  │  └─ Inventaire             (sous-page)
│  │  └─ Capitaine Orsen           7A1C4E90B3F2D615
│  └─ Créatures/
│     └─ Dragon des Cimes          C47D2B9E0F6A1388
├─ Lieux/
│  ├─ Hautbois                     5B19C0E2A4D7F311
│  └─ Col gelé                     8C3E1F7A90B2D446
├─ Factions/
│  └─ Ordre du Givre               9E04B6A1C8D2F570
└─ Quêtes/
   └─ Le Col gelé                  2D8F5A7C1B9E0463
Catégories
├─ Entité                          E1A0000000000001
│  ├─ Personnage                   E1A0000000000002
│  │  ├─ PNJ                       0D7EA95162C3B804
│  │  │  └─ Marchand · Garde · Érudit
│  │  └─ Joueur
│  └─ Créature
└─ Région · Quête · Faction
Propriétés
├─ Personnages/  Niveau 44B107E9C2A63F58 · Rôle · Âge · Points de vie · Hostile
└─ Monde/        Région · Réputation
Liens
└─ Allié de · Situé à · Mentor de
```

### 2.2 Valeurs notables

| Élément | Données utiles aux scénarios |
|---|---|
| **Ilsa Varenne** | Catégories PNJ + Marchand · `Niveau = 72` (**hors règle**) · `Rôle = Marchande` · `Hostile` hérité de PNJ (`Non`) · `Région = Hautbois` · 4 titres (Présentation, Histoire › Les années de cartographe, Rôle dans le jeu) · références vers Hautbois, Ordre du Givre, Capitaine Orsen, Le Col gelé · texte contenant « Col gelé » · intègre `portrait_ilsa.png` (sa pièce jointe) et `carte_hautbois.webp` (pièce jointe de Hautbois) |
| **Capitaine Orsen** | PNJ + Garde · `Niveau = 40` · référence Ilsa |
| **Dragon des Cimes** | Créature · `Niveau = 99` (**hors règle**) |
| **Hautbois** | Région · pièces jointes `carte_hautbois.webp`, `bande_annonce.mp4`, `plans.zip`, `blason.svg` |
| **Le Col gelé** (quête) | Quête · texte contenant « col gelé » trois fois |
| **Col gelé** (lieu) | Région · nom proche de la quête (tests de recherche) |
| **PNJ** | Parent Personnage · défauts `Hostile = Non`, `Niveau = 10` · sous-catégories Marchand, Garde, Érudit |
| **Niveau** | `int` · règles `greater_or_equal 1`, `less_or_equal 60` · défaut fourni par PNJ, Créature et Joueur · 2 pages hors règles (Ilsa, Dragon) |
| **Rôle** | `enum` : Marchande, Garde, Érudit, Voyageur |
| **Région** | `ref` · règle `in_category Région` |
| **Allié de** | Ilsa → Capitaine Orsen · inverse « Allié de » |
| **Situé à** | Ilsa → Hautbois · inverse « Lieu de » |
| **Mentor de** | aucun lien dans le jeu de données · inverse « Élève de » |

### 2.3 Utilisateurs (mode serveur)

| Identifiant | Rôle sur « Chroniques d'Aube » | Usage |
|---|---|---|
| `admin` | Administrateur | gestion des membres |
| `camille` | Éditrice | édition, multi-utilisateur |
| `malo` | Éditeur | second éditeur concurrent |
| `lea` | Lectrice | droits en lecture seule |
| `externe` | aucun (membre d'un autre projet) | isolation entre projets |

Les mots de passe de test sont dans `spec/projects/users.json`, propre à l'environnement de test.

---

## 3. Catalogue des fonctionnalités

| ID | Fonctionnalité | Écran du mockup | Jalon |
|---|---|---|---|
| F01 | Accueil et gestion des projets | Accueil | M1 |
| F02 | Explorateur (arbre) | toutes | M1 |
| F03 | Identifiants stables | Page, Propriété | M1 |
| F04 | Lecture d'une page | Page | M1 |
| F05 | Édition d'une page | Édition | M1 |
| F06 | Pièces jointes | Page, Édition | M1 |
| F07 | Catégories | Catégorie | M2 |
| F08 | Propriétés et validation | Propriété, Édition | M2 |
| F09 | Liens typés | Page | M2 |
| F10 | Recherche et filtres | Recherche | M3 |
| F11 | Récemment consultées / modifiées | Page, Recherche | M3 |
| F12 | Suppression et corbeille | — | M2 |
| F13 | Historique des révisions | — | M3 |
| F14 | Multi-utilisateur | Page, Édition | M4 |
| F15 | Préférences : langue, thème, raccourcis | Accueil | M1 → M5 |
| F16 | Application desktop | Accueil | M0 → M5 |
| F17 | Accessibilité et clavier | toutes | M1 → M5 |
| F18 | Version mobile (web) | toutes | M6 |
| F19 | Application Android | — | M6 |

---

## F01 — Accueil et gestion des projets

**Règles**
- **F01.R1** Un projet est un dossier contenant `erable.json` (§A-7.1). Ouvrir un dossier sans manifeste propose de créer un projet, sans rien écrire sans confirmation.
- **F01.R2** La liste des projets récents (local et serveur) est stockée dans le profil utilisateur, pas dans le projet. Un projet introuvable reste listé, grisé, avec une action « Retirer ».
- **F01.R3** Un projet d'un `formatVersion` plus ancien est migré à l'ouverture, après confirmation et sauvegarde automatique (`erable.db.bak-<date>`). Un format plus récent que l'application est refusé avec un message clair, sans modification.
- **F01.R4** La langue du contenu est choisie à la création et figée (décision Q1).

```gherkin
E2E-01-01 · Créer un projet vide                              P0 · @pr @desktop · M1
Étant donné  l'écran d'accueil
Quand        je choisis « Nouveau projet », saisis le nom « Atlas » et la langue « Français »
Alors        le projet s'ouvre avec l'explorateur et ses racines Pages, Catégories, Propriétés, Liens
Et           « Atlas » apparaît en tête des projets récents au retour à l'accueil

E2E-01-02 · Rouvrir un projet récent                          P0 · @pr · M1
Étant donné  « Chroniques d'Aube » dans les projets récents
Quand        je clique dessus
Alors        le projet s'ouvre sur la dernière page consultée

E2E-01-03 · Projet récent introuvable                         P1 · @nightly · M1
Étant donné  un projet récent dont le dossier a été supprimé
Alors        il est affiché grisé avec la mention « Introuvable »
Quand        je clique sur « Retirer »
Alors        il disparaît de la liste et aucun fichier n'est créé

E2E-01-04 · Ouvrir un projet d'un format plus récent          P1 · @nightly · M1
Étant donné  un projet dont formatVersion est supérieur à celui de l'application
Quand        je l'ouvre
Alors        un message indique qu'une mise à jour de l'application est nécessaire
Et           les fichiers du projet sont inchangés (empreinte identique)

E2E-01-05 · Migrer un projet d'un format plus ancien          P0 · @nightly · M2
Étant donné  le projet figé spec/projects/legacy/v1
Quand        je l'ouvre et confirme la migration
Alors        une sauvegarde erable.db.bak-* est créée
Et           toutes les pages s'affichent avec leurs références résolues

E2E-01-06 · Se connecter à un serveur                         P0 · @server @desktop · M4
Étant donné  l'écran d'accueil
Quand        je choisis « Se connecter à un serveur », saisis l'URL puis les identifiants de camille
Alors        la liste des projets du serveur s'affiche, avec « Chroniques d'Aube »
Et           le serveur apparaît dans les récents avec le mode « Serveur »
```

---

## F02 — Explorateur (arbre)

**Règles**
- **F02.R1** Quatre racines fixes, non renommables et non supprimables : Pages, Catégories, Propriétés, Liens.
- **F02.R2** Un élément ne peut être déplacé que sous sa propre racine (une catégorie ne va pas dans Pages).
- **F02.R3** Une page peut être déplacée sous une autre page (elle devient sous-page), jamais sous l'une de ses propres descendantes.
- **F02.R4** Renommer ou déplacer ne modifie jamais l'ID et ne casse aucune référence (§A-4).
- **F02.R5** L'état déplié/replié de chaque nœud est mémorisé par utilisateur et par projet.
- **F02.R6** Deux éléments frères peuvent porter le même nom ; un indicateur discret le signale.

```gherkin
E2E-02-01 · Créer un dossier et une page                      P0 · @pr @desktop · M1
Quand        je crée le dossier « Objets » sous Pages, puis la page « Lame de givre » dedans
Alors        l'arbre affiche Pages › Objets › Lame de givre
Et           la page s'ouvre en édition, titre sélectionné

E2E-02-02 · Renommer une page référencée                      P0 · @pr · M1
Étant donné  « Ilsa Varenne » qui référence « Capitaine Orsen »
Quand        je renomme « Capitaine Orsen » en « Commandant Orsen » depuis l'arbre (F2)
Alors        la page Ilsa Varenne affiche « Commandant Orsen » à l'endroit du lien
Et           le Markdown source d'Ilsa est inchangé (contient toujours 7A1C4E90B3F2D615)

E2E-02-03 · Déplacer par glisser-déposer                      P0 · @pr · M1
Quand        je glisse « Hautbois » de Lieux vers Factions
Alors        Hautbois apparaît sous Factions, son fil d'Ariane est mis à jour
Et           les liens qui y mènent depuis Ilsa fonctionnent toujours

E2E-02-04 · Déplacement interdit vers une autre racine        P1 · @pr · M1
Quand        je tente de glisser la catégorie « PNJ » dans Pages
Alors        le dépôt est refusé (curseur interdit), l'arbre est inchangé

E2E-02-05 · Une page ne peut pas devenir sa propre descendante  P1 · @nightly · M1
Quand        je tente de glisser « Ilsa Varenne » sur sa sous-page « Dialogues »
Alors        le dépôt est refusé

E2E-02-06 · Réordonner des frères                             P2 · @nightly · M1
Quand        je glisse « Col gelé » au-dessus de « Hautbois »
Alors        l'ordre est conservé après rechargement

E2E-02-07 · Mémoriser l'état déplié                           P2 · @nightly · M1
Étant donné  Pages › Personnages › PNJ déplié et Lieux replié
Quand        je recharge l'application
Alors        l'arbre est dans le même état

E2E-02-08 · Compteurs des racines                             P2 · @nightly · M1
Alors        chaque racine affiche son nombre d'éléments (hors dossiers), mis à jour après création ou suppression
```

---

## F03 — Identifiants stables

**Règles**
- **F03.R1** Chaque élément reçoit à sa création un ID 64 bits aléatoire, affiché `XXXX XXXX XXXX XXXX`.
- **F03.R2** L'ID est copiable en un clic, au format canonique (16 caractères, sans espaces).
- **F03.R3** L'URL d'un élément contient son ID ; elle reste valide après renommage et déplacement.

```gherkin
E2E-03-01 · Afficher et copier l'ID                           P1 · @pr · M1
Étant donné  la page « Ilsa Varenne »
Alors        l'en-tête affiche « 3F2A 9C14 E07B 5D21 »
Quand        je clique sur l'ID
Alors        le presse-papiers contient « 3F2A9C14E07B5D21 » et une confirmation brève s'affiche

E2E-03-02 · Lien permanent                                    P0 · @pr · M1
Étant donné  l'URL de « Hautbois » enregistrée
Quand        je renomme Hautbois et le déplace sous Factions, puis j'ouvre l'URL enregistrée
Alors        la page Hautbois s'affiche sous son nouveau nom

E2E-03-03 · ID inconnu dans l'URL                             P1 · @nightly · M1
Quand        j'ouvre l'URL d'un ID inexistant
Alors        une page « Élément introuvable » s'affiche avec un accès à la recherche, sans erreur technique
```

---

## F04 — Lecture d'une page

**Règles**
- **F04.R1** Une référence `[[ID]]` affiche le **nom actuel** de la cible ; `[[ID|texte]]` affiche le texte.
- **F04.R2** Une référence vers un élément supprimé ou inexistant s'affiche comme lien cassé (style dédié, infobulle), jamais comme une erreur.
- **F04.R3** La table des matières reflète les titres de niveau 2 et 3 ; un clic fait défiler vers le titre et met à jour l'ancre de l'URL.
- **F04.R4** Le panneau latéral affiche les propriétés (avec leur origine « via <catégorie> »), les liens sortants et entrants, et les pièces jointes.
- **F04.R5** Le HTML brut dans le Markdown n'est pas interprété (§A-9).

```gherkin
E2E-04-01 · Rendu complet d'une page                          P0 · @pr @desktop · M1
Quand        j'ouvre « Ilsa Varenne »
Alors        je vois le fil d'Ariane Pages / Personnages / PNJ / Ilsa Varenne
Et           les pastilles de catégories PNJ et Marchand
Et           « Modifiée il y a 2 h par Camille R. »
Et           les liens Hautbois, Ordre du Givre, Capitaine Orsen rendus avec leur nom
Et           l'image portrait_ilsa.png et l'image carte_hautbois.webp (légende « Pièce jointe de Hautbois »)

E2E-04-02 · Table des matières                                P1 · @pr · M1
Quand        je clique sur « Les années de cartographe » dans le sommaire
Alors        la page défile jusqu'au titre, l'URL se termine par #les-annees-de-cartographe
Et           l'entrée est marquée active dans le sommaire

E2E-04-03 · Aperçu au survol d'une référence                  P2 · @nightly · M1
Quand        je survole le lien « Capitaine Orsen »
Alors        une carte affiche son nom, son ID, son chemin et le début de son contenu

E2E-04-04 · Navigation par référence                          P0 · @pr · M1
Quand        je clique sur « Hautbois » dans le texte
Alors        la page Hautbois s'ouvre et le retour arrière ramène à Ilsa, à la même position de défilement

E2E-04-05 · Lien cassé                                        P1 · @pr · M2
Étant donné  « Ordre du Givre » supprimé
Quand        j'ouvre « Ilsa Varenne »
Alors        le lien est affiché comme cassé avec l'infobulle « Élément supprimé — Restaurer »

E2E-04-06 · Sous-pages                                        P1 · @pr · M1
Alors        la section Sous-pages d'Ilsa liste Dialogues et Inventaire
Quand        je clique sur « Nouvelle sous-page »
Alors        une page enfant d'Ilsa est créée et ouverte en édition

E2E-04-07 · HTML neutralisé                                   P0 · @pr · M1
Étant donné  une page dont le Markdown contient <img src=x onerror="window.__xss=1">
Quand        je l'ouvre
Alors        le code n'est pas exécuté (window.__xss indéfini) et le texte est affiché échappé
```

---

## F05 — Édition d'une page

**Règles**
- **F05.R1** L'éditeur affiche une pastille avec le nom à la place de chaque ID ; une pastille se comporte comme un seul caractère (curseur, suppression).
- **F05.R2** « Afficher les ID bruts » remplace les pastilles par le texte source, sans modifier le contenu.
- **F05.R3** La palette d'insertion s'ouvre en tapant `[[` ou `![[`, ou avec le raccourci configuré (§A-11.3) : `Entrée` insère un lien, `Maj+Entrée` une intégration, `Tab` change de type, `Échap` ferme sans rien insérer.
- **F05.R4** Un brouillon local est enregistré automatiquement ; il est proposé à la réouverture si l'enregistrement n'a pas eu lieu.
- **F05.R5** Quitter l'édition avec des modifications non enregistrées demande une confirmation.
- **F05.R6** Les violations de règles sont signalées en direct, sans bloquer l'enregistrement (§A-5.4).

```gherkin
E2E-05-01 · Modifier et enregistrer                           P0 · @pr @desktop · M1
Étant donné  « Ilsa Varenne » ouverte
Quand        je clique sur « Modifier », ajoute la phrase « Elle parle peu. » puis appuie sur Ctrl+S
Alors        la page repasse en lecture et affiche la phrase
Et           « Modifiée à l'instant par moi » s'affiche

E2E-05-02 · Pastilles à la place des ID                       P0 · @pr · M1
Étant donné  « Ilsa Varenne » en édition
Alors        l'éditeur affiche des pastilles « Hautbois », « Capitaine Orsen » et aucun ID hexadécimal
Quand        je place le curseur juste après la pastille « Hautbois » et appuie sur Retour arrière
Alors        la pastille entière est supprimée, rien d'autre

E2E-05-03 · Insérer un lien via la palette                    P0 · @pr · M1
Quand        je tape « [[orse »
Alors        la palette s'ouvre avec « Capitaine Orsen » en premier, son chemin et son ID
Quand        j'appuie sur Entrée
Alors        une pastille « Capitaine Orsen » est insérée et la source contient [[7A1C4E90B3F2D615]]

E2E-05-04 · Intégrer une pièce jointe d'une autre page        P0 · @pr · M1
Quand        je tape « ![[carte », choisis « Hautbois › carte_hautbois.webp » et appuie sur Maj+Entrée
Alors        la source contient ![[<ID de carte_hautbois.webp>]]
Et           l'aperçu affiche l'image

E2E-05-05 · Filtrer la palette par type                       P1 · @pr · M1
Quand        j'ouvre la palette et appuie trois fois sur Tab
Alors        l'onglet « Catégories » est actif et seules des catégories sont proposées

E2E-05-06 · Fermer la palette sans insérer                    P1 · @nightly · M1
Quand        j'ouvre la palette puis appuie sur Échap
Alors        la palette se ferme et seul le texte tapé (« [[ ») reste dans l'éditeur

E2E-05-07 · Raccourci configurable                            P2 · @nightly · M5
Étant donné  le raccourci de la palette réglé sur Ctrl+Maj+L dans les préférences
Quand        j'appuie sur Ctrl+Maj+L dans l'éditeur
Alors        la palette s'ouvre

E2E-05-08 · Afficher les ID bruts                             P1 · @pr · M1
Quand        j'active « Afficher les ID bruts »
Alors        l'éditeur montre [[7A1C4E90B3F2D615]] à la place de la pastille
Et           le contenu enregistré est identique avant et après bascule

E2E-05-09 · Brouillon récupéré                                P0 · @pr · M1
Étant donné  une modification non enregistrée de « Hautbois »
Quand        je ferme brutalement l'onglet puis rouvre Hautbois en édition
Alors        le bandeau « Un brouillon non enregistré existe » propose « Restaurer » ou « Ignorer »
Quand        je choisis « Restaurer »
Alors        ma modification est de retour

E2E-05-10 · Garde-fou avant de quitter                        P0 · @pr · M1
Étant donné  une modification non enregistrée
Quand        je clique sur une autre page dans l'arbre
Alors        une confirmation propose « Enregistrer », « Abandonner » ou « Annuler »

E2E-05-11 · Annuler l'édition                                 P1 · @pr · M1
Quand        je modifie le texte puis clique sur « Annuler » et confirme
Alors        la page revient à son contenu enregistré et le brouillon est supprimé

E2E-05-12 · Validation en direct non bloquante                P0 · @pr · M2
Étant donné  « Ilsa Varenne » en édition, panneau Propriétés ouvert
Alors        « Niveau » affiche « Doit être inférieur ou égal à 60 » et la barre d'état « 1 avertissement de validation »
Quand        j'enregistre
Alors        l'enregistrement réussit et la page porte un badge « hors règles »
Quand        je remplace 72 par 55
Alors        l'erreur disparaît immédiatement, avant enregistrement

E2E-05-13 · Barre d'outils de mise en forme                   P2 · @nightly · M1
Quand        je sélectionne un mot et clique sur « B »
Alors        le mot est entouré de ** dans la source
```

---

## F06 — Pièces jointes

**Règles**
- **F06.R1** Un fichier collé ou déposé dans l'éditeur devient une pièce jointe de la page courante, et `![[ID]]` est inséré au curseur.
- **F06.R2** Images, vidéos, audio et PDF sont intégrés ; les autres types s'affichent en carte téléchargeable. **SVG et HTML ne sont jamais rendus inline** (§A-9).
- **F06.R3** Les vidéos sont lues en streaming (avance rapide sans tout télécharger).
- **F06.R4** Supprimer une pièce jointe référencée ailleurs affiche la liste des pages concernées avant confirmation.
- **F06.R5** La taille maximale est configurable ; un dépassement est refusé avec un message, sans fichier partiel.

```gherkin
E2E-06-01 · Coller une image                                  P0 · @pr @desktop · M1
Étant donné  « Hautbois » en édition
Quand        je colle une image PNG depuis le presse-papiers
Alors        une pastille de pièce jointe apparaît au curseur
Et           l'image figure dans le panneau « Pièces jointes » de Hautbois

E2E-06-02 · Déposer plusieurs fichiers                        P1 · @pr · M1
Quand        je dépose trois fichiers (PNG, PDF, ZIP) dans l'éditeur
Alors        trois pièces jointes sont créées avec une progression visible
Et           l'image et le PDF sont intégrés, le ZIP affiché en carte

E2E-06-03 · Ajouter depuis le panneau                         P1 · @pr · M1
Quand        je clique « + Ajouter » dans Pièces jointes et choisis un fichier
Alors        il apparaît avec son nom, son extension et sa taille, sans être inséré dans le texte

E2E-06-04 · Lecture vidéo en streaming                        P1 · @nightly · M1
Quand        j'intègre bande_annonce.mp4 et avance la lecture à 80 %
Alors        la vidéo reprend à cette position (requête Range 206 observée)

E2E-06-05 · SVG non rendu inline                              P0 · @pr · M1
Quand        j'intègre blason.svg dans une page
Alors        il s'affiche en carte téléchargeable et n'est pas inséré dans le DOM sous forme <svg> ou <object>

E2E-06-06 · Supprimer une pièce jointe référencée             P1 · @pr · M2
Quand        je supprime carte_hautbois.webp depuis Hautbois
Alors        la confirmation indique « Utilisée dans 1 autre page : Ilsa Varenne »
Quand        je confirme
Alors        Ilsa affiche un emplacement « Pièce jointe supprimée » à la place de l'image

E2E-06-07 · Fichier trop volumineux                           P1 · @nightly · M1
Étant donné  une limite de 10 Mo
Quand        je dépose un fichier de 11 Mo
Alors        un message indique la limite et aucune pièce jointe n'est créée

E2E-06-08 · Fichier identique dédupliqué                      P2 · @nightly · M1
Quand        j'attache la même image à deux pages
Alors        deux pièces jointes distinctes existent, mais un seul blob est stocké (vérifié via /_test)
```

---

## F07 — Catégories

**Règles**
- **F07.R1** Une catégorie a une description en texte simple, un parent optionnel unique et des valeurs de propriétés par défaut.
- **F07.R2** Un reparentage qui créerait un cycle est refusé.
- **F07.R3** Une page peut avoir plusieurs catégories (décision Q4). Résolution d'une valeur : page > catégorie la plus spécifique > défaut de la propriété. Deux catégories sans lien d'héritage avec des valeurs différentes → **conflit** à résoudre sur la page (§A-5.3).
- **F07.R4** « Pages de la catégorie » peut inclure les sous-catégories.

```gherkin
E2E-07-01 · Consulter une catégorie                           P0 · @pr · M2
Quand        j'ouvre la catégorie « PNJ »
Alors        je vois sa description, l'héritage Entité › Personnage › PNJ
Et           les sous-catégories Marchand, Garde, Érudit
Et           le tableau des propriétés avec leur source (PNJ ou héritée)

E2E-07-02 · Créer une catégorie avec parent                   P0 · @pr · M2
Quand        je crée « Forgeron » avec le parent « PNJ » et la description « Artisan du métal »
Alors        Forgeron hérite des valeurs par défaut de PNJ (Hostile = Non, source « PNJ »)

E2E-07-03 · Cycle refusé                                      P0 · @pr · M2
Quand        je choisis « Marchand » comme parent de « Personnage »
Alors        le choix est refusé avec « Créerait une boucle : Personnage › PNJ › Marchand › Personnage »

E2E-07-04 · Ajouter une catégorie à une page                  P0 · @pr · M2
Quand        j'ajoute la catégorie « Érudit » à « Capitaine Orsen »
Alors        la page affiche la pastille Érudit et ses propriétés héritées

E2E-07-05 · Valeur héritée puis redéfinie                     P0 · @pr · M2
Étant donné  « Capitaine Orsen » avec Hostile hérité de PNJ (Non)
Quand        je fixe Hostile = Oui sur la page
Alors        la valeur affichée est Oui sans mention « via »
Quand        je clique sur « Rétablir la valeur héritée »
Alors        la valeur redevient Non, « via PNJ »

E2E-07-06 · Conflit entre catégories                          P0 · @pr · M2
Étant donné  une catégorie « Montagnard » sans parent, avec Hostile = Oui par défaut
Quand        j'ajoute Montagnard à « Ilsa Varenne » (déjà PNJ, Hostile = Non)
Alors        Hostile est marqué « En conflit : PNJ (Non) / Montagnard (Oui) »
Quand        je choisis Non sur la page
Alors        le conflit disparaît

E2E-07-07 · Pages de la catégorie avec sous-catégories        P1 · @pr · M2
Quand        j'ouvre PNJ et coche « Inclure les sous-catégories »
Alors        la liste comprend Ilsa (via Marchand) et Orsen (via Garde)
Quand        je clique sur « Voir dans la recherche »
Alors        la recherche s'ouvre filtrée sur PNJ avec sous-catégories

E2E-07-08 · Modifier un défaut se propage                     P1 · @nightly · M2
Quand        je change le Niveau par défaut de PNJ de 10 à 15
Alors        une page PNJ sans Niveau propre affiche 15, une page avec sa propre valeur est inchangée
```

---

## F08 — Propriétés et validation

**Règles**
- **F08.R1** Une définition a un nom affiché, une clé unique, une description, un type, une valeur par défaut et des règles (§A-5.4).
- **F08.R2** Les règles proposées dépendent du type ; aucune règle pour `bool`.
- **F08.R3** Les règles sont vérifiées à chaque enregistrement ; une violation **avertit sans bloquer** et est listée sur la définition (« N pages hors règles »).
- **F08.R4** Modifier une règle recalcule les violations de toutes les pages concernées.
- **F08.R5** Renommer une option d'`enum` met à jour les pages qui l'utilisent ; supprimer une option utilisée demande une option de remplacement.
- **F08.R6** Le changement de type d'une propriété déjà renseignée n'est autorisé que si les valeurs existantes sont convertibles ; sinon la liste des pages bloquantes est affichée.

```gherkin
E2E-08-01 · Consulter une définition                          P0 · @pr · M2
Quand        j'ouvre la propriété « Niveau »
Alors        je vois le type int, les règles « ≥ 1 » et « ≤ 60 »
Et           « Utilisée par » : PNJ, Créature, Joueur · « Renseignée sur N pages »
Et           « 2 pages hors règles » : Ilsa Varenne (72), Dragon des Cimes (99)

E2E-08-02 · Créer une propriété typée                         P0 · @pr · M2
Quand        je crée « Poids » de type float avec la règle « greater_than 0 »
Alors        elle est proposée dans « Ajouter une propriété » d'une page
Et           saisir -2 sur une page affiche « Doit être supérieur à 0 »

E2E-08-03 · Règles proposées selon le type                    P1 · @pr · M2
Quand        je change le type de la nouvelle propriété en string
Alors        les règles proposées sont min_length, max_length, match, not_empty
Quand        je le change en bool
Alors        le texte « Aucune règle disponible pour un booléen » s'affiche

E2E-08-04 · Tester une valeur                                 P1 · @pr · M2
Quand        je saisis 72 dans « Tester une valeur » de Niveau
Alors        le résultat indique « Échoue : less_or_equal 60 »
Quand        je saisis 30
Alors        le résultat indique « Valide »

E2E-08-05 · Modifier une règle recalcule les violations       P0 · @pr · M2
Quand        je remplace « ≤ 60 » par « ≤ 80 » sur Niveau
Alors        « 1 page hors règles » : Dragon des Cimes
Et           le badge « hors règles » disparaît de la page Ilsa Varenne

E2E-08-06 · Saisie par type sur une page                      P0 · @pr · M2
Étant donné  « Capitaine Orsen » en édition
Alors        Hostile s'édite par interrupteur, Rôle par liste, Région par sélecteur de page filtré sur la catégorie Région
Et           Âge refuse la saisie de lettres

E2E-08-07 · Règle « match » sur une chaîne                    P1 · @nightly · M2
Étant donné  une propriété « Code » string avec match ^[A-Z]{3}-\d{2}$
Quand        je saisis « ab-1 » puis « ABC-12 »
Alors        la première valeur est signalée, la seconde est valide

E2E-08-08 · Renommer une option d'enum                        P1 · @nightly · M2
Quand        je renomme l'option « Marchande » de Rôle en « Négociante »
Alors        Ilsa Varenne affiche Rôle = Négociante sans autre action

E2E-08-09 · Supprimer une option d'enum utilisée              P1 · @nightly · M2
Quand        je supprime l'option « Garde » de Rôle
Alors        une boîte demande l'option de remplacement pour les pages concernées (Capitaine Orsen)

E2E-08-10 · Changement de type incompatible                   P1 · @nightly · M2
Étant donné  Rôle renseigné avec des valeurs textuelles
Quand        je change son type en int
Alors        le changement est refusé avec la liste des pages dont la valeur n'est pas convertible

E2E-08-11 · Renommer la clé d'une propriété                   P1 · @pr · M2
Quand        je renomme la clé « niveau » en « level »
Alors        toutes les pages, catégories et filtres enregistrés qui utilisent Niveau fonctionnent toujours
```

---

## F09 — Liens typés

**Règles**
- **F09.R1** Un type de lien a un nom, une description et un libellé inverse optionnel.
- **F09.R2** Un lien relie deux pages quelconques, indépendamment des dossiers ; il est visible depuis la source (sortant) et la cible (entrant, avec le libellé inverse).
- **F09.R3** Supprimer un type de lien utilisé affiche le nombre de liens concernés avant confirmation.

```gherkin
E2E-09-01 · Créer un type de lien                             P0 · @pr · M2
Quand        je crée le type « Rival de » avec l'inverse « Rival de »
Alors        il apparaît sous la racine Liens

E2E-09-02 · Lier deux pages                                   P0 · @pr · M2
Étant donné  « Ilsa Varenne » ouverte
Quand        je clique « + Lier », choisis « Mentor de » puis « Capitaine Orsen »
Alors        le panneau Liens d'Ilsa affiche « Mentor de · Capitaine Orsen »
Et           la page Capitaine Orsen affiche « Élève de · Ilsa Varenne » parmi ses liens entrants

E2E-09-03 · Liens entrants                                    P1 · @pr · M2
Quand        j'ouvre « Hautbois »
Alors        les liens entrants comprennent « Lieu de · Ilsa Varenne »
Et           les références implicites (pages qui mentionnent Hautbois dans leur texte) sont listées séparément

E2E-09-04 · Supprimer un lien                                 P1 · @nightly · M2
Quand        je supprime le lien « Allié de » d'Ilsa vers Orsen
Alors        il disparaît des deux pages

E2E-09-05 · Supprimer un type de lien utilisé                 P1 · @nightly · M2
Quand        je supprime le type « Situé à »
Alors        la confirmation indique « 1 lien utilise ce type et sera supprimé »
```

---

## F10 — Recherche et filtres

**Règles**
- **F10.R1** `Ctrl+K` ouvre une recherche rapide par nom sur tous les types ; `Entrée` ouvre le premier résultat.
- **F10.R2** La recherche plein texte ignore la casse et les accents (« col gele » trouve « Col gelé »), et met en évidence les passages trouvés.
- **F10.R3** Les filtres se combinent (ET) : type, catégories (avec sous-catégories optionnelles), conditions sur propriétés (valeurs héritées incluses), date de modification, « hors règles ».
- **F10.R4** Tri par pertinence, dernière modification ou nom ; l'état de la recherche est reflété dans l'URL (partageable).

```gherkin
E2E-10-01 · Recherche rapide                                  P0 · @pr @desktop · M3
Quand        j'appuie sur Ctrl+K et tape « ilsa »
Alors        « Ilsa Varenne » est le premier résultat
Quand        j'appuie sur Entrée
Alors        la page s'ouvre et la recherche se ferme

E2E-10-02 · Plein texte insensible aux accents                P0 · @pr · M3
Quand        je recherche « col gele »
Alors        les résultats comprennent la quête « Le Col gelé », le lieu « Col gelé » et Ilsa Varenne
Et           les passages contenant « Col gelé » sont surlignés

E2E-10-03 · Filtrer par type et catégorie                     P0 · @pr · M3
Quand        je filtre Type = Pages, Catégorie = PNJ avec sous-catégories
Alors        seules Ilsa Varenne et Capitaine Orsen sont listées

E2E-10-04 · Condition sur une propriété héritée               P0 · @pr · M3
Quand        j'ajoute la condition « Hostile = Non »
Alors        Ilsa et Orsen sont listés (valeur héritée de PNJ)

E2E-10-05 · Condition numérique                               P1 · @pr · M3
Quand        j'ajoute la condition « Niveau > 50 »
Alors        Ilsa (72) et Dragon des Cimes (99) sont listés, pas Orsen (40)

E2E-10-06 · Filtre « hors règles »                            P1 · @nightly · M3
Quand        j'active le filtre « Hors règles »
Alors        seules les pages en violation sont listées, avec la règle concernée

E2E-10-07 · Filtre de date                                    P1 · @nightly · M3
Étant donné  l'horloge fixée et des dates de modification connues
Quand        je choisis « Modifié : 24 h »
Alors        seules les pages modifiées depuis le 12 octobre 2026 10:00 sont listées

E2E-10-08 · Tri et URL partageable                            P2 · @nightly · M3
Quand        je trie par « Nom » puis copie l'URL et l'ouvre dans un nouvel onglet
Alors        la même recherche, avec les mêmes filtres et le même tri, s'affiche

E2E-10-09 · Aucun résultat                                    P2 · @nightly · M3
Quand        je recherche « zzzz »
Alors        un message « Aucun résultat » propose de réinitialiser les filtres

E2E-10-10 · Réinitialiser les filtres                         P2 · @nightly · M3
Quand        je clique sur « Réinitialiser »
Alors        tous les filtres sont retirés, le texte de recherche est conservé

E2E-10-11 · Élément créé immédiatement trouvable              P0 · @pr · M3
Quand        je crée la page « Lanterne d'ambre » puis recherche « ambre »
Alors        elle apparaît dans les résultats sans délai d'indexation perceptible
```

---

## F11 — Récemment consultées / modifiées

**Règles**
- **F11.R1** « Récemment consultées » est **propre à chaque utilisateur** ; une consultation est comptée après 2 secondes d'affichage.
- **F11.R2** « Récemment modifiées » est commun à tous, avec l'auteur et la date.

```gherkin
E2E-11-01 · Récemment consultées                              P1 · @pr · M3
Quand        je consulte PNJ, puis Hautbois, puis Niveau
Alors        la liste « Récemment consultées » affiche Niveau, Hautbois, PNJ dans cet ordre, avec leur type

E2E-11-02 · Récemment modifiées                               P1 · @pr · M3
Quand        je modifie « Hautbois »
Alors        Hautbois apparaît en tête de « Modifiées » avec « moi · à l'instant »

E2E-11-03 · Consultations propres à chaque utilisateur        P1 · @multi @server · M4
Étant donné  camille a consulté Hautbois
Alors        Hautbois n'apparaît pas dans les consultées de malo
Mais         une modification de camille apparaît dans les modifiées de malo
```

---

## F12 — Suppression et corbeille

**Règles**
- **F12.R1** Supprimer un élément le place dans la corbeille ; ses sous-pages et le contenu de ses dossiers suivent.
- **F12.R2** Avant suppression, si l'élément est référencé, la liste des éléments qui y font référence est affichée.
- **F12.R3** Restaurer replace l'élément à son emplacement d'origine (ou à la racine si le parent n'existe plus) avec le même ID ; les références redeviennent valides.
- **F12.R4** Supprimer une catégorie retire la catégorie des pages, sans supprimer les pages. Supprimer une propriété retire ses valeurs, restaurables avec elle.
- **F12.R5** Vider la corbeille est définitif et demande une confirmation explicite.

```gherkin
E2E-12-01 · Supprimer une page référencée                     P0 · @pr · M2
Quand        je supprime « Capitaine Orsen »
Alors        la confirmation indique « 1 page fait référence à cet élément : Ilsa Varenne »
Quand        je confirme
Alors        Orsen disparaît de l'arbre et apparaît dans la corbeille

E2E-12-02 · Restaurer depuis la corbeille                     P0 · @pr · M2
Étant donné  « Capitaine Orsen » dans la corbeille
Quand        je le restaure
Alors        il revient sous PNJ avec le même ID et le lien depuis Ilsa fonctionne

E2E-12-03 · Suppression en cascade des sous-pages             P1 · @pr · M2
Quand        je supprime « Ilsa Varenne »
Alors        la confirmation mentionne « et 2 sous-pages »
Et           la restauration d'Ilsa restaure aussi Dialogues et Inventaire

E2E-12-04 · Supprimer une catégorie                           P1 · @nightly · M2
Quand        je supprime la catégorie « Marchand »
Alors        Ilsa Varenne existe toujours, sans pastille Marchand

E2E-12-05 · Restaurer quand le parent a disparu               P2 · @nightly · M2
Étant donné  « Hautbois » supprimé, puis le dossier « Lieux » supprimé et effacé définitivement de la corbeille
Quand        je restaure Hautbois
Alors        il est replacé à la racine Pages, avec un message l'indiquant

E2E-12-06 · Vider la corbeille                                P1 · @nightly · M2
Quand        je vide la corbeille et confirme en tapant le nom du projet
Alors        la corbeille est vide et les éléments ne sont plus restaurables
```

---

## F13 — Historique des révisions

**Règles**
- **F13.R1** Chaque enregistrement crée une révision (auteur, date, contenu, catégories, propriétés).
- **F13.R2** Deux révisions peuvent être comparées (diff du texte et des propriétés).
- **F13.R3** Restaurer une révision crée une **nouvelle** révision ; l'historique n'est jamais réécrit.

```gherkin
E2E-13-01 · Consulter l'historique                            P1 · @pr · M3
Étant donné  « Hautbois » modifiée trois fois
Quand        j'ouvre son historique
Alors        quatre révisions sont listées avec auteur et date, la plus récente en tête

E2E-13-02 · Comparer deux révisions                           P1 · @pr · M3
Quand        je compare la révision 1 et la révision 4
Alors        les ajouts et suppressions de texte sont mis en évidence, ainsi que les propriétés modifiées

E2E-13-03 · Restaurer une révision                            P0 · @pr · M3
Quand        je restaure la révision 2
Alors        le contenu de la page est celui de la révision 2
Et           l'historique compte cinq révisions, la cinquième marquée « Restauration de la révision 2 »
```

---

## F14 — Multi-utilisateur (mode serveur)

**Règles**
- **F14.R1** Les droits sont **par projet uniquement** (décision Q5) : lecteur, éditeur, administrateur.
- **F14.R2** Un utilisateur n'a aucun accès, même en lecture, à un projet dont il n'est pas membre.
- **F14.R3** La présence (qui consulte ou modifie quelle page) est visible en temps réel.
- **F14.R4** Une modification enregistrée par un utilisateur est visible chez les autres sans rechargement.
- **F14.R5** Deux enregistrements concurrents sont fusionnés automatiquement s'ils touchent des parties différentes ; sinon une vue de résolution s'ouvre. **Aucun texte n'est perdu** (§A-8.3). La co-édition en direct est hors v1 (décision Q3).

```gherkin
E2E-14-01 · Connexion et déconnexion                          P0 · @server · M4
Quand        je me connecte avec les identifiants de camille
Alors        son avatar s'affiche dans la barre supérieure
Quand        je me déconnecte
Alors        l'écran de connexion s'affiche et l'API répond 401 avec l'ancien cookie

E2E-14-02 · Identifiants invalides                            P1 · @server · M4
Quand        je saisis un mauvais mot de passe
Alors        le message « Identifiant ou mot de passe incorrect » s'affiche, sans préciser lequel

E2E-14-03 · Lecteur en lecture seule                          P0 · @server · M4
Étant donné  lea connectée
Alors        aucun bouton « Modifier », « + Ajouter », « + Lier » ni action de l'arbre n'est proposé
Et           une requête PUT forgée vers une page reçoit 403

E2E-14-04 · Isolation entre projets                           P0 · @server · M4
Étant donné  externe connecté
Alors        « Chroniques d'Aube » n'apparaît pas dans sa liste de projets
Et           l'URL directe d'Ilsa Varenne affiche « Accès refusé », sans fuite du nom de la page

E2E-14-05 · Présence                                          P1 · @multi @server · M4
Étant donné  camille et malo connectés
Quand        camille ouvre « Ilsa Varenne » en édition sur la section Histoire
Alors        malo voit l'avatar CR sur la page et le bandeau « Camille R. modifie aussi cette page — section Histoire »
Quand        camille ferme l'onglet
Alors        le bandeau disparaît chez malo en moins de 30 secondes

E2E-14-06 · Propagation sans rechargement                     P0 · @multi @server · M4
Étant donné  malo consulte « Hautbois »
Quand        camille modifie et enregistre Hautbois
Alors        malo voit le nouveau contenu sans recharger
Et           un nouvel élément créé par camille apparaît dans l'arbre de malo

E2E-14-07 · Enregistrements concurrents fusionnés             P0 · @multi @server · M4
Étant donné  camille et malo éditent « Ilsa Varenne » depuis la même révision
Quand        camille modifie la section Présentation et enregistre
Et           malo modifie la section Histoire et enregistre ensuite
Alors        l'enregistrement de malo réussit avec la notification « Fusionné avec les modifications de Camille R. »
Et           la page contient les deux modifications

E2E-14-08 · Conflit réel                                      P0 · @multi @server · M4
Étant donné  camille et malo modifient la même phrase différemment
Quand        camille enregistre, puis malo enregistre
Alors        malo voit la vue de résolution avec les deux versions de la phrase
Quand        malo choisit sa version et enregistre
Alors        la page contient la version de malo, et la version de camille est consultable dans l'historique

E2E-14-09 · Session expirée pendant l'édition                 P0 · @server · M4
Étant donné  camille en édition avec des modifications non enregistrées
Quand        sa session expire et elle appuie sur Ctrl+S
Alors        une boîte de reconnexion s'affiche
Quand        elle se reconnecte
Alors        l'enregistrement aboutit avec ses modifications intactes

E2E-14-10 · Gestion des membres                               P1 · @server · M4
Étant donné  admin connecté
Quand        il passe lea du rôle Lecteur à Éditeur
Alors        lea voit le bouton « Modifier » après rafraîchissement de sa session
Et           un éditeur n'a pas accès à l'écran de gestion des membres
```

---

## F15 — Préférences : langue, thème, raccourcis

**Règles**
- **F15.R1** La langue d'interface (Français, English, Deutsch, Español) est indépendante de la langue du contenu (décision Q1).
- **F15.R2** Thème Clair, Sombre ou Système ; le mode Système suit le réglage de l'OS en direct.
- **F15.R3** Les préférences sont mémorisées par utilisateur (profil local ou compte serveur) et appliquées avant le premier affichage (pas de flash de thème).
- **F15.R4** Les raccourcis sont configurables ; une combinaison déjà prise est signalée.

```gherkin
E2E-15-01 · Changer la langue d'interface                     P0 · @pr · M1
Quand        je choisis « English » dans les préférences
Alors        l'interface est en anglais (« Explorer », « Edit », « Properties »)
Et           le contenu des pages et les noms des éléments sont inchangés
Et           les dates sont formatées à l'anglaise

E2E-15-02 · Thème sombre                                      P0 · @pr · M1
Quand        je choisis « Sombre »
Alors        l'attribut data-theme vaut dark et le fond de page est sombre
Et           après rechargement, le premier rendu est directement sombre

E2E-15-03 · Thème système                                     P1 · @nightly · M1
Étant donné  le thème « Système » et l'OS en clair
Quand        l'OS passe en sombre (émulation prefers-color-scheme)
Alors        l'application passe en sombre sans rechargement

E2E-15-04 · Conflit de raccourci                              P2 · @nightly · M5
Quand        j'attribue Ctrl+K à la palette d'insertion
Alors        un avertissement indique « Déjà utilisé par : Recherche » et propose de remplacer
```

---

## F16 — Application desktop

Ces scénarios s'exécutent **sur le binaire packagé** (§T-10) : via CDP sous Windows, via `--self-test` sous macOS et Linux.

**Règles**
- **F16.R1** Le backend embarqué n'écoute que sur la boucle locale, sur un port aléatoire, et exige le jeton de session (§A-9).
- **F16.R2** Un même projet ne peut pas être ouvert par deux instances : la seconde met la première au premier plan.
- **F16.R3** À la fermeture, aucune modification n'est perdue et aucun processus ne subsiste.
- **F16.R4** En mode connecté, une version de serveur incompatible est détectée avant toute opération (§A-2).

```gherkin
E2E-16-01 · Démarrage et parcours de fumée                    P0 · @desktop · M0
Quand        je lance l'application installée
Alors        l'écran d'accueil s'affiche en moins de 3 secondes (10 s sur les runners partagés de la CI, testing.md §10)
Et           le scénario de fumée (créer un projet, une page, une référence, une image, rechercher) réussit

E2E-16-02 · Serveur local protégé                             P0 · @desktop · M0
Alors        le port d'écoute est lié à 127.0.0.1 uniquement
Et           une requête HTTP sans jeton, ou avec un en-tête Host différent, est refusée

E2E-16-03 · Ouvrir un dossier avec le sélecteur natif         P1 · @desktop · M1
Quand        je choisis « Ouvrir un dossier… » (Ctrl+O) et sélectionne le dossier du projet
Alors        le projet s'ouvre

E2E-16-04 · Deuxième instance                                 P1 · @desktop · M1
Étant donné  « Chroniques d'Aube » ouvert
Quand        je lance une seconde instance sur le même projet
Alors        la première fenêtre passe au premier plan et la seconde se ferme

E2E-16-05 · Fermeture propre                                  P0 · @desktop · M1
Étant donné  une page en édition avec des modifications non enregistrées
Quand        je ferme la fenêtre
Alors        la confirmation « Enregistrer / Abandonner / Annuler » s'affiche
Quand        je choisis « Enregistrer »
Alors        la fenêtre se ferme, aucun processus Erable ne subsiste et la modification est présente à la réouverture

E2E-16-06 · Serveur incompatible                              P1 · @desktop @server · M4
Étant donné  un serveur exposant une version d'API non supportée
Quand        je m'y connecte
Alors        un message indique les versions en présence, sans aucune autre requête vers le serveur

E2E-16-07 · Glisser un dossier sur l'accueil                  P2 · @desktop · M1
Quand        je glisse le dossier d'un projet sur l'écran d'accueil
Alors        le projet s'ouvre
```

---

## F17 — Accessibilité et clavier

Ces scénarios complètent la vérification axe-core de chaque écran (§T-9.3).

```gherkin
E2E-17-01 · Navigation dans l'arbre au clavier                P1 · @a11y · M1
Quand        je place le focus dans l'arbre et utilise ↑ ↓ → ← Entrée F2
Alors        je parcours, déplie, replie, ouvre et renomme sans souris
Et           le lecteur d'écran annonce les nœuds (role treeitem, aria-expanded, aria-level)

E2E-17-02 · Écrire une page sans souris                       P1 · @a11y · M3
Quand        j'ouvre une page avec Ctrl+K, passe en édition, insère un lien avec la palette et enregistre avec Ctrl+S
Alors        tout le parcours se fait au clavier, avec un focus toujours visible

E2E-17-03 · Fenêtres modales                                  P1 · @a11y · M1
Quand        une boîte de confirmation s'ouvre
Alors        le focus y est piégé, Échap la ferme et le focus revient à l'élément d'origine

E2E-17-04 · Aucune violation axe-core                         P0 · @a11y @nightly · M5
Alors        chaque écran du mockup, en clair et en sombre, ne présente aucune violation « serious » ou « critical »
```

---

## F18 — Version mobile (web)

La même SPA, adaptée aux téléphones : pas de seconde interface. La **mise en page étroite** s'applique sous 720 px de large ; les règles tactiles s'appliquent dès que le pointeur principal est grossier (`pointer: coarse`), quelle que soit la largeur. Le mockup ne décrit que le bureau : la mise en page mobile reprend ses composants, ses tokens et ses écrans, empilés (§A-3.1).

**Règles**
- **F18.R1** En mise en page étroite, l'explorateur devient un **tiroir** superposé au contenu : fermé à l'ouverture d'un écran, refermé après chaque navigation, par Échap, en touchant le fond ou par son bouton ; y toucher un dossier ou une racine le déplie au lieu de l'ouvrir (une page s'ouvre) ; le focus y est piégé tant qu'il est ouvert. L'état « ouvert » et la largeur du panneau du bureau ne sont ni lus ni modifiés.
- **F18.R2** La barre supérieure tient sur une ligne : accueil, explorateur, nom du projet, recherche (icône), menu « Plus » qui regroupe précédent / suivant, langue, thème, préférences et compte. La barre d'état se réduit à l'état de connexion et au mode.
- **F18.R3** **Aucun défilement horizontal** de l'écran à 360 px de large, sur tous les écrans : seuls les blocs larges (tableaux, code, comparaison de révisions, grilles) défilent dans leur propre cadre. Le texte se coupe entre les mots, jamais au milieu (sauf ID et URL trop longs).
- **F18.R4** Avec un pointeur grossier, toute cible interactive offre une zone de toucher d'au moins **44 × 44 px**, et aucune action n'est accessible seulement au survol ou au clic droit : les éléments de l'arbre et les lignes de liste ont un bouton « … » qui ouvre le même menu que le clic droit (un appui long l'ouvre aussi).
- **F18.R5** Un élément de l'arbre se **déplace sans glisser-déposer** : l'action « Déplacer vers… » de son menu propose les destinations valides (même racine, pas sous lui-même) et produit le même déplacement que le glisser-déposer (F02).
- **F18.R6** L'édition reste complète : la barre d'outils défile horizontalement, « Annuler » et « Enregistrer » restent visibles quand le clavier virtuel est ouvert, et le panneau de la page (propriétés, catégories, liens) passe sous un onglet au-dessus de l'éditeur au lieu de la colonne latérale. La palette d'insertion tient dans l'écran au-dessus du clavier.
- **F18.R7** En mise en page étroite, la recherche rapide et les boîtes de dialogue occupent tout l'écran (les confirmations courtes restent des cartes centrées) ; les filtres de l'écran Recherche s'ouvrent dans un panneau plein écran, refermé par « Afficher les N résultats », pour en combiner plusieurs.
- **F18.R8** La SPA servie par un serveur est **installable** comme application web : manifeste (nom, icônes, couleur de thème, `display: standalone`, `start_url` à la racine). Pas de mode hors ligne ni de service worker, et toujours aucun état durable dans le navigateur (décision Q7).

```gherkin
E2E-18-01 · Explorateur en tiroir                             P0 · @mobile @pr · M6
Étant donné  la page « Ilsa Varenne » ouverte sur un téléphone
Alors        l'explorateur est masqué et la page occupe toute la largeur
Quand        j'ouvre l'explorateur et touche « Hautbois »
Alors        la page « Hautbois » s'affiche et le tiroir est refermé
Et           sur un écran large, l'explorateur est toujours ouvert à sa largeur d'avant

E2E-18-02 · Aucun défilement horizontal                       P1 · @mobile · M6
Quand        je parcours l'accueil, une page en lecture et en édition, une catégorie, une propriété, la recherche, la corbeille, l'historique et les membres
Alors        la largeur du document ne dépasse jamais celle de l'écran

E2E-18-03 · Lire une page sur un téléphone                    P1 · @mobile @pr · M6
Étant donné  la page « Ilsa Varenne » ouverte sur un téléphone
Alors        le titre, les catégories et le début du texte sont visibles sans défiler, le titre coupé entre les mots
Quand        je déplie « Panneau de la page »
Alors        je vois « Hostile » hérité « via PNJ » et le lien « Allié de » vers « Capitaine Orsen »
Quand        je touche le lien « Hautbois » du texte
Alors        la page « Hautbois » s'affiche

E2E-18-04 · Modifier et enregistrer une page                  P0 · @mobile @pr · M6
Étant donné  la page « Ilsa Varenne » ouverte sur un téléphone
Quand        je touche « Modifier », tape « [[orse » puis touche « Capitaine Orsen » dans la palette
Et           je touche « Enregistrer »
Alors        la page est en lecture et contient un lien vers « Capitaine Orsen »

E2E-18-05 · Propriétés en édition sur un téléphone            P1 · @mobile · M6
Étant donné  la page « Ilsa Varenne » en édition sur un téléphone
Quand        j'ouvre l'onglet du panneau de la page et saisis « 50 » dans « Niveau » (72, hors règle)
Alors        l'avertissement « Doit être inférieur ou égal à 60 » disparaît
Quand        je touche « Enregistrer »
Alors        la page en lecture affiche « Niveau » 50

E2E-18-06 · Rechercher sur un téléphone                       P1 · @mobile @pr · M6
Quand        je touche la recherche et tape « ilsa »
Alors        la recherche rapide occupe l'écran et propose « Ilsa Varenne »
Quand        j'ouvre l'écran Recherche et le panneau des filtres, choisis la catégorie « PNJ » puis touche « Afficher les résultats »
Alors        le panneau se referme et les résultats se limitent aux PNJ

E2E-18-07 · Menu d'un élément sans clic droit                 P1 · @mobile · M6
Quand        je touche le bouton « … » de « Hautbois » dans l'explorateur puis « Renommer »
Et           je saisis « Hautbois-sur-Givre » et valide
Alors        l'élément est renommé dans l'arbre et dans les références qui le citent

E2E-18-08 · Déplacer un élément sans glisser-déposer          P1 · @mobile @pr · M6
Quand        je choisis « Déplacer vers… » dans le menu de « Capitaine Orsen » puis le dossier « Lieux »
Alors        « Capitaine Orsen » est rangé dans « Lieux » et ses références sont intactes
Et           le menu de « Personnages » ne propose ni lui-même, ni « PNJ » (son descendant), ni une destination hors de Pages

E2E-18-09 · Préférences et compte depuis le menu « Plus »     P2 · @mobile · M6
Quand        j'ouvre le menu « Plus » de la barre supérieure
Alors        je peux changer la langue et le thème, revenir en arrière et me déconnecter

E2E-18-10 · Cibles tactiles                                   P1 · @mobile · M6
Quand        j'affiche l'accueil, une page, l'édition et l'explorateur sur un téléphone
Alors        chaque bouton, lien de navigation et champ visible offre une zone de toucher d'au moins 44 × 44 px

E2E-18-11 · Application web installable                       P2 · @mobile · M6
Alors        la SPA déclare un manifeste valide (nom, icônes 192 et 512 px accessibles, `display: standalone`) et une couleur de thème
```

---

## F19 — Application Android

Un client des serveurs Erable (décision Q11) : l'application ne contient ni backend ni projet. Elle garde une liste de serveurs et ouvre la SPA du serveur choisi dans un WebView ; l'authentification est celle du web (cookie du serveur).

**Règles**
- **F19.R1** L'utilisateur ajoute un serveur par son adresse. L'adresse est normalisée (schéma `https` par défaut, hôte en minuscules, pas de chemin de requête ni de fragment, barre finale retirée) et vérifiée par `GET /api/info` **avant** l'enregistrement : il faut `mode: "server"` et une version d'API prise en charge ; sinon un message indique la cause (injoignable, pas un serveur Erable, version incompatible avec les versions en présence, comme F16.R4).
- **F19.R2** Le WebView ne navigue que dans l'origine du serveur ouvert : tout autre lien s'ouvre dans le navigateur du système ; `file:`, `content:` et `javascript:` sont refusés.
- **F19.R3** HTTP en clair est accepté (serveur sur le réseau local) mais signalé dans la liste des serveurs ; HTTPS est proposé par défaut.
- **F19.R4** Le bouton retour d'Android remonte l'historique du WebView, puis revient à la liste des serveurs.
- **F19.R5** Les pièces jointes s'envoient par le sélecteur de fichiers d'Android et se téléchargent par le gestionnaire de téléchargements, avec la session de l'utilisateur.
- **F19.R6** La session survit au redémarrage de l'application ; « Oublier ce serveur » le retire de la liste et efface ses cookies.
- **F19.R7** L'interface native (liste, ajout, erreurs) suit la langue du système parmi `fr`, `en`, `de`, `es` (anglais sinon) ; la SPA garde la langue des préférences de l'utilisateur.

**Vérification.** La normalisation, la compatibilité de version et la politique de navigation sont testées en xUnit (`tests/Erable.Android.Tests`). Le parcours sur appareil (ajout, connexion, lecture, édition, envoi et téléchargement d'une pièce jointe, retour, oubli) est une liste de contrôle manuelle de `docs/release.md`, faute d'émulateur dans la CI : pas de scénario `E2E-19-*` automatisé.

---

## 4. Synthèse

### 4.1 Volume par jalon

| Jalon | Ajoutés | Cumul | dont P0 (cumul) |
|---|---|---|---|
| M0 | E2E-16-01, 16-02 | 2 | 2 |
| M1 | F01–F06, F15, F16 et F17, hors scénarios rattachés à un jalon ultérieur (dont E2E-17-02, décision P-13) | 50 | 22 |
| M2 | F07, F08, F09, F12, plus E2E-01-05, 04-05, 05-12, 06-06 | 85 | 38 |
| M3 | F10, F11, F13 | 101 | 44 |
| M4 | F14, plus E2E-01-06, 11-03, 16-06 | 114 | 52 |
| M5 | E2E-05-07, 15-04, 17-04 | 117 | 53 |
| M6 | F18 (F19 sans scénario automatisé) | 128 | 55 |

Le pipeline PR exécute les 62 scénarios `@pr` (un peu plus de la moitié) ; le reste tourne la nuit (§T-13).

### 4.2 Ce que les e2e ne couvrent pas

Les combinatoires sont vérifiées à des niveaux plus rapides, pas en e2e (§T-4 à §T-8) : toutes les règles par type, tous les cas de la syntaxe Markdown, toutes les combinaisons de rôles par endpoint, la fusion sur entrées générées. Un scénario e2e vérifie que **la chaîne complète est branchée**, pas chaque cas.

### 4.3 Évolution du document

- Une nouvelle fonctionnalité ajoute ses règles `F<nn>.R<n>` et ses scénarios **avant** l'implémentation ; ils servent de critères d'acceptation.
- Un bug trouvé en usage réel ajoute un scénario s'il concerne un parcours ; sinon, un test de plus bas niveau suffit.
- Un script CI vérifie que chaque ID de scénario du document a un test Playwright du même ID, et inversement.
