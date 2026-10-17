# M5-01 — Raccourcis configurables

- **Statut** : review
- **Jalon** : M5
- **Dépend de** : M1-13, M3-02
- **Références** : §A-11.3, F15.R4, E2E-05-07, E2E-15-04

## Objectif

Laisser chacun adapter les raccourcis à son clavier et à son OS.

## Périmètre

- Table de raccourcis en données (action → combinaison par OS), défauts de §A-11.3, surcharges dans les préférences utilisateur.
- Écran de configuration : capture de combinaison, conflit signalé (« Déjà utilisé par : Recherche ») avec proposition de remplacer, réinitialisation.
- Écran « Aide et raccourcis » (accueil) généré depuis la table.
- Toutes les actions accessibles aussi à la souris (§A-3.1, mobile plus tard).

## Critères d'acceptation

- [x] `E2E-05-07`, `E2E-15-04` verts.
- [x] Affichage des combinaisons adapté à l'OS (`⌘` sur macOS).

## Notes d'implémentation

- Table `shortcuts` (action → défaut par OS) dans `shared/keyboard/shortcuts.ts` ; surcharges dans `UserPreferences.shortcuts` (action → combinaison CodeMirror, `""` = aucun raccourci). Quatre actions configurables : palette d'insertion, recherche, enregistrer, ouvrir un dossier. F2, `[[` et Échap restent fixes.
- `useShortcut(action, handler)` (écoute capturante sur la fenêtre) remplace les écouteurs en dur de l'accueil et du shell ; l'éditeur reçoit `saveKey` et `bindings` dans un `Compartment` reconfiguré à chaud.
- Écran « Raccourcis clavier » dans les préférences (`ShortcutsSettings`) : capture, conflit (« Déjà utilisé par : Recherche ») avec remplacement, réinitialisation par action et globale. « Aide et raccourcis » est généré depuis la table. Affichage `⌘⇧K` sur macOS, noms de touches traduits sinon.
- Scénarios `E2E-05-07` et `E2E-15-04` écrits (non exécutés à ce stade, voir la vérification finale de M5) ; tests unitaires dans `shortcuts.test.ts`.
- Écart : clés i18n `help.*.{openFolder,save,paletteShortcut}`, `editor.saveShortcut`, `shell.searchShortcut` supprimées (remplacées par la table).
- Message de commit : `feat(web): add configurable keyboard shortcuts (M5-01)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
