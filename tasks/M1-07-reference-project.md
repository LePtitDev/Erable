# M1-07 — Projet « Chroniques d'Aube », builders, points d'appui `/_test`

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-05, M1-06
- **Références** : features §1.4, features §2, §T-4.3

## Objectif

Disposer du monde de test unique utilisé par les tests d'intégration, les e2e, les captures et la démo.

## Périmètre

- Générateur (code C# versionné) qui produit `spec/projects/chroniques/` à partir d'une description lisible, avec **les ID figés** de features §2.1. Le dossier produit est versionné ; le générateur sert à le reconstruire après une migration de format.
- Contenu M1 : arbre de §2.1 (pages, dossiers, sous-pages), textes des pages (repris du mockup « Page — lecture » pour Ilsa), références croisées, pièces jointes de §2.2 (petits fichiers de test réels : PNG, WEBP, MP4 court, ZIP, SVG). Catégories, propriétés et liens s'ajoutent en M2.
- Dates de modification cohérentes avec l'horloge de test (13 octobre 2026, 10:00, Europe/Paris) : « Modifiée il y a 2 h par Camille R. » pour Ilsa.
- Points d'appui (mode test uniquement) : `POST /_test/reset?fixture=chroniques` (< 100 ms), `POST /_test/clock`, `GET /_test/blobs`. `login-as` arrive en M4-01.
- Fixtures Playwright : chargement du projet, horloge.

## Critères d'acceptation

- [ ] Un test e2e d'exemple réinitialise le projet et ouvre Ilsa Varenne par son ID.
- [ ] `reset` est isolé par fichier de test (exécution parallèle possible).
- [ ] Le projet passe `erable doctor` (dès que M1-16 existe).

## Points d'attention

- Les binaires de test doivent rester petits (< 1 Mo au total).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
