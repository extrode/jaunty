# Enterprise Readiness Pass — 2026-07-04

Full-repo comb for paying-enterprise-customer readiness. Hybrid method: fresh sweep
cross-checked against [PRODUCTION-READINESS-2026-07-02.md](../../05-quality/reports/PRODUCTION-READINESS-2026-07-02.md),
[PRODUCTION-READINESS-TASKLIST.md](PRODUCTION-READINESS-TASKLIST.md) (PRD-*), and the
merged audit fix series (`fix/audit-001` … `fix/audit-015`).

Constraint for this pass: **additive-only public API** (no breaking changes; breaking
candidates land in "Needs decision").

## Verified state (2026-07-04, local, Release, net8.0)

- `dotnet build Jaunty.slnx -c Release`: **success, 0 errors, 0 src warnings**
  (2 CS8600 warnings in `tests/Jaunty.Tests` — src-only `TreatWarningsAsErrors`).
- `dotnet test` (net8.0, no local DB containers): **4369 passed / 0 failed / 714 skipped**.
  Skips are SqlServer/MySql/Postgres dialect tests needing containers.
  | Suite | Passed | Skipped |
  |---|---|---|
  | Jaunty.Tests | 3079 | 713 |
  | Jaunty.Fluent.Tests | 787 | 0 |
  | Jaunty.FlatFiles.DuckDB.Tests | 301 | 1 |
  | Jaunty.Scaffolding.Tests | 177 | 0 |
  | Jaunty.FlatFiles.Tests | 20 | 0 |
  | Jaunty.SourceGenerator.Tests | 5 | 0 |
- PRD-012/001/013/014(code)/015/016 confirmed done in repo (release.yml, SourceLink,
  snupkg, TreatWarningsAsErrors, CHANGELOG, SECURITY.md all present).

## Workstreams — executed this pass (priority order)

### PROD-101 (P1): ConfigureAwait(false) sweep — `fix/prod-101-configureawait`
- 774 `await` vs 459 `ConfigureAwait` in `src/`; ~33 files with awaits and zero
  ConfigureAwait, including core read paths (`QueryAsync`, `QueryFirstAsync`, …),
  streaming, stored procedures, scaffolding readers, bulk-copy providers.
- Library targets netstandard2.0 → sync-context deadlock risk for WinForms/WPF/legacy
  ASP.NET consumers calling `.Result`/`.Wait()` on our tasks.
- Scope: add `.ConfigureAwait(false)` to all awaits in `src/` libraries (incl.
  `await using` / `await foreach` forms). Exclude `Jaunty.Scaffolding.Cli` `Program.cs`
  (app entry point, no sync context concern) — include the rest of the Cli for consistency.
- Guard: enable CA2007 as error for `src/` so it cannot regress.
- Acceptance: zero unconfigured awaits in `src/` libraries; Release build green.

### PROD-102 (P1): Versioning & packaging consistency — `fix/prod-102-packaging`
- `<Version>2026.01.01</Version>` hardcoded in 7 csproj while release.yml overrides with
  `-p:Version=<tag>` → local packs and tag packs disagree; CHANGELOG documents CalVer.
  Centralize version in `src/Directory.Build.props`; remove per-project copies.
- `GeneratePackageOnBuild=true` in 6 projects: packs on every build (slow, litters bin/,
  and local packs carry the stale hardcoded version). Release pipeline packs explicitly.
  Remove the property.
- `Microsoft.Bcl.AsyncInterfaces` pinned 10.0.3 in 3 projects but 10.0.1 in
  Jaunty.Scaffolding — align to 10.0.3.
- Add `EnablePackageValidation` to packable projects.
- Acceptance: `dotnet pack -p:Version=X` produces consistently versioned packages;
  build green.
- **Found during execution (fixed):** `dotnet pack Jaunty.slnx` packed
  `Jaunty.Benchmarks` and the 3 `NativeAOT-*` samples — release.yml pushes
  `packout/*.nupkg`, so a tagged release would have published sample apps to
  nuget.org. Fixed with `IsPackable=false` (also on SourceGenerator, which ships
  inside Beparey.Jaunty and errored NU5128 when packed standalone).
- **Found during execution (baselined, see decision 7):** package validation
  detected 15 public members present in netstandard2.0 but missing from net8.0
  (`QueryStreamAsync`/`QueryPartialStreamAsync`/`QueryPartialUnbufferedAsync`/
  `GetAllStreamAsync` ValueTask variants; `EntityMetadata.ParameterMap`).
  Baselined in `src/Jaunty/CompatibilitySuppressions.xml` so validation still
  catches *new* divergence.

### PROD-103 (P1): Dependency hygiene — `fix/prod-103-dependencies`
- Run `dotnet list package --vulnerable --include-transitive` and `--deprecated`;
  record results here; bump any flagged pins (additive version bumps only).
- netstandard2.0 path pins Microsoft.Extensions.* **6.0.0** (out of support Nov 2024);
  net8.0 path pins 8.0.0. Bump ns2.0 pins to 8.0.x (still ns2.0-compatible).
- Acceptance: vulnerable/deprecated scans clean; build + tests green on both TFMs.

### PROD-104 (P1): CI/supply-chain hardening — `fix/prod-104-ci`
- Add `.github/dependabot.yml` (nuget + github-actions ecosystems).
- Verify release.yml step-level `if: secrets.NUGET_API_KEY` behavior and pin
  action versions where floating.
- Acceptance: workflows lint clean (actionlint if available), dependabot config valid.

### PROD-105 (P1): Coverage instrumentation + gap inventory — `fix/prod-105-coverage`
- No coverage collector in any test project today. Add `coverlet.collector` to the 6
  test projects (infra only — **no test authoring**, per scope).
- Run `dotnet test --collect:"XPlat Code Coverage"`, aggregate, and write
  `docs/05-quality/reports/COVERAGE-GAPS-2026-07-04.md`: per-assembly coverage,
  files/members below threshold, and the 713 env-skipped test inventory.
- Acceptance: report exists with concrete uncovered-member list for handoff.

### PROD-106 (P2): Docs truth pass — `fix/prod-106-docs`
- PRD-017 remainder: archive superseded status/coverage docs into `docs/99-archive/`
  (move, not delete), update PRODUCTION-READINESS-TASKLIST.md statuses to reflect
  2026-07-03/04 work, refresh README claims that lack automated backing.
- Acceptance: single authoritative readiness trail; no doc contradicts CI reality.

## Needs decision (user) — not executed

1. **Core lib depends on full `Microsoft.Extensions.DependencyInjection`** (not just
   Abstractions) — heavyweight transitive for a micro-ORM. Removing = dependency
   removal (requires your explicit approval) and could break consumers relying on the
   transitive. Recommend: drop to Abstractions-only in next minor.
2. ~~`System.CommandLine` beta in Jaunty.Scaffolding.Cli~~ **RESOLVED (PROD-107)**:
   2.0.9 stable exists; CLI migrated to the GA API (`SetAction`/`ParseResult`),
   smoke-tested against the Northwind SQLite reference db (list-tables, scaffold
   --dry-run, required-option validation).
3. **Sync-over-async** (`.GetAwaiter().GetResult()`) in 5 sync internals
   (`ExecuteReader.cs` ×3, `QueryCore.cs`, `ExecuteNonQueryCore.cs`). With PROD-101's
   ConfigureAwait(false) the deadlock risk is mitigated, but true sync ADO.NET calls
   would be cleaner. Fixing may change sync-path behavior — decide appetite.
4. **`JauntyConfig` mutable global statics** (resolvers, logger, interceptor pipeline):
   no thread-safety guarantees documented. Options: document "configure at startup
   only" contract vs. freeze-after-first-use semantics (behavioral change).
5. **PackageIcon**: absent; needs an actual icon asset from you.
6. **CalVer `2026.01.01` vs SemVer**: CHANGELOG documents CalVer; NuGet ecosystem and
   package validation tooling assume SemVer. Confirm CalVer is intentional. Note NuGet
   normalizes `2026.01.01` to `2026.1.1` in package filenames.
7. **TFM public-API divergence (PRD-003 evidence)**: net8.0 exposes streaming as
   `IAsyncEnumerable<T>` while netstandard2.0 exposes `ValueTask<IEnumerable<T>>`
   members of the same names — same signature, different return type, so the two
   cannot coexist on one TFM. Unifying is a breaking change on one side. 15 members
   baselined in `CompatibilitySuppressions.xml`; decide the unification story before
   v1 GA. Related: `Jaunty.Internals.Entity.EntityMetadata` is a public type in an
   `Internals` namespace — consider making internal (breaking).

## Handoff tasklist — follow-up work (tests/comments/docs; per scope, not done here)

1. Fix 2 `CS8600` warnings in `tests/Jaunty.Tests/Integration/TypeHandlers/TypeHandlerRoundTripTests.cs`
   (lines 157, 229); then extend `TreatWarningsAsErrors` to `tests/`.
2. Write tests for every member listed in `COVERAGE-GAPS-2026-07-04.md` (produced by
   PROD-105) below the agreed threshold.
3. XML doc comments for `Jaunty.SourceGenerator` internals (March audit gap) and any
   public member the build's doc-file generation flags.
4. README files for `Jaunty.Scaffolding` / `Jaunty.Scaffolding.Cli` project directories.
5. MySQL test-isolation cleanup in `Jaunty.Tests` (March audit: 47 Release-mode failures
   from shared table state; env-dependent).
6. Benchmark result hygiene (PRD-002): re-run and publish BenchmarkDotNet results,
   replace stale README numbers.

## Deferred / feature gaps (tracked, intentionally not in this pass)

- PRD-003 async API consistency remainder, PRD-004 reflection-free hot paths,
  PRD-005 sync/async parity tuning, PRD-006 upsert optimization (P1 carried; perf
  workstreams need benchmark baselines first — see handoff #6).
- PRD-007 Fluent 3-way join parity, PRD-008 CTE async completion, PRD-009 scaffolding
  navigation properties, PRD-011 retry/resilience (features, not readiness blockers).
- SplitOn implementation (explicitly deferred by owner in spec 002).

## Manual cleanups (user) — carried from PRD-014 + this pass

1. Create `main` branch on origin; protect it.
2. Configure `NUGET_API_KEY` secret in GitHub repo settings.
3. Decisions 1-6 above.

## Work log

- 2026-07-04: Pass started. Recon + verified state recorded. Workstreams defined.
