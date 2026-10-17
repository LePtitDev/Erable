# Revue des traductions (M5-05)

> État au 17 octobre 2026 : 793 clés, 4 langues (`fr` est la référence). Les relectures `en`, `de`, `es` ci-dessous sont à confirmer par une personne dont c'est la langue.

## Ce qui est vérifié automatiquement

- **Parité** (`parity.test.ts`) : chaque clé de `fr` existe dans `en`, `de`, `es` et inversement, avec les mêmes paramètres d'interpolation et les mêmes formes de pluriel.
- **Pas de texte laissé en français** : balayage des valeurs de `en`, `de`, `es` identiques à `fr` ou contenant des mots français courants. Résultat : seules subsistent des valeurs identiques par nature (« Erable », « Code », « Description », « Type », « Date », « Actions », « Local », « Options », « Ctrl+X »…), toutes légitimes dans la langue cible.
- **Pas de chaîne en dur** dans le JSX (règle ESLint) ; pseudo-locale `xx` pour les débordements (M5-02).

## À relire par une personne de la langue

### Terminologie (les mêmes mots partout)

| Concept | fr | en | de | es |
|---|---|---|---|---|
| Lien typé entre éléments | lien | link | Verknüpfung | enlace |
| Référence `[[ID]]` dans le texte | lien / référence | link | **Link** (3 occurrences) | enlace |
| Propriété | propriété | property | Eigenschaft | propiedad |
| Catégorie | catégorie | category | Kategorie | categoría |
| Pièce jointe | pièce jointe | attachment | Anhang | adjunto |
| Corbeille / Historique | corbeille / historique | trash / history | Papierkorb / Verlauf | papelera / historial |
| Dossier | dossier | folder | Ordner | carpeta |

- **`de` : « Link » ou « Verknüpfung » ?** Le lien *typé* est partout « Verknüpfung » ; le lien *dans le texte* (clés `notFound.description`, `reference.brokenLabel`, `palette.keys.link`) est « Link ». C'est cohérent avec l'usage informatique allemand, mais à confirmer.
- `es` : « enlace » sert aux deux sens, ce qui est naturel en espagnol.

### Longues phrases (≥ 90 caractères en français)

Ce sont les textes les plus exposés à une tournure maladroite : messages d'erreur, aides, confirmations. Les 39 clés, à relire en `en`, `de` et `es` :

| Clé | Texte de référence (fr) |
|---|---|
| `errors.project.formatTooNew` | Ce projet a été créé par une version plus récente d’Erable. Mettez à jour l’application pour l’ouvrir. |
| `errors.project.migration.failed` | La migration a échoué : le projet a été remis dans son état d’origine, rien n’a été modifié. |
| `errors.attachment.executable` | Erable n’ouvre jamais un programme ou un script. Affichez-le dans l’explorateur si vous savez d’où il vient. |
| `errors.attachment.quotaExceeded` | Ce projet a utilisé toute la place que le serveur accorde à ses pièces jointes. Supprimez des fichiers ou demandez à l’administrateur du serveur d’augmenter le quota. |
| `errors.property.key.invalid` | Clé invalide : lettres minuscules, chiffres, « _ » ou « - », en commençant par une lettre. |
| `errors.account.login.invalid` | L’identifiant compte 2 à 32 caractères : lettres minuscules, chiffres, « . », « _ » ou « - ». |
| `home.new.inFolderDescription` | Ce dossier n’est pas encore un projet Erable. Rien n’est écrit tant que vous n’avez pas confirmé. |
| `home.open.status.tooNew` | Ce projet utilise le format {{version}}, plus récent que cette version d’Erable. Mettez à jour l’application pour l’ouvrir ; le projet n’a pas été modifié. |
| `home.migrate.description` | Ce projet a été créé avec une version plus ancienne d’Erable (format {{from}}). Pour l’ouvrir, il doit être converti au format {{to}}. |
| `home.migrate.backup` | Une sauvegarde de la base, « erable.db.bak-… », est créée à côté du projet avant toute modification. Si la migration échoue, elle est remise en place et le projet reste tel qu’il était. |
| `home.server.empty` | Vous n’êtes membre d’aucun projet pour le moment. Demandez à un administrateur de vous ajouter. |
| `notFound.description` | Cet élément n’existe pas, ou plus, dans ce projet. Son lien a peut-être été copié depuis un autre projet. |
| `history.hint` | Cochez deux révisions pour les comparer. Restaurer crée une nouvelle révision : l’historique n’est jamais réécrit. |
| `attachments.tooLarge` | « {{name}} » dépasse la taille maximale autorisée ({{max}}). Aucune pièce jointe n’a été créée. |
| `editor.conflict` | La page a été modifiée ailleurs depuis son ouverture. Copiez vos changements, puis rechargez la page avant d’enregistrer. |
| `property.keyHint` | Identifiant technique : lettres minuscules, chiffres, « _ » ou « - ». Le renommer ne casse rien : tout passe par l’ID. |
| `property.blocked.explain` | Ces valeurs existantes ne peuvent pas être converties dans le nouveau type. Corrigez-les ou gardez le type actuel. |
| `category.propertiesHint` | Appliquées à chaque page de la catégorie. Les valeurs servent de défaut et peuvent être redéfinies par page. |
| `category.noProperties` | Aucune propriété : ajoutez une valeur par défaut pour qu’elle s’applique aux pages de la catégorie. |
| `links.inverseHint` | Affiché sur la page liée, côté entrant (« Élève de » pour « Mentor de »). Laisser vide pour garder le même libellé. |
| `trash.intro` | Les éléments supprimés restent ici jusqu’à leur restauration ou jusqu’au vidage de la corbeille. |
| `trash.propertyValues_other` | Les valeurs de {{count}} pages ou catégories seront retirées (restaurables avec la propriété). |
| `trash.propertyValues_many` | Les valeurs de {{count}} pages ou catégories seront retirées (restaurables avec la propriété). |
| `trash.relocated` | « {{name}} » a été restauré sous « {{parent}} » : son emplacement d’origine n’existe plus. |
| `trash.emptyDescription_one` | Cette action est définitive : {{count}} élément et son contenu seront supprimés pour toujours. |
| `trash.emptyDescription_other` | Cette action est définitive : {{count}} éléments et leur contenu seront supprimés pour toujours. |
| `trash.emptyDescription_many` | Cette action est définitive : {{count}} éléments et leur contenu seront supprimés pour toujours. |
| `auth.expired.description` | Votre session a expiré. Reconnectez-vous pour continuer : vos modifications non enregistrées sont conservées. |
| `accessDenied.description` | Vous n’avez pas accès à ce projet. Demandez à un administrateur du projet de vous y ajouter. |
| `presence.editing_one` | {{names}} modifie aussi cette page{{section}}. Vos changements seront fusionnés à l’enregistrement. |
| `presence.editing_other` | {{names}} modifient aussi cette page. Vos changements seront fusionnés à l’enregistrement. |
| `presence.editing_many` | {{names}} modifient aussi cette page. Vos changements seront fusionnés à l’enregistrement. |
| `conflict.description` | {{names}} a modifié les mêmes passages que vous. Choisissez, pour chaque passage, la version à conserver : l’autre reste dans l’historique de la page, rien n’est perdu. |
| `conflict.remoteChanged` | {{names}} a enregistré cette page pendant que vous la modifiez. Vos changements seront fusionnés à l’enregistrement. |
| `servers.noVault` | Ce système n’a pas de coffre pour les mots de passe : il faudra vous reconnecter à chaque lancement. |
| `servers.connect.description` | Saisissez l’adresse du serveur Erable de votre équipe. Rien d’autre n’est envoyé avant la vérification des versions. |
| `servers.incompatible` | {{host}} utilise l’API {{serverApi}} (Erable {{serverApp}}) et cette application comprend l’API {{appApi}} (Erable {{appVersion}}). Mettez à jour l’application ou le serveur pour les faire dialoguer. Rien d’autre n’a été envoyé à ce serveur. |
| `servers.expired.description` | Votre session sur ce serveur a pris fin. Saisissez le mot de passe de {{user}} pour continuer : votre travail en cours est conservé. |
| `members.intro` | Qui peut lire, modifier ou administrer ce projet. Les changements s’appliquent immédiatement. |

## Contenu par défaut (langue du contenu)

Les noms des quatre racines de l'arbre suivent la **langue du projet**, pas celle de l'interface (décision Q1) : `Roots.Label` en `fr`, `en`, `de`, `es` (test `RootsTests`). Il n'existe pas encore de modèles de projet : leurs libellés seront à ajouter à ce moment-là. Les noms proposés pour une nouvelle page ou un nouveau dossier (« Nouvelle page », « Nouveau dossier ») viennent en revanche de la **langue de l'interface** : voir la proposition de `tasks/M5-05-i18n-backup-docs.md`.
