# M2-05 — Écran Propriété

- **Statut** : review
- **Jalon** : M2
- **Dépend de** : M2-03, M1-11
- **Références** : F08, Mockup « Propriété — définition et règles »

## Objectif

Définir une propriété et ses règles, et voir immédiatement leur effet sur le projet.

## Périmètre

- Mode édition avec Annuler / Enregistrer ; fil d'Ariane ; badge Pr ; ID.
- Identité : nom affiché, clé, description.
- Type de valeur (sélecteur de cartes comme dans le mockup) ; valeur par défaut adaptée au type.
- Règles de validation : liste, ajout selon le type (« Ajouter : + … »), texte « Aucune règle disponible pour un booléen ».
- « Tester une valeur » avec résultat instantané (évaluateur TS).
- « Utilisée par » (catégories), « Renseignée sur N pages », « N pages hors règles » avec valeurs.
- Dialogues : remplacement d'option d'enum supprimée, refus de changement de type avec pages bloquantes.

## Critères d'acceptation

- [x] `E2E-08-01` à `E2E-08-05`, `E2E-08-07` à `E2E-08-11` verts (les volets « page » de 08-02, 08-05, 08-08 et 08-11 sont complétés en M2-06).
- [x] Clavier et axe-core.

## Notes d'implémentation

- **Écran** (`features/properties/PropertyView.tsx`) : lecture (identité, type et défaut, options d'une énumération, règles, « Tester une valeur », « Utilisée par », « Renseignée sur N pages », « N pages hors règles » avec valeur et messages) et édition (nom, clé, description, cartes de type + case « Liste de valeurs », défaut par `ValueEditor`, options, règles). Réutilise les styles de l'écran Catégorie.
- **Règles** : `RulesEditor.tsx` + `ruleDrafts.ts`. Une règle en cours de saisie est un brouillon textuel (`RuleDraft`) converti par `parseRule` : « Enregistrer » est désactivé tant qu'une règle est incomplète. Changer de type retire les règles qui ne s'appliquent plus (F08.R2) et vide le défaut.
- **Enregistrement** : `PUT properties/{id}` puis, si le nom change, `PATCH elements/{id}` (non atomique, dette mineure). Les refus `property.option.inUse` (F08.R5) et `property.value.notConvertible` (F08.R6) ouvrent une boîte : choix de l'option de remplacement ou simple liste des pages bloquantes (extension `pages` du ProblemDetails).
- **Test d'une valeur** : évaluateur TS (`evaluateRules`) ; non proposé pour les références (il faudrait le contexte du projet).
- **Création** : « Nouvelle propriété » dans le menu contextuel de la racine Propriétés et de ses dossiers ; créée en `string`, elle s'ouvre en édition.
- Vérification : `pnpm -C src/web test` (587 verts), `lint`, `typecheck`, `format:check` ; `pnpm -C src/web exec playwright test f07 f08` (Chromium + WebKit, 28 verts).
- Commit proposé : `feat(web): add the property screen with rules editor and rule tester (M2-05)`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
