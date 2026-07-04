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

1. ~~Full `Microsoft.Extensions.DependencyInjection` dependency~~ **RESOLVED
   (PROD-108, 2026-07-04)**: Abstractions-only; the ASP0000-pattern
   `ApplyJauntyInterceptors(IServiceCollection)` overload removed (breaking,
   pre-first-publish).
2. ~~`System.CommandLine` beta in Jaunty.Scaffolding.Cli~~ **RESOLVED (PROD-107)**:
   2.0.9 stable exists; CLI migrated to the GA API (`SetAction`/`ParseResult`),
   smoke-tested against the Northwind SQLite reference db (list-tables, scaffold
   --dry-run, required-option validation).

3. **Sync-over-async** (`.GetAwaiter().GetResult()`) in 5 sync internals
   (`ExecuteReader.cs` ×3, `QueryCore.cs`, `ExecuteNonQueryCore.cs`). With PROD-101's
   ConfigureAwait(false) the deadlock risk is mitigated, but true sync ADO.NET calls
   would be cleaner. Fixing may change sync-path behavior — decide appetite.

4. ~~`JauntyConfig` mutable global statics~~ **RESOLVED (PROD-111, 2026-07-04)**:
   implemented - volatile fields, synchronized interceptor mutation, synchronized
   TypeHandlerRegistry counts; contract documented on the class (Reset() is
   test-only and not atomic as a whole).

5. ~~PackageIcon~~ **RESOLVED (PROD-110, 2026-07-04)**: docs/_assets/jaunty.png
   processed via ImageMagick (crop/smooth/128px, transparent corners) into
   docs/_assets/icon.png; packed into all packages.

I've dropped jaunty.png an image / logo under docs/_assets/, let's convert it to an appopriate icon but smooth out the edges first through one of the vision compatible models under openrouter or if you're able to then do it youself, or use imagemagick: C:\home\tools\ImageMagick

6. ~~CalVer vs SemVer~~ **RESOLVED (PROD-109, 2026-07-04)**: SemVer; dev baseline
   `1.0.0-rc.1`, release tags `vMAJOR.MINOR.PATCH`.

7. ~~TFM public-API divergence~~ **RESOLVED (PROD-112, 2026-07-04)**:
   ASYNC_ENUMERABLE_SUPPORT extended to netstandard2.0 (Bcl.AsyncInterfaces), so both
   TFMs expose identical IAsyncEnumerable streaming; ValueTask variants removed
   (breaking on ns2.0, pre-first-publish). EntityMetadata.ParameterMap unified to
   IReadOnlyDictionary. CompatibilitySuppressions.xml deleted - validation now has a
   zero baseline. net472 suite (ns2.0 assembly) green: 2522/0. Still open (folded
   into decision 3's category): making `Jaunty.Internals.*` public types internal.

Let's use #if / #else compiler directives to target both

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
- 2026-07-04: PROD-101..107 executed and merged to dev (--no-ff each):
  ConfigureAwait sweep + CA2007 gate; version centralization + package validation
  + IsPackable hygiene (release would have published sample apps to nuget.org);
  CVE remediation (SQLitePCLRaw 2.1.x, Regex 4.3.0) + EOL ME.* 6.0.0 bumps;
  dependabot; coverlet + COVERAGE-GAPS-2026-07-04.md; docs consolidation;
  System.CommandLine beta4 -> 2.0.9. Final gate: Release build 0 errors,
  4369 passed / 0 failed / 714 env-skips, 7 packages pack clean,
  vulnerable+deprecated scans clean (xunit v2 'legacy' notice excepted).

## PROD-120 (RESOLVED 2026-07-04): bulk-path defects found by measurement

- SQLite `BulkInsert` is 16x SLOWER than a plain transactional ADO.NET loop
  (334ms vs 20.6ms @ 10k rows). Provider code looks right but has 0% coverage;
  core `SQLiteDialect.CreateBulkCopyProvider()` returns null - verify which path
  actually executes (native provider vs BulkInsertLoop) and profile.
- MariaDB native bulk errors outright (NA in benchmarks) while linq2db works on
  the same server - suspect MySqlBulkLoader local-infile config; provider should
  fail with an actionable message.
- SQL Server bulk unverified: needs correct JAUNTY_TEST_SQLSERVER credentials
  for the local container (or CI container job).
- Evidence: docs/05-quality/reports/BENCHMARKS-2026-07-04.md Update 3.
- Resolution (Updates 4-5): MySQL/MariaDB provider rewritten to chunked
  multi-row INSERT (16.1x @10k, live-tested); SQLite routed to the prepared
  loop via IsSqliteDialect() (1.005x parity, provider deleted).

## PROD-121 (RESOLVED 2026-07-04): SqlServer native bulk was never functional

- `SqlServerBulkCopyProvider` threw `MissingMethodException` on every call:
  `MapBulkCopyOptions` passed a boxed `Int32` where the `SqlBulkCopy` ctor
  takes the `SqlBulkCopyOptions` enum. Fixed with `Enum.ToObject`.
- Second defect: no `ColumnMappings`, so ordinal mapping hit the destination
  IDENTITY column. Fixed with by-name mappings from the source reader.
- Measured (Warm): 3.5x @100, 17.6x @1k, **36.6x @10k** vs transactional
  loop; matches linq2db's SqlBulkCopy within noise; 6.6x less allocation.
- Live tests: tests/Jaunty.Tests/Unit/BulkCopy/SqlServerBulkCopyProviderTests.cs.
- Evidence: BENCHMARKS-2026-07-04.md Update 5.
- Residual (harness only, open): the EF Core competitor benchmark fails on
  SqlServer ("Cannot insert explicit value for identity column") - EfProduct
  key not ValueGeneratedOnAdd for this provider. Competitor number missing;
  no Jaunty impact.

- Residual resolved (PROD-122 task 2): EfProduct key lacked ValueGeneratedOnAdd()
  and IterationSetup did not reset product_id to 0 between iterations. Both fixed.
- TryEnhanceWithBulkCopy mystery resolved (PROD-122 task 3): TryEnhanceWithBulkCopy
  in SqlDialectFactory reflection-calls BulkCopyDialectFactory.GetDialect, but that
  method guards on BulkCopyDialectFactory._enabled (default false). The enabled flag
  is only set by JauntyReflectionExtensions.UseNativeBulkCopy(), which benchmarks
  call in GlobalSetup but tests never call. Result: in the test process the factory
  type resolves successfully via Type.GetType, GetDialect is invoked, but _enabled
  is false so it returns the base dialect unchanged — identical to the unenhanced
  path. No exception is swallowed; no assembly load fails; no initialization defect.
  The "silent failure" is correct by design: tests opt out of native bulk copy by not
  calling UseNativeBulkCopy(), and SqliteBulkPathDiagnosticTests seeing a plain
  SQLiteDialect is the expected result. No src change required.

## PROD-122 (IN PROGRESS 2026-07-04): cleanup sprint

- Task 1 (cross-TFM contention): Added `<TestTfmsInParallel>false</TestTfmsInParallel>`
  to tests/Jaunty.Tests/Jaunty.Tests.csproj. `dotnet test` without -f now runs net8.0
  then net472 sequentially, eliminating the ~7-30 MariaDB/Postgres Write test failures
  caused by concurrent TFM runs hitting the same live databases.
  Acceptance run pending (Task 1 commit: 3f7f03f).

- Task 2 (EF Core SqlServer identity INSERT): Two fixes in benchmarks/Jaunty.Benchmarks.
  (a) EfProduct.cs OnModelCreating: added ValueGeneratedOnAdd() on product_id so EF Core
  metadata is correct for all four providers. (b) BulkCopyBenchmarks.cs IterationSetup:
  reset product_id = 0 on each EfProduct before every iteration — after SaveChanges EF
  Core writes back db-assigned IDs; subsequent iterations carried non-zero keys that
  SqlServer rejected as explicit identity values. Smoke-checked --quick against SqlServer;
  all three BatchSize runs completed without error (exit code 0, no NA).
  Commit: f854427.

- Task 3 (TryEnhanceWithBulkCopy silent failure): Not a defect. TryEnhanceWithBulkCopy
  resolves BulkCopyDialectFactory via Type.GetType and successfully invokes GetDialect,
  but BulkCopyDialectFactory._enabled defaults to false. UseNativeBulkCopy() sets it;
  benchmarks call UseNativeBulkCopy() in GlobalSetup, tests do not. Tests opt out by
  design; SqliteBulkPathDiagnosticTests seeing a plain SQLiteDialect is correct.
  No exception swallowed, no load failure, no src change required. Documented in
  PROD-121 residual section above. Commit: 4ac079b.

- Task 4 (git housekeeping): Deleted 83 local branches fully merged into dev
  (audit/*, chore/*, docs/*, feat/*, feature/*, features/*, fix/*, fixes/*,
  organizations/*, refactors/*, specs/*). Remote prune completed. Working tree clean.

- Task 5 (tasklist bookkeeping): This section.
