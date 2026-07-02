# Jaunty Production Readiness Report (2026-07-02)

## Scope and Method

- Full local validation on branch `dev` (commit `7b57cc0`): `dotnet build Jaunty.slnx -c Release`, full test suites per project, package metadata audit, dependency vulnerability scan, secrets scan, CI workflow review, documentation review.
- Unlike the 2026-03-03 report (source/document review only), this assessment executed the build and the complete test suite.
- Supersedes: [PRODUCTION-READINESS-2026-03-03.md](PRODUCTION-READINESS-2026-03-03.md).

## Verdict

**Not production-ready as-is.** The library code itself is in good shape (healthy test suite, no vulnerable dependencies, no dead TODOs, interception/logging shipped), but three things block release today:

1. The Release solution build is broken (CI is red on its primary job).
2. There is no release pipeline at all: no `main` branch, no publish workflow, no changelog, incomplete NuGet package metadata.
3. CI verifies only ~2.7% of the core test suite (97 of 3,589 tests), so cross-provider correctness claims are unproven in automation.

The P0 correctness risk from March (schema-safe ordinal caching, PRD-001) remains formally open — "runtime validation pending".

With the defect fixed and a focused release-engineering pass (est. 2-3 weeks), the project is releasable for controlled workloads.

## Verified Test Results (2026-07-02, local, Release, net8.0)

| Suite | Passed | Failed | Skipped | Total | Notes |
|---|---|---|---|---|---|
| Jaunty.Tests | 2,931 | 658 | 0 | 3,589 | All 658 failures are SQL Server connection errors (no local instance). Every failed test name matches SqlServer/Mssql. Zero logic failures. |
| Jaunty.Tests (SQLite filter, CI-equivalent) | 97 | 0 | 0 | 97 | This is all CI runs of the core suite. |
| Jaunty.Fluent.Tests | 787 | 0 | 0 | 787 | |
| Jaunty.FlatFiles.Tests | 20 | 0 | 0 | 20 | |
| Jaunty.FlatFiles.DuckDB.Tests | 301 | 0 | 1 | 302 | |
| Jaunty.Scaffolding.Tests | 177 | 0 | 0 | 177 | |
| **Total** | **4,216** | **658** | **1** | **4,875** | All failures are missing-infrastructure, not defects. |

PostgreSQL / MySQL / MariaDB connection strings exist in `tests/Jaunty.Tests/appsettings.json` but those providers are exercised neither locally nor in CI.

## Defects (verified)

### D1 — Release solution build fails (breaks CI)

`dotnet build Jaunty.slnx -c Release` fails with 3 errors:

```
benchmarks/Jaunty.Benchmarks/Entities/JauntyProduct.cs(10,38): error CS0535: 'JauntyProduct' does not implement interface member 'IMapped<JauntyProduct>.ReadEntity(IDataReader)'
benchmarks/Jaunty.Benchmarks/Entities/TpcLineItem.cs(9,36): error CS0535 (same)
benchmarks/Jaunty.Benchmarks/Entities/TpcOrder.cs(9,33): error CS0535 (same)
```

Root cause: the benchmark entities are `partial class X : IMapped<X>` and rely on `Jaunty.SourceGenerator` to emit `ReadEntity`. `Jaunty.slnx` excludes the generator from Release solution builds:

```xml
<Project Path="src/Jaunty.SourceGenerator/Jaunty.SourceGenerator.csproj">
  <Build Solution="Release|*" Project="false" />
</Project>
```

So in a Release solution build the analyzer assembly is never produced, generation does not run, and compilation fails. Debug builds succeed. **CI runs exactly this Release solution build (`.github/workflows/ci.yml`), so the Build & Test job — and everything gated on it — fails on a fresh checkout.**

### D2 — Nullable-reference warnings in shipping code

Observed during the Release build (before it aborted): `CS8603` in `src/Jaunty.Fluent/Builders/Join/JoinedQueryBuilderSelect.cs:164` and `CS8604` in `src/Jaunty/Internals/Write/DeleteCore.cs:84`, among others. `TreatWarningsAsErrors` is not set anywhere, so these can accumulate silently.

### D3 — PRD-001 (schema-safe ordinal cache) still open

The March P0 — source-generated mapper ordinal cache vulnerable to stale mappings when result shape changes — is marked "In Progress ... runtime validation pending" and no completing commit was found. The generator's `CacheEntry` uses an `_initialized` boolean rather than shape validation (per commit history `8dc2257`, `3a02660`). This remains the top correctness risk for the source-generated path.

## Missing for Production

### M1 — No release pipeline

- The remote has **only `origin/dev`** — no `main`/release branch despite the documented release flow (`/release-cut`, `/release-ship`).
- Only one workflow (`ci.yml`, dev-only). **No NuGet publish workflow**, no tag-triggered release, no GitHub Releases.
- **No CHANGELOG or release notes** anywhere in the repo.
- All packages are pinned at `Version 2026.01.01` (six months stale); no versioning strategy is documented.

### M2 — Incomplete NuGet package metadata

`GeneratePackageOnBuild` is `true`, but across all 7 packable projects the following are absent:

- `PackageLicenseFile` / license asset packed into the nupkg (critical: the license is a custom proprietary one, so nuget.org will reject or mislabel the package without an explicit license file; `PackageRequireLicenseAcceptance` should also be considered).
- `PackageReadmeFile`, `PackageIcon`.
- `GenerateDocumentationFile` (XML docs were written — merge `717498c` — but are not packed).
- SourceLink / `PublishRepositoryUrl` / `EmbedUntrackedSources` / `ContinuousIntegrationBuild` / symbol packages (`snupkg`).
- Only the FlatFiles projects set `PackageTags`.

As configured, publishing today would ship packages with no license, no readme, and no IntelliSense.

### M3 — CI test coverage gap

CI runs 97 SQLite tests + FlatFiles/DuckDB. Not run in CI: 3,492 core tests (SQL Server portion ~658 requires a server), Fluent (787), Scaffolding (177). No SQL Server service container / Testcontainers usage. PostgreSQL/MySQL/MariaDB paths (including the README's native bulk copy performance claims for those providers) have no automated verification at all. No code-coverage measurement in CI; the coverage reports in `docs/05-quality/code-coverage/` are manual and dated January 2026.

### M4 — Documentation staleness and conflicts

- `PRODUCTION-READINESS-TASKLIST.md` was last updated 2026-03-03 and does not reflect merged work (interception/logging `a5f9254` fulfills PRD-010; XML docs merged).
- Multiple overlapping status/assessment documents (already flagged as P1 finding #6 in March) remain unconsolidated.
- No `SECURITY.md` / vulnerability-reporting policy — expected for a data-access library asking for production trust.

## What Is in Good Shape (verified)

- **Test suite quality**: 4,875 tests, zero logic failures; failures are purely environmental.
- **Dependencies**: `dotnet list package --vulnerable` and `--deprecated` are clean for the core package.
- **Code hygiene**: zero `TODO`/`FIXME`/`HACK` comments in `src/` and `tests/`; all `NotSupportedException` usages are legitimate documented guards.
- **Secrets**: no credentials in the repo (test appsettings use localhost with empty passwords).
- **Features shipped since March**: command interception pipeline (`ICommandInterceptor`, `InterceptorPipeline`, `LoggingInterceptor`, `AuditInterceptor`) with sensitive-parameter redaction (closes PRD-010); complete XML documentation; DI integration design in progress (`7b57cc0`).
- **CI structure**: sound skeleton (restore/build/test/AOT verify/AOT publish, NuGet caching, concurrency groups) — it just fails at the build step and under-tests.

## Recommended Path to Production

Ordered execution plan with acceptance criteria: [PRODUCTION-READINESS-TASKLIST.md](../../06-releases/tasklists/PRODUCTION-READINESS-TASKLIST.md) (updated 2026-07-02).

Summary of the critical path:

1. Fix the Release build / CI (D1) — hours.
2. Close PRD-001 runtime validation (D3) — days.
3. Release engineering: package metadata, publish workflow, `main` branch, versioning + changelog (M1, M2) — ~1 week.
4. CI hardening: SQL Server container, full suite, coverage reporting (M3) — days.
5. Then the carried-over P1/P2 API-consistency and feature-parity items.
