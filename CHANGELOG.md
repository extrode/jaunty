# Changelog

All notable changes to Jaunty are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning is calendar-based (`YYYY.M.D` of the release cut). Package versions
are set at release time from the git tag (`vYYYY.M.D`).

## [Unreleased]

### Fixed

- **Generated mapper NULL handling (correctness):** the source-generated
  `DbDataReader` fast path mapped SQL `NULL` to `0`/`false`/default instead of
  `null` for nullable value type properties (`decimal?`, `int?`, `bool?`, ...).
  The generated ternary now uses a property-typed `default`. (PRD-001)
- **Packaging:** `Beparey.Jaunty` now ships `Jaunty.SourceGenerator.dll` under
  `analyzers/dotnet/cs`. Previously the published package contained no
  analyzer, so `IMapped<T>` mappers were never generated for package consumers.
  (PRD-013)
- **Release build:** the solution no longer excludes the source generator from
  Release configuration; `dotnet build Jaunty.slnx -c Release` succeeds from a
  clean clone. (PRD-012)
- `JoinedQueryBuilder.SelectPartialFirstAsync` (non-generic) now throws
  `InvalidOperationException` on an empty result, per the `IJoinedQuery`
  contract, instead of returning `null`. (PRD-016)

### Added

- `tests/Jaunty.SourceGenerator.Tests`: runtime shape-safety validation of the
  actual generator output against SQLite (reordered columns, result-set
  changes, interleaved readers, narrowed shapes). (PRD-001)
- NuGet metadata for all packages: SourceLink, symbol packages (snupkg),
  license-acceptance flag, repository info, XML documentation. (PRD-013)
- `TreatWarningsAsErrors` for all `src/` projects; the codebase builds with
  zero warnings. (PRD-016)

### Changed

- `JauntyConfig.Logger` is now `Action<string, object?>` (parameter payload
  may be null). Source-compatible for typical lambda subscribers.

## [2026.01.01] - baseline

Initial internal version. Core query/CRUD/bulk APIs, fluent builder,
scaffolding, FlatFiles/DuckDB providers, NativeAOT source generation,
logging and command interception.
