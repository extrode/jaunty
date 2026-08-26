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

#### The `Jaunty.Tests` baseline is deferred to the nightly tier — measured, not dropped

The first `tests/Jaunty.Tests` run was **abandoned after 4h20m** (PID 37720, 2,886 CPU-seconds,
960 MB working set, started 01:42, killed 06:07) having produced **no report at all** — the output
directory held nothing but its own `.gitignore`. It was still spawning fresh test processes when
stopped, so it was progressing, not deadlocked. Simply too slow to finish.

The cause is structural, not a misconfiguration:

| Factor | Value |
|---|---|
| Tests in the initial run | 3,306 test attributes → 6,220 cases |
| Live-database integration suites in that run | MariaDB, PostgreSQL, SQL Server, MySQL |
| `TestTfmsInParallel` | `false` (deliberate — shared DB instances contend) |
| Per-mutant cost | a further test run each |

`mutate` narrows which **source** files get mutated; it does nothing to the **test** side. Stryker
4.16 exposes no test filter — `--help` offers `--mutate`, `--since`, `--test-project`, and no
equivalent of VSTest's `--test-case-filter` — so the live-DB integration suites cannot be excluded
from the initial run or from any mutant's run. A mutation run over a suite that talks to four
database engines is a nightly-tier activity by construction.

**Consequence, stated rather than quietly absorbed:** the rank-1 target
(`Internals/Parameters/**`) has **no mutation baseline**, so the Phase-1 success metric for it is
the coverage and perturbation evidence recorded above, not a killed-mutant delta. The run moves to
Phase 2's nightly Stryker matrix, where a multi-hour job is what the tier is for. Nothing about the
scope was reduced — only where it runs.

`Jaunty.Fluent.Tests` does not have this problem: SQLite only (no Npgsql or SqlClient reference),
two TFMs, 1,632 test attributes, and a 10-file mutate scope. That baseline runs interactively and
is the one that decides Phase 2 item 3.

<!-- BASELINE TABLE: filled from the first completed run -->

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

### 2–6

Dialect boundary theories, streaming lifecycle, allocation budgets, trim-analyzer verification
and the Stryker re-run: pending.

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
