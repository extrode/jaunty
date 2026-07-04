# Production Readiness Tasklist

Last updated: 2026-07-03

This is the active execution plan. Priorities are ordered by production risk.
Assessment basis: [PRODUCTION-READINESS-2026-07-02.md](../../05-quality/reports/PRODUCTION-READINESS-2026-07-02.md) (full local build + test validation).

## P0 - Broken Build and Correctness

### PRD-012: Fix Release solution build (CI is red)
- Priority: `P0`
- Status: `Done (2026-07-03, branch fixes/release-build-prd012)` - slnx Release exclusion removed; clean-clone Release build succeeds with 0 warnings.
- Scope:
  - `dotnet build Jaunty.slnx -c Release` fails with 3x CS0535 in `benchmarks/Jaunty.Benchmarks/Entities/` (`JauntyProduct`, `TpcLineItem`, `TpcOrder` missing generated `ReadEntity`).
  - Root cause: `Jaunty.slnx` sets `<Build Solution="Release|*" Project="false" />` on `Jaunty.SourceGenerator`, so the analyzer assembly is never built in Release solution builds and source generation does not run.
  - Fix the slnx exclusion (or make the analyzer project reference build-independent) and verify `ci.yml` Build & Test passes on a fresh checkout.
- Acceptance criteria:
  - `dotnet build Jaunty.slnx -c Release` succeeds from a clean clone.
  - CI Build & Test job green on `dev`.
- Estimate: 0.5 day

### PRD-001: Make generated ordinal caching schema-safe
- Priority: `P0`
- Status: `Done (2026-07-03, branch fixes/ordinal-cache-prd001)` - new tests/Jaunty.SourceGenerator.Tests validates REAL generator output against SQLite (reordered columns, NextResult shape changes, interleaved readers). Found and fixed a NULL->0 mapping bug for nullable value types in the DbDataReader fast path (untyped default in generated ternary).
- Scope:
  - Update source generator to avoid stale ordinal reuse across different result shapes (current `CacheEntry` uses an `_initialized` flag, not shape validation).
  - Add regression coverage for shape/order changes.
- Acceptance criteria:
  - Same entity queried with different column orders maps correctly.
  - Same entity queried with different column subsets fails/passes according to strict/partial rules, not stale cache.
  - No mapping corruption in multi-result-set flows.
- Estimate: 2-4 days (remaining: runtime validation)

## P1 - Release Engineering (net-new; nothing publishable exists today)

### PRD-013: NuGet package metadata completeness
- Priority: `P1`
- Status: `Done (2026-07-03, branch fixes/nuget-metadata-prd013)` - src/Directory.Build.props already covered license/readme/docs/tags (2026-07-02 report missed it); added SourceLink, snupkg symbols, license acceptance, and CRITICALLY the analyzer dll now packs into analyzers/dotnet/cs (published packages previously shipped without the source generator). All 7 packages pack clean. 2026-07-04: EnablePackageValidation on (15 pre-existing ns2.0/net8.0 API divergences baselined in CompatibilitySuppressions.xml); IsPackable=false added to benchmarks/samples/SourceGenerator so solution pack ships ONLY the 7 intended packages.
- Scope (all 7 packable projects; consider a shared `Directory.Build.props`):
  - `PackageLicenseFile` packing `LICENSE.md` (custom proprietary license — cannot use an SPDX expression); evaluate `PackageRequireLicenseAcceptance`.
  - `PackageReadmeFile`, `PackageIcon`, `PackageTags` (only FlatFiles projects have tags today).
  - `GenerateDocumentationFile` so the merged XML docs ship in the packages.
  - SourceLink: `PublishRepositoryUrl`, `EmbedUntrackedSources`, `ContinuousIntegrationBuild`, `IncludeSymbols` + `SymbolPackageFormat=snupkg`, deterministic build.
- Acceptance criteria: `dotnet pack` output passes NuGet package validation (`dotnet-validate` / NuGet.org checks); IntelliSense works from the package.
- Estimate: 1-2 days

### PRD-014: Release pipeline and versioning
- Priority: `P1`
- Status: `Mostly done (2026-07-03, branch fixes/release-pipeline-prd014)` - CHANGELOG.md added (CalVer documented); release.yml packs/tests/publishes on v* tags and creates a GitHub Release. REMAINING MANUAL: create main branch on the remote; configure NUGET_API_KEY secret. 2026-07-04: version centralized in src/Directory.Build.props (was hardcoded per-csproj); GeneratePackageOnBuild removed.
- Scope:
  - Create `main` branch (remote currently has only `origin/dev`) and align with the documented release flow (`/release-cut`, `/release-ship`).
  - Add a tag-triggered publish workflow (pack, validate, push to NuGet feed, create GitHub Release).
  - Decide and document a versioning strategy; current `2026.01.01` is six months stale across all packages.
  - Add `CHANGELOG.md` and keep it per release.
- Acceptance criteria: a tagged release produces published, versioned packages with release notes, reproducibly.
- Estimate: 2-3 days

### PRD-015: CI test coverage hardening
- Priority: `P1`
- Status: `Done (2026-07-03, branch fixes/ci-hardening-prd015)` - mssql 2022 service container + committed Northwind bootstrap (generated from the SQLite reference db); CI now runs core (SQLite+SqlServer), Fluent, Scaffolding, SourceGenerator, FlatFiles suites with Cobertura coverage artifacts. Verified locally against a real container: 3,587/3,589 (2 CSV BULK INSERT tests need the CI volume mount). Postgres/MySQL jobs remain future work.
- Scope:
  - CI currently runs only 97 of 3,589 core tests (SQLite filter) plus FlatFiles/DuckDB; Fluent (787) and Scaffolding (177) suites are not run at all.
  - Add SQL Server service container (or Testcontainers) to run the ~658 SQL Server tests in CI.
  - Run Fluent and Scaffolding suites in CI.
  - Add code-coverage collection + report artifact (coverage docs are manual, dated Jan 2026).
  - Stretch: PostgreSQL/MySQL job to back the README's cross-provider bulk copy claims.
- Acceptance criteria: CI runs the full suite (minus providers explicitly marked unsupported in CI) and publishes coverage.
- Estimate: 2-4 days

### PRD-016: Zero-warning build policy
- Priority: `P1`
- Status: `Done (2026-07-03, branch fixes/warnings-prd016)` - 200 warnings fixed to 0 (nullable annotations, trim-analysis attributes, generated-code defaults); TreatWarningsAsErrors on for src/.
- Scope: fix nullable-reference warnings (`CS8603` `JoinedQueryBuilderSelect.cs:164`, `CS8604` `DeleteCore.cs:84`, and any others surfaced by a clean build), then enable `TreatWarningsAsErrors` in `src/` projects.
- Acceptance criteria: clean Release build with warnings-as-errors on.
- Estimate: 1 day

## P1 - API Consistency and AOT Integrity (carried over from 2026-03-03)

### PRD-003: Async API consistency pass
- Priority: `P1`
- Status: `Planned`
- Scope: align `IDbConnection`/`DbConnection` behavior and docs for async methods.
- Acceptance criteria: no surprise runtime throws for documented async pathways.
- Estimate: 3-5 days

### PRD-004: Remove reflection from core hot/runtime paths
- Priority: `P1`
- Status: `Planned`
- Scope: reduce reflection in mapper/binder resolution and startup extension bootstrap.
- Acceptance criteria: AOT verification passes with documented reflection boundaries.
- Estimate: 4-7 days

### PRD-005: Sync/async query parity tuning
- Priority: `P1`
- Status: `Planned`
- Scope: align async list sizing and row count hints with sync path.
- Acceptance criteria: async path uses equivalent capacity strategy and avoids unnecessary reallocations.
- Estimate: 1-2 days

### PRD-006: Upsert hot-path optimization
- Priority: `P1`
- Status: `Planned`
- Scope: replace per-property reflection reads with cached compiled getters.
- Acceptance criteria: lower allocations/CPU in upsert microbenchmarks.
- Estimate: 2-4 days

## P2 - Trust, Docs, and Feature Completeness

### PRD-002: Benchmark result hygiene
- Priority: `P2` (was P0; blocked by PRD-012 — benchmarks do not currently compile in Release)
- Status: `Partial (2026-07-04)` - QueryBenchmarks measured full-config (SQLite + MariaDB) and published in docs/05-quality/reports/BENCHMARKS-2026-07-04.md; README bulk-copy claims requalified as native-API ranges. Remaining: proper bulk-suite runs with provider containers.
- Scope:
  - Standardize benchmark matrix and annotate unsupported scenarios.
  - Remove ambiguous summary claims not backed by current artifacts.
- Acceptance criteria: reproducible benchmark command set; report tables with explicit supported/unsupported markers.
- Estimate: 2-3 days

### PRD-017: Documentation consolidation and security policy
- Priority: `P2`
- Status: `Mostly done (2026-07-04)` - SECURITY.md added; stale Feb-2026 dotCover docs and the March readiness report archived to 99-archive; doc indexes repointed to the July reports. Remaining: refresh README cross-provider perf claims after a benchmark re-run (PRD-002).
- Scope:
  - Consolidate/archive conflicting status reports (coverage docs dated Jan 2026, multiple assessments); one authoritative readiness report + this tasklist.
  - Add `SECURITY.md` with a vulnerability-reporting policy.
  - Refresh README claims that lack automated backing (cross-provider bulk copy gains) or mark them as measured-on-date.
- Estimate: 1-2 days

### PRD-007: Fluent 3-way join parity
- Priority: `P2`
- Status: `Planned`
- Scope: async/order/paging parity for `IJoinedQuery3<>`.
- Estimate: 3-5 days

### PRD-008: CTE async method completion
- Priority: `P2`
- Status: `Planned`
- Scope: add missing async first/first-or-default CTE methods.
- Estimate: 1-2 days

### PRD-009: Scaffolding navigation properties
- Priority: `P2`
- Status: `Planned`
- Scope: generate opt-in reference/collection navigation properties from FK metadata.
- Estimate: 4-7 days

## P3 - Production Ecosystem

### PRD-011: Retry/resilience abstraction
- Priority: `P3`
- Status: `Planned`
- Scope: optional transient retry policy integration.
- Estimate: 3-5 days

## Done

### PRD-010: Interception and observability hooks
- Status: `Done (2026-03, merge a5f9254)`
- Delivered: `ICommandInterceptor`, `InterceptorPipeline`, `LoggingInterceptor`, `AuditInterceptor`, `LoggingConfiguration` with sensitive-parameter redaction, plus unit tests.

## Work Log

- 2026-03-03: Created readiness report and active prioritized tasklist.
- 2026-03-03: Started `PRD-001` implementation (schema-safe generated ordinal cache).
- 2026-03-03: Added source-generator regression tests for reordered columns across reader instances and result sets.
- 2026-07-02: Full local build+test validation. Found Release solution build broken (PRD-012). Added release-engineering items PRD-013..017. Marked PRD-010 done (logging/interception merged). Demoted PRD-002 to P2 (blocked by PRD-012). New report: PRODUCTION-READINESS-2026-07-02.md.
- 2026-07-03: Executed PRD-012, PRD-001, PRD-016, PRD-013, PRD-014 (code side), PRD-015, PRD-017 (partial) in feature branches merged to dev with --no-ff.
- 2026-07-04: Enterprise readiness pass (see ENTERPRISE-READINESS-2026-07-04.md): ConfigureAwait(false) sweep + CA2007 enforcement (PROD-101); version centralization, package validation, IsPackable hygiene - a tagged release would previously have published sample apps to nuget.org (PROD-102); High-severity transitive CVE remediation SQLitePCLRaw/Regex + EOL Microsoft.Extensions 6.0.0 bumps, vulnerable scan now clean (PROD-103); dependabot (PROD-104); coverlet + coverage gap inventory (PROD-105); docs consolidation (PROD-106). Found and fixed: NULL->0 generated-mapper bug (nullable value types), analyzer missing from published package, DialectFixture ship_postal_code bug, SP script snake/Pascal mismatches. Full core suite verified against SQL Server 2022 container: 3,587/3,589. Remaining manual steps: create main branch, set NUGET_API_KEY, push dev.
