# M5-06 — Nom affiché modifiable dans les préférences

- **Statut** : review
- **Jalon** : M5
- **Dépend de** : M1-08 (accueil), M4-01 (comptes), M4-08 (membres)
- **Références** : F15, §A-9, décision Q8

## Objectif

Permettre à l'utilisateur de changer, depuis les préférences, le nom sous lequel il apparaît : « Bonjour, … » de l'accueil, auteur des modifications, présence. Un seul nom par personne, partout.

## Périmètre

- `PUT /api/v1/me/name` `{ name }` (tout utilisateur connecté), qui renvoie `CurrentUserInfo` :
  - desktop : nom gardé dans le profil applicatif (`identity.json`), à la place du nom du compte du système ;
  - serveur : nom du compte, modifié par son titulaire (sans toucher à l'identifiant, aux droits ni aux sessions).
- Champ « Nom affiché » en tête de la boîte Préférences, avec bouton Enregistrer (Entrée au clavier).
- Textes dans les 4 langues.

## Hors périmètre

- Revenir au nom du système en desktop (le champ ne peut pas être vide).
- Rafraîchir la présence déjà affichée chez les autres membres (voir Propositions).

## Critères d'acceptation

- [x] Le nouveau nom apparaît sur l'accueil sans rechargement, et comme auteur (`createdByName`, `updatedByName`, historique).
- [x] Desktop : le nom survit à un redémarrage (même profil).
- [x] Serveur : le nom du compte change pour tous (liste des comptes, membres, auteurs) ; session et droits conservés.
- [x] Nom vide, blanc, avec caractère de contrôle ou de plus de 80 caractères : 422 `account.name.invalid`, nom inchangé, message traduit.
- [x] Anonyme : 401 (règle `RequireSignIn` du groupe `/me`, couverte par la matrice d'autorisation).
- [x] Tests d'intégration (`DisplayNameApiTests`) et Vitest (`HomeScreen.test.tsx`).
- [x] Textes traduits dans les 4 langues ; utilisable au clavier.
- [ ] Scénario E2E : aucun scénario `E2E-15-*` n'existe pour cette règle (voir Propositions).

## Notes d'implémentation

- **Décisions (validées par l'utilisateur)** : le nom vaut partout (pas une simple salutation) ; en serveur, chacun peut changer le sien.
- **Desktop** : `UserProfile.GetDisplayName()` / `SaveDisplayName()` (fichier `identity.json`, gardé en mémoire une fois lu : `NameOf` est appelé pour chaque auteur affiché). `UserDirectory.NameOf("local")` prend le profil de la requête (`UserProfiles.For`), donc le slot de test en e2e, sinon le profil par défaut (présence hors requête).
- **Serveur** : réutilise `AccountService.UpdateAccount` (nom seul : pas de fin de session, cache des noms invalidé).
- **Validation** : `AccountRules.NormalizeDisplayName` (Core), la même que pour la création de compte.
- **Front** : `useRenameMe()` met à jour `me` puis invalide les autres requêtes (les auteurs affichés peuvent changer).
- **Client API** régénéré. `pnpm` via Corepack était cassé sur la machine (cache incomplet) : `node scripts/api-generate.mjs` lancé directement.
- **Vérifications** : `dotnet build Erable.sln` (0 avertissement) ; `dotnet test --solution Erable.sln` : 1368 réussis, 1 échec intermittent sans lien, `PresenceTests.Presence_OfAClientThatStopsAnswering_ExpiresWithoutAGoodbye` (délai de 1 s sous la charge de la suite complète ; vert 3 fois sur 3 isolé et dans la suite serveur seule) ; Vitest 756/756 ; lint et typecheck propres ; Prettier propre sur les fichiers modifiés (`Members.test.tsx` signalé, antérieur et non modifié).
- **Non vérifié** : e2e Playwright non lancés ; rendu non contrôlé dans l'application.

## Propositions

- Ajouter dans `docs/features.md` une règle `F15.R5` (« le nom affiché se change dans les préférences ; il vaut partout ») et un scénario `E2E-15-0x`.
- Diffuser un événement de présence après un renommage, pour que les autres membres voient le nouveau nom sans attendre.
- Desktop : bouton « Revenir au nom du système ».
- Fiabiliser `Presence_OfAClientThatStopsAnswering_ExpiresWithoutAGoodbye` (attente bornée plus large que le délai du client).
