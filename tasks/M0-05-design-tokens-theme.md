# M0-05 — Tokens de design et thèmes

- **Statut** : todo
- **Jalon** : M0
- **Dépend de** : M0-02
- **Références** : §A-3.1 (Thèmes), F15.R2–R3, Mockup (tous les écrans ; « Page — édition » est en sombre), P-03

## Objectif

Extraire du mockup un système de tokens (couleurs, typographie, espacements, rayons, ombres) en variables CSS, clair et sombre, pour qu'aucun composant n'ait de couleur en dur.

## Périmètre

- Décoder le mockup (`CLAUDE.md` §1) et relever les valeurs des écrans clair et sombre. Les styles y sont inline avec de nombreuses nuances proches : les regrouper en un jeu **sémantique** réduit (`--color-bg`, `--color-surface`, `--color-text-muted`, `--color-accent`, `--color-danger`…).
- Couleurs par type d'élément (badges P / C / Pr / L du mockup) en tokens dédiés.
- Polices **auto-hébergées** (l'app doit fonctionner hors ligne) : IBM Plex Sans, IBM Plex Mono, Source Serif 4 ; inclure leurs licences OFL.
- `:root` = clair, `[data-theme="dark"]` = sombre ; « Système » suit `prefers-color-scheme` en direct.
- Thème appliqué **avant le premier rendu** (attribut injecté par le backend dans `index.html`, voir P-03).
- Page interne de démonstration des tokens (dev uniquement) pour la revue visuelle.
- Règle de lint (stylelint) interdisant les couleurs littérales hors du fichier de tokens.

## Critères d'acceptation

- [ ] Contraste AA vérifié pour le texte normal et atténué dans les deux thèmes ; les couleurs du mockup qui échouent sont signalées avec une correction proposée.
- [ ] Une couleur littérale dans un `.module.css` fait échouer le lint.
- [ ] Bascule clair / sombre / système fonctionnelle sur la page de démonstration.

## Points d'attention

- Ne pas recopier des dizaines de nuances : **proposer** le jeu réduit et le faire valider avant de construire les écrans.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
