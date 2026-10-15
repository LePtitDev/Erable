# M1-15 — Préférences : langue et thème

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-08
- **Références** : F15.R1–R3, Mockup « Accueil » (langue et thème), P-03

## Objectif

Choisir la langue d'interface et le thème, mémorisés par utilisateur et appliqués sans flash.

## Périmètre

- Préférences stockées côté backend (`GET/PUT /api/v1/me/preferences`) : profil applicatif en local, compte en mode serveur (M4).
- Langue d'interface (Français, English, Deutsch, Español) indépendante de la langue du contenu ; dates reformatées.
- Thème Clair / Sombre / Système ; Système suit l'OS en direct.
- Le backend injecte `lang` et `data-theme` dans `index.html` (pas de flash au premier rendu).
- Écran ou panneau « Préférences » accessible depuis l'accueil et le projet.

## Critères d'acceptation

- [x] `E2E-15-01`, `E2E-15-02`, `E2E-15-03` verts.
- [x] Les préférences survivent à un redémarrage du desktop (port différent).

## Points d'attention

- Le choix des raccourcis arrive en M5-01 : prévoir la structure de données, pas l'écran.

## Notes d'implémentation

- **Modèle** : `UserPreferences(Language?, Theme, Shortcuts)` dans `Erable.Server/Projects` (`ThemePreference` y rejoint l'énum, `Spa/ThemePreference.cs` et `IThemePreferenceSource` supprimés : `index.html` lit directement le profil de la requête). `Language = null` = suivre le navigateur. `Shortcuts` (commande → combinaison, 200 au plus, 64 caractères) prépare M5-01 sans écran.
- **Stockage** : `preferences.json` du profil applicatif (Q7) ; un document illisible ou une valeur inconnue retombe sur le défaut (`Normalize`) plutôt que de bloquer l'application.
- **API** : `GET|PUT /api/v1/me/preferences` ; `PUT` renvoie les préférences enregistrées, 400 `preferences.languageUnsupported` / `preferences.shortcutsInvalid`, 400 aussi pour un thème inconnu (désérialisation).
- **Sans flash** : `SpaIndexEndpoint` écrit `<html lang="…" data-theme="…">` ; `lang` n'est écrit que si une langue est choisie (sinon la SPA garde sa détection navigateur et pose `lang` elle-même).
- **SPA** : `useSavePreferences` fusionne le changement avec les préférences connues (changer le thème ne remet pas la langue à zéro) ; le changement est appliqué tout de suite, l'enregistrement suit (erreur seulement journalisée). `useSetTheme` sert au sélecteur et au bouton soleil/lune de la barre du projet. La pseudo-locale de développement n'est jamais enregistrée.
- **Bug corrigé** : l'accueil affiche deux sélecteurs de thème (colonne et dialogue) qui partageaient `name="theme"`, donc un seul groupe radio natif : cliquer dans l'un décochait l'autre et React rétablissait l'état. Nom unique par instance (`useId`).
- **Écart** : E2E-15-01 cite « Properties », libellé du panneau Propriétés d'une page (M2-06) ; le test vérifie « Explorer » et « Edit » et la date anglaise, et sera complété en M2-06.
- **Redémarrage desktop** : `PreferencesApiTests.Preferences_NewBackendOnTheSameProfile_AreKept` démarre deux backends desktop successifs (ports aléatoires) sur le même profil.

**Vérification** : `dotnet test --solution Erable.sln` (420 dont 3 ignorés) · `dotnet format --verify-no-changes` · `pnpm -C src/web api:generate`, `typecheck`, `lint`, `format:check`, `test` (339) · `exec playwright test --project chromium --project webkit` (108).

**Commit** : `feat(server,web): persist language and theme preferences and inject them into index.html (M1-15)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
