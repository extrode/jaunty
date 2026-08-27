# Testing strategy implementation — operationalizing the solver & fuzz handover

Status: in progress · Branch: `test/testing-strategy-phase0` · Input: `docs/testing-strategy-handover.md`

## Context

`docs/testing-strategy-handover.md` catalogs fuzz/solver/property/differential testing techniques
and, more usefully, a **gate** for deciding where each is worth adding to a codebase that already
has 8,471 passing tests. In jaunty it was an orphan — not merely un-actioned but **untracked**:
the file was never committed, so no spec, plan or todo referenced it and `git log` had never seen
it. The sibling repo operationalized the same document on 2026-08-24
(`../jauntyq/docs/plans/2026-08-24-011-testing-strategy-implementation.md`); this plan is
jaunty's, and it re-measures rather than inherits, because two of jauntyq's headline findings do
not reproduce here.

## Ground rules

1. **Nothing is removed.** Existing tests, fixtures, scripts and CI jobs stay; work extends them.
2. **The handover's own gate governs** (§1): critical path + real constraint surface + bug
   history + mutation-confirmed gap. Where the gate says don't, record why.
3. **CsCheck** as the property library (no transitive deps, deterministic repro, shrinking).
4. Test-side packages only; `src/` is untouched.
5. Every new test gets a RED-phase check against a deliberately broken target before it is
   trusted green.

## Relevance triage of the handover

Recorded before any work, so the "we skipped it" calls are visible rather than implicit.

| Handover item | Verdict for jaunty |
|---|---|
| §6 SQL-to-C# generator fuzz | **Remapped.** Jaunty's generator does not parse SQL. The analogue is the runtime `SqlParameterParser` |
| §6 shape guard / schema drift | Not applicable — a JauntyQ concept |
| §6 DbType boundaries across dialects | Applies: 4 dialects, `IDecimalBindingDialect` |
| §6 `@stream` lifecycle | Remapped to `GetAll` unbuffered / `GridReader` `IAsyncEnumerable` paths |
| §6 cross-dialect differential | Deferred — per-engine suites plus the sakila/conduit/eShopOnWeb torture ports already give implicit agreement |
| §6 zero-alloc gates | Applies; three test files already do this ad hoc |
| §6 incremental generator caching | **Already satisfied** — `GeneratorCachingTests` (308 lines) uses `TrackIncrementalGeneratorSteps` and asserts on `IncrementalStepRunReason` |
| §6 trim analyzer | Verify only; `IsTrimmable`/`IsAotCompatible` are already set in `src/Directory.Build.props:11-12` |
| §3 Microsoft.Z3 | **Rejected.** The dialect boundary surface is hand-enumerable in one sitting, and the handover itself says Z3 output lands as ordinary checked-in unit tests — which the boundary theories produce directly, without a model to maintain |
| §3 FsCheck Command model | Gate-check candidate for `Jaunty.Fluent`; decided from the mutation baseline, not by default |
| §3 Verify.SourceGenerators | Skipped — `Jaunty.SourceGenerator.Tests` already asserts emitted source directly |
| §3 cargo-fuzz / FFI marshaling | Not applicable — no Rust, no C ABI, no `unsafe` block in `src/` |
| §7 fit notes for Loom, Warden, Strata, iqr4, coding-agent, hearth… | Out of scope — cross-portfolio triage for other repos |

## Phase 0 — measure first (handover §2 triage)

### Coverage tooling

`coverage.runsettings` + `scripts/coverage.ps1` added; every project under `tests/` already
carried `coverlet.collector` 10.0.1, so no project file needed a reference. The four
`samples/torture-test-*` ports are deliberately out of scope: they sit outside `Jaunty.slnx`,
are not part of `dotnet test Jaunty.slnx`, and carry no collector.

**jauntyq's `CompilerGeneratedAttribute` finding does not reproduce here, and the measurement
says so.** There, excluding that attribute hid every async and iterator body. Measured on
`Jaunty.FlatFiles.Tests` with coverlet.collector 10.0.1, the same run reports:

| ExcludeByAttribute | Classes | State machines | Lines |
|---|---:|---:|---:|
| `Obsolete,GeneratedCodeAttribute` (kept) | 583 | 240 | 38,130 |
| `…,CompilerGeneratedAttribute` (excluded) | 485 | 240 | 35,652 |
| **hidden by the exclusion** | **98** | **0** | **2,478** |

Async and iterator state machines are unaffected on this collector version. What the exclusion
hides is 77 `<>c__DisplayClass` closures, 18 `<>c` lambda caches and 3 option/payload types —
2,478 lines of predicate and callback bodies. Still worth keeping visible, but for a narrower
reason than the sibling plan gives, and the config comment says so rather than repeating the
inherited claim.

### Coverage × complexity cross-reference

8 suites, 8,471 tests, 0 failures. Union-merged per `(file, line)` across reports, nested
compiler-generated classes folded into their outer class, partial classes keyed by name.
Probe: `tmp/coverage-complexity.py` (scratch).

**Product total: 88.8 % line coverage over 25,469 lines, 1,128 units of complexity that no test
executes.**

| Assembly | Cx | Lines | Cov % | Uncovered Cx |
|---|---:|---:|---:|---:|
| Jaunty | 6950 | 12,895 | 89.2 | **746** |
| Jaunty.Fluent | 1897 | 5,812 | 87.0 | **251** |
| Jaunty.Extensions.Reflection | 420 | 1,411 | 86.0 | 73 |
| Jaunty.Scaffolding | 465 | 1,521 | 81.9 | 31 |
| Jaunty.SourceGenerator | 205 | 1,099 | 91.5 | 18 |
| Jaunty.FlatFiles.DuckDB | 168 | 2,072 | 94.5 | 8 |
| Jaunty.FlatFiles | 64 | 358 | 99.2 | 1 |
| Jaunty.Scaffolding.Cli | 4 | 301 | 98.7 | 0 |

**Complex but thinly tested** — Cx ≥ 40 and under 85 % covered, by uncovered complexity:

| Class | Assembly | Cx | Lines | Cov % | Uncov Cx |
|---|---|---:|---:|---:|---:|
| `Internals.Parameters.SqlParameterParser` | Jaunty | 200 | 165 | **53.3** | **93** |
| `Fluent.Expressions.WhereExpressionVisitor\`1` | Jaunty.Fluent | 232 | 454 | 84.8 | 35 |
| `Reflection.BulkCopy.PostgreSqlBulkCopyProvider` | Jaunty.Extensions.Reflection | 52 | 131 | 51.1 | 25 |
| `Fluent.Expressions.SelectExpressionVisitor\`1` | Jaunty.Fluent | 127 | 179 | 81.6 | 23 |
| `Fluent.Expressions.JoinedGroupByExpressionVisitor` | Jaunty.Fluent | 102 | 193 | 84.5 | 16 |
| `Fluent.JoinedQuery4Builder\`4` | Jaunty.Fluent | 44 | 244 | 64.3 | 16 |
| `Fluent.Expressions.ExistsExpressionVisitor\`2` | Jaunty.Fluent | 49 | 149 | 68.5 | 15 |
| `Fluent.Expressions.JoinExpressionVisitor4\`4` | Jaunty.Fluent | 50 | 149 | 69.8 | 15 |
| `Interceptors.InterceptorPipeline` | Jaunty | 60 | 119 | 84.9 | 9 |
| `Internals.Read.DrDispatcher` | Jaunty | 42 | 40 | 82.5 | 7 |

One product class sits at 0 % with Cx ≥ 5: `Scaffolding.Providers.PostgreSql.PostgreSqlSchemaReader`
(126 lines, Cx 12) — a live-Postgres path the offline suite never enters.

### The headline finding: `SqlParameterParser`'s 53.3 % is a shipped-code gap, not a testing gap

The ranking put the parser first, which the plan predicted from code shape. The reason it is at
53.3 % is not what the ranking implies, and it is worse.

The parser ships **twice**. `ExtractParameterNamesSpan` serves net8.0/net10.0 behind
`#if NET8_0_OR_GREATER`; `ExtractParameterNamesClassic` is a hand-maintained duplicate compiled
into every target but only *called* on netstandard2.0 — which is the assembly .NET Framework
consumers load. Splitting the coverage by line range:

| Implementation | Lines | Covered | Rate |
|---|---:|---:|---:|
| `…Span` (L28–210), live on net8.0/net10.0 | 81 | 81 | **100 %** |
| `…Classic` (L213–411), live on netstandard2.0 | 80 | 3 | **3.8 %** |

`Jaunty.Tests` does target net472 and 50 parser tests pass there locally, exercising the classic
body. **`ci.yml` has no net472 leg** — it is `ubuntu-latest` throughout — so no CI run has ever
executed the code path that .NET Framework users get. Two hand-maintained parsers that must agree,
one of them tested only when someone happens to run the net472 leg on a Windows box, is a silent
wrong-parameter-set waiting to ship. That is the gap Phase 1 targets first.

### Mutation baselines

`stryker-config.json` per test-project directory (Stryker resolves it from there, not the repo
root); `dotnet-stryker` 4.16.0 pinned in `.config/dotnet-tools.json`; `StrykerOutput/` gitignored.
Scoped by the ranking above rather than run whole — `mutate` globs limit each run to the files the
cross-reference actually indicted:

| Config | Mutates | Rationale |
|---|---|---|
| `tests/Jaunty.Tests` | `**/Internals/Parameters/**`, `**/Dialects/**` | rank 1 and the Phase-1 boundary target |
| `tests/Jaunty.Fluent.Tests` | `**/Expressions/**` | ranks 2, 4, 5, 7, 8 are all expression visitors |

`Jaunty.FlatFiles` is deliberately **not** configured: at 99.2 % line coverage and 1 unit of
uncovered complexity it fails gate item 4 before a run is worth its CI minutes.

Thresholds are `break: 0` — mutation score is an artifact to read, not a gate that fails a build,
because a score drop is a prompt to look rather than a defect on its own.

#### Why the first three attempts produced nothing, and what actually fixed it

Three runs were abandoned before any report existed: `Jaunty.Tests` after **4h20m** (2,886
CPU-seconds, 960 MB, output directory holding nothing but its own `.gitignore`), then two on
`Jaunty.Fluent.Tests`. All were progressing, not deadlocked — just far too slow.

The first diagnosis was that the live-database integration suites in `Jaunty.Tests` made mutation
testing inherently a nightly-tier job. **That was wrong as the primary cause.** `Jaunty.Fluent.Tests`
has no Npgsql or SqlClient reference at all and hit the same wall. Running with
`--verbosity debug` to a file rather than through `| tail` — which had swallowed every line until
exit — showed the real one:

```
[ERR] It looks like the test coverage capture failed. Disable coverage based optimisation.
```

Stryker defaults to the **vstest** runner. These are xunit.v3 3.2.2 projects with
`OutputType=Exe`, which run under Microsoft.Testing.Platform. Coverage capture failed silently, and
with the optimisation off every mutant re-ran the entire suite:

| Measurement | Value |
|---|---:|
| Mutants created for `**/Expressions/**` | 6,698 |
| Per-mutant test run | ~23 s |
| Full Fluent suite, serialized | 22 s |
| Configured concurrency / logical cores | 4 / 16 |
| Extrapolated wall-clock | **≈ 10.7 h** |

Per-mutant cost equalling full-suite cost is the signature: the selection was saving nothing.
`--test-runner mtp` restores it (`Starting coverage capture for MTP runner`). Scoped to one
visitor, the same work is **98 mutants in under 4 minutes**. Concurrency also raised 4 → 12.

Two contributing factors are real but secondary, and both were fixed rather than worked around:
`xunit.runner.json` serialized every test in both projects, and 29 % of mutants (1,933 of 6,698)
are discarded as `CompileError` — Stryker retried compilation ten times with rollbacks on
`JoinedGroupByExpressionVisitor.cs` before Safe Mode discarded every mutant in
`ExtractGroupByColumns` and `TranslateMemberInit` (`CS0165`, unassigned local `assignment`). Those
two methods get no mutation coverage regardless of runner.

#### Mutation runs target `Jaunty.UnitTests`, not `Jaunty.Tests`

Measured while a `Jaunty.Tests` mutation run was in flight: a concurrent ordinary test run failed
**45 tests**, all on `dialect: SqlServer` — `There is already an object named 'bulk_test'`, plus
row-count assertions off by one. Re-run with nothing else executing, the same suite was **0
failed**. Stryker's 12 parallel test hosts were racing each other on the one shared SQL Server.

That makes a mutation run against `Jaunty.Tests` not merely slow but **invalid**: mutants get
recorded as killed by database collisions rather than by an assertion, so the score measures
contention. `stryker-config.json` therefore lives in `tests/Jaunty.UnitTests`, which reaches no
live engine and where concurrency 12 is safe.

<!-- BASELINE TABLE: filled from the first completed run -->

| Module | Host | Scope | Tested | Killed | Survived | No coverage | Timeout | Score |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `Jaunty` | `Jaunty.UnitTests` | `Internals/Parameters/**` + `Dialects/**` | 2,287 | 2,273 | **0** | 233 | 14 | **90.75 %** |
| `Jaunty.Fluent` | `Jaunty.Fluent.Tests` | `Expressions/ExistsExpressionVisitor.cs` | 98 | 59 | 1 | — | 38 | 66.90 % |

The core run took 75 minutes (07:56 → 09:11) at concurrency 12; 212 further mutants were discarded
as `CompileError`.

**The headline is the zero.** Not one mutant that a test actually executed survived, in any file.
The 9.25 % shortfall is entirely `NoCoverage` — mutations in code no test reaches at all. So the
deficit here is reach, not oracle strength, which inverts the usual reading of a mutation score and
inverts jauntyq's lesson quoted in Phase 1 item 6: there are no surviving mutants to turn into
missing assertions, because there are none.

| File | Killed | No coverage | Score |
|---|---:|---:|---:|
| `Internals/Parameters/ParameterBinder.cs` | 389 | **165** | 70.22 % |
| `Dialects/SqlDialectFactory.cs` | 51 | **30** | 62.96 % |
| `Dialects/PostgreSqlDialect.cs` | 285 | 13 | 95.64 % |
| `Dialects/SQLiteDialect.cs` | 265 | 10 | 96.36 % |
| `Dialects/MySqlDialect.cs` | 397 | 8 | 98.02 % |
| `Dialects/SqlServerDialect.cs` | 532 | 7 | 98.70 % |
| `Internals/Parameters/SqlParameterParser.cs` | 312 | 0 | **100 %** |

Two files hold 195 of the 233 unreached mutants and are where Phase 1's remaining effort belongs:
`ParameterBinder` and `SqlDialectFactory`.

`SqlParameterParser` scoring **100 % with zero unreached mutants** is the rank-1 target from the
coverage cross-reference, and it is the one file in this run that received new tests this session.
Its 14 timeouts are loop mutations that fail to terminate, which Stryker counts as killed.

The Fluent row is a narrower, weaker result and should not be read beside the core one: 38 of 98
timeouts is high enough that some are likely the suite's own slowness under a mutated build rather
than genuine hangs, and its single survivor has not been triaged.

## Phase 1 — improve existing tests in place

### 1. `SqlParameterParser` property + differential tests — **done**

`tests/Jaunty.Tests/Unit/Read/SqlParameterParserPropertyTests.cs`, 7 facts, CsCheck 4.8.0,
20,000 iterations each. The 43 curated cases in `SqlParameterParserTests` are untouched.

The generator composes statements from a 50-fragment vocabulary chosen for the branches the
parser actually has — every quoting style (`'` `"` `[` `` ` ``), doubled and backslash escapes,
unterminated literals, `--` and `/* */` comments, `$$`/`$tag$` dollar-quoting, `@@` system
variables, sigils inside identifiers, and the U+0085/U+2028/U+2029 line separators — plus a
second arbitrary-text generator for shapes the vocabulary cannot compose.

| Fact | Asserts |
|---|---|
| `ExtractionIsTotalOverGeneratedSql` / `…OverArbitraryText` | never throws, either escape mode |
| `EveryExtractedNameIsAValidParameterName` | every returned name is non-empty and all `IsParameterChar` |
| `ExtractionIsDeterministic` | same input, same result |
| **`BothImplementationsAgree`** / `…OnArbitraryText` | span and classic return identical sets |
| `SigilOverloadsAgree` | the string and span overloads of `IsSigilInsideIdentifier` do not diverge |

`BothImplementationsAgree` reaches the private classic method by reflection, so it runs on the
net10.0 leg CI already has. **That puts the netstandard2.0 parser under CI for the first time**
without adding a net472 job.

RED-phase checked, both directions:

| Perturbation | Result |
|---|---|
| classic stops skipping backtick-quoted identifiers | 1 of 7 fail — the differential |
| span over-reads parameter names by one char | 3 of 7 fail |

`src/` restored from backup and confirmed clean by `git status` after each. Parser suite after:
43 → **50** on net10.0 (57 including the cache-split tests), net472 leg unaffected at 50 —
the file is `#if CSCHECK`-guarded because CsCheck ships no .NET Framework target.

### 3. Streaming lifecycle — **done**

`tests/Jaunty.Tests/Integration/Streaming/StreamingLifecycleTests.cs`, 8 theories over the
five dialects (two SQLite-only), joining the `Get Operations` collection because it reuses
that fixture's `get_test` table.

The gap was not "streaming is untested" — `Integration/Streaming/` already held 35 methods
covering empty results, early break, extra/missing columns and option plumbing. The gap was
**cancellation had no oracle**. Both existing cancellation tests pass a token that is never
cancelled:

| existing test | what it actually asserts |
|---|---|
| `QueryStreamAsync_WithCancellationToken_Works` | the overload accepts a token; `CancellationTokenSource(10s)` never fires |
| `GetAllStreamAsync_WithCancellation_ThrowsOrCompletes` | same, and the name's `Throws` branch is unreachable |

Added, against `GetAllStreamAsync`: pre-cancelled token throws before yielding any row;
cancel-after-first-row stops with `OperationCanceledException` having yielded exactly 1;
cancel-mid-iteration and break-mid-iteration both leave the connection `Open` and able to
serve a follow-up `COUNT(*)` (an undisposed reader is what this would catch); double
`DisposeAsync` is idempotent; `MoveNextAsync` after dispose returns false; 20,000 rows yield
in ascending id order; breaking at row 10 of 20,000 still releases the reader. The 20k rows
are generated server-side with a recursive CTE rather than 20,000 round trips.

**RED-phase check — the first perturbation was the wrong one.** Deleting
`cancellationToken.ThrowIfCancellationRequested()` from `GetAllStreamCoreAsync` (GetAllCore.cs:344)
changed nothing: 16 passed before, 16 after. On both SQLite providers the cancellation is
caught one line later by `reader.ReadAsync(cancellationToken)`, so that guard is
defence-in-depth for providers whose `ReadAsync` ignores its token — not the line under test.
Perturbing the honest question instead — every `cancellationToken` inside
`GetAllStreamCoreAsync` replaced with `CancellationToken.None`, i.e. the token ignored
outright — failed all 6 cancellation variants (16 passed → 10). Non-vacuous. `src/` restored
from `tmp/GetAllCore.cs.bak` and confirmed clean by `git diff src/`.

Measured: 34 tests, 16 passed, 12 skipped (Postgres and MariaDB containers not running).
The 6 SqlServer variants are unverified — the local `MSSQLSERVER` service is stopped, and
these failed with `provider: Named Pipes Provider, error: 40`, a connection error rather than
an assertion. They need a re-run once the service is up.

### 5. Trim analyzer — **done, nothing changed**

As predicted from `src/Directory.Build.props:11-12`, measured via
`dotnet msbuild <proj> -getProperty:<prop> -p:TargetFramework=net10.0`:

| project | EnableTrimAnalyzer | EnableAotAnalyzer |
|---|---|---|
| Jaunty | true | true |
| Jaunty.Extensions.Reflection | true | true |
| Jaunty.Fluent | true | true |
| Jaunty.FlatFiles | true | true |
| Jaunty.FlatFiles.DuckDB | true | true |
| Jaunty.Scaffolding | true | true |
| Jaunty.Scaffolding.Cli | true | true |
| Jaunty.SourceGenerator | *unset* | true |

`EnableSingleFileAnalyzer`, `IsTrimmable` and `IsAotCompatible` are likewise true on
`src/Jaunty`. `Jaunty.SourceGenerator` is a netstandard2.0 Roslyn analyzer that is never
trimmed, so unset is correct there. The plan said verify and add nothing; nothing added.

### 2, 4, 6

Dialect boundary theories, allocation budgets and the Stryker re-run: pending.

## Phase 2 — new additive capabilities

SharpFuzz harness over `ExtractParameterNames` and a nightly workflow: pending. Unlike jauntyq —
where the harness was written but never executed because libFuzzer is Linux-only and the machine
is Windows — this repo has a self-hosted Linux runner (`vars.CI_RUNNER`), so the fuzz job can
actually run.

## Verification protocol

- Every new test RED-phase-checked against a deliberately broken target before it is trusted.
- Full suite green at each phase end: `dotnet test Jaunty.slnx -c Release -f net10.0`.
- Databases returned to baseline after every run: `scripts/reset-test-databases.ps1 -e`.
- Stryker killed-mutant delta per module is this plan's own success metric.
- No `src/` changes are planned; if `git status src/` is ever dirty at a phase end, investigate.
