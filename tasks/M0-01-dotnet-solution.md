# M0-01 — Solution .NET, projets, analyseurs

- **Statut** : review
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

- [x] `dotnet build` sans avertissement ; `dotnet test` vert.
- [x] `Erable.Core` ne référence **aucun** paquet IO/HTTP/SQLite (test d'architecture simple sur les références d'assembly).
- [x] Un `switch` non exhaustif sur un `enum` fait échouer le build (CS8509 en erreur).
- [x] `CLAUDE.md` §9 à jour.

## Points d'attention

- Vérifier les versions actuelles des paquets (xUnit v3, Shouldly, CsCheck) au lieu de les écrire de mémoire.
- Pas de dépendance « au cas où ».

## Notes d'implémentation

- **Versions vérifiées sur nuget.org (14/10/2026)** : xunit.v3 4.0.2, Shouldly 4.3.0 (5.0 encore en préversion), CsCheck 4.9.1 (5.0 en RC). SDK local 10.0.204.
- **Écart : Microsoft.Testing.Platform (MTP) au lieu de VSTest.** xUnit v3 4.x ne supporte plus la cible VSTest sous le SDK .NET 10 ; `global.json` active `"test": { "runner": "Microsoft.Testing.Platform" }`. Conséquences : `Microsoft.NET.Test.Sdk` et `xunit.runner.visualstudio` sont inutiles (retirés) et la commande devient `dotnet test --solution Erable.sln`.
- `Erable.sln` au format classique (la tâche le nomme ainsi), bien que .NET 10 propose `.slnx` par défaut.
- L'exécutable serveur s'appelle `erable` (`AssemblyName`) pour coller à la CLI `erable serve` / `erable doctor`.
- `CS8509`/`CS8524` forcés en erreur (déjà couverts par `TreatWarningsAsErrors`, explicité par sécurité). Vérifié manuellement : un `switch` non exhaustif sur un `enum` produit `error CS8509`. Pas de test automatisé (il faudrait embarquer Roslyn pour compiler un extrait : coût > bénéfice).
- `CA1707` (soulignés) et `CA2007` (`ConfigureAwait`) désactivés sous `tests/` : incompatibles avec la convention `Method_Context_ExpectedResult` et inutiles sous xUnit.
- Test d'architecture : `Erable.Core` ne référence aucun assembly dont le nom commence par `Microsoft.Data`, `System.Data`, `Dapper`, `Microsoft.AspNetCore`, `System.Net.Http`, `Photino`, `Erable.Storage/Server/Desktop`.
- `Erable.Storage` n'a pas encore de paquet (SQLite/Dapper arrivent en M1-03 : pas de dépendance « au cas où »).
- `.gitignore` : `*.db` ignoré sauf sous `spec/`.

**Vérification** : `dotnet build Erable.sln` (0 avertissement) · `dotnet test --solution Erable.sln` (3/3).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
