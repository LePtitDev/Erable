# M0-01 — Solution .NET, projets, analyseurs

- **Statut** : todo
- **Jalon** : M0
- **Dépend de** : —
- **Références** : §A-3, §T-3, `CLAUDE.md` §7–8

## Objectif

Poser la solution C# avec ses projets, leurs dépendances et un compilateur assez exigeant pour que des classes entières de bugs ne compilent pas.

## Périmètre

- `global.json` (SDK .NET 10, `rollForward: latestFeature`), `Erable.sln`.
- Projets : `src/Erable.Core` (classlib), `src/Erable.Storage` (classlib → Core), `src/Erable.Server` (web → Core, Storage), `src/Erable.Desktop` (exe → Server).
- Projets de test : `tests/Erable.Core.Tests`, `tests/Erable.Storage.Tests`, `tests/Erable.Server.Tests` (xUnit v3, Shouldly, CsCheck).
- `Directory.Build.props` : `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `ImplicitUsings`, `Deterministic`.
- `Directory.Packages.props` (versions centralisées).
- `.editorconfig` (C#, TS, JSON, MD), `.gitattributes` (fins de ligne), `.gitignore` (.NET, Node, artefacts, `*.db` hors `spec/`).
- Un test trivial par projet de test pour valider la chaîne.
- Mettre à jour `CLAUDE.md` §9 avec les commandes réelles.

## Hors périmètre

- Code métier, endpoints (M0-03), CI (M0-10).

## Critères d'acceptation

- [ ] `dotnet build` sans avertissement ; `dotnet test` vert.
- [ ] `Erable.Core` ne référence **aucun** paquet IO/HTTP/SQLite (test d'architecture simple sur les références d'assembly).
- [ ] Un `switch` non exhaustif sur un `enum` fait échouer le build (CS8509 en erreur).
- [ ] `CLAUDE.md` §9 à jour.

## Points d'attention

- Vérifier les versions actuelles des paquets (xUnit v3, Shouldly, CsCheck) au lieu de les écrire de mémoire.
- Pas de dépendance « au cas où ».

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
