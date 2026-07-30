# 010 — Tasks

Spec: ./010-spec.md
Plan: ./010-plan.md

Three PRs, in order. PR1's fixes are verifiable **only against a net10 target**, which `dev` does
not yet have — verify them in `.worktrees/net10-measure` (spec 010's evidence record, already
multi-targeting `netstandard2.0;net10.0`) and land them on `dev` where they are net8-neutral.

**Every build claim must name the command that produced it, and that command must include
`--no-incremental`.** ILLink analysis is skipped on an up-to-date compile: an incremental build
reported 0 diagnostics on net10 where a clean build of the same tree reported 4. An incremental
result is not evidence (`010-plan.md:279-282`).

**Every repo sweep must exclude `.worktrees/` and `.worktrees/`.** `git ls-files` does this
inherently and is the preferred form; a bare `grep -r` double-counts and already produced one wrong
pin count (39 against a true 21).

## Enumeration (resolved 2026-07-30, the figure `/tasks` owed the plan)

`git ls-files '*.csproj'` → **27 tracked**, of which **23 mention `net8.0`**, matching the plan.
They decompose as:

| Group | Count | Detail |
|---|---|---|
| In `Jaunty.slnx`, need `net10.0` | **19** | every solution project except `src/Jaunty.SourceGenerator` |
| In `Jaunty.slnx`, stays put | 1 | `src/Jaunty.SourceGenerator` — `netstandard2.0` only, a Roslyn analyzer must stay there |
| Tracked, **not** in the solution, mention `net8.0` | **4** | `samples/NativeAOT-FluentQuery`, `tests/Jaunty.Fluent.SourceGen.Tests`, `samples/torture-test-sakila-queries/SakilaQueries.csproj`, `tools/native/…/MangleMap.csproj` |
| Tracked, no TFM declared at all | 3 | the torture ports — see T0 |

19 + 4 = 23 . Tag forms: **19** files use singular `<TargetFramework>`, **6** use plural
`<TargetFrameworks>`; `src/Jaunty/Jaunty.csproj:5` also holds a *commented-out* singular tag, so a
naive grep over-counts.

Pins, per file, `SetTargetFramework="TargetFramework=net8.0"` → **21 across 11 files**
(`Jaunty.Benchmarks` 2, `Jaunty.FlatFiles.Benchmarks` 2, `Jaunty.Extensions.Reflection` 1,
`Jaunty.FlatFiles.DuckDB` 3, `Jaunty.FlatFiles` 2, `Jaunty.FlatFiles.DuckDB.Tests` 4,
`Jaunty.FlatFiles.Tests` 1, `Jaunty.Fluent.SourceGen.Tests` 2, `Jaunty.Scaffolding.Tests` 1,
`Jaunty.SourceGenerator.Tests` 1, `Jaunty.Tests` 2). The **10 `netstandard2.0` pins across 8
files** are untouched.

## PR1 — green clean net10 build, still net8-only

- [x] **T1** `LoggingInterceptor` — remove the decorative `[DynamicallyAccessedMembers]`, rewrite
  the false `:194` justification, replace the stale `:202` `AOT-SAFE` marker — files:
  `src/Jaunty/Interceptors/LoggingInterceptor.cs` — covers: §3.3, AC2, AC8-adjacent — done when:
  clean net10 build drops the `IL2111` with no suppression added.
  **DONE 2026-07-30, merged `7529a8f2`.** Measured 3 → 2 in `.worktrees/net10-measure` via
  `dotnet build src/Jaunty/Jaunty.csproj -f net10.0 --no-incremental`. net8 clean build 0/0. Unit
  suite 2913 passed / 0 failed. Perturbation-checked: gutting the method fails exactly 2 tests.
- [x] **T2** 009 scope amendment — files: `docs/specs/009-aot-annotation-pass/009-spec.md` —
  covers: §3.3 — done when: `ParameterBinder:81/:920` and `ParameterCache`'s `IL2070` are in 009's
  scope and the two `IL2111`s are marked closed by 010.
  **DONE 2026-07-30.** T1 also removed `LoggingInterceptor` from 009's residue entirely.
- [x] **T3** AC2 clean-build clause and AC5 rescope — files:
  `docs/specs/010-net10-migration/010-spec.md` — covers: AC2, AC5 — done when: AC5 states three
  mechanical checks (publish exit 0 with no new `ilc` diagnostic; exit code equals net8; stdout
  equals net8 modulo timings and paths).
  **DONE 2026-07-30.** The AC2 *relaxation* the first draft called mandatory was dissolved by T1.
- [x] **T4** `ParameterCache.cs:40` — `Cache.GetOrAdd(type, _ => BuildMetadata(type))`, closing over
  the DAM-annotated `type` rather than passing a method group, plus a `TryGetValue` fast path so the
  closure is not allocated on the hit path — files:
  `src/Jaunty/Internals/Parameters/ParameterCache.cs` — covers: §3.3, AC2 — done when: clean net10
  build shows one fewer `IL2111` and nothing new.
  **DONE UPSTREAM 2026-07-30, `c90e5af8` (spec 011), merged here as `30b28874`.** It went further
  than this task asked and was right to: rather than keep the annotation and close over it, spec 011
  **deleted** the `[DynamicallyAccessedMembers]` from `Get(Type)` — it could never be satisfied,
  since every caller arrives through `parameters.GetType()` on an `object` — and arranged real
  preservation at the consumer's call sites instead (`JauntyAot.PreserveParameters<T>()` emitted by
  the generator, `JAUNTYGEN003` where it cannot see the type). The `TryGetValue` fast path this task
  specified is present at `:52`, so `BuildMetadata` still runs only on a miss.
- [x] **T5** ~~`ParameterBinder` `IL2072` ×2~~ — **DISSOLVED, not done.** There is nothing left to
  suppress. Both diagnostics were caused by `ParameterCache.Get`'s unsatisfiable annotation; deleting
  it in T4 removed the requirement the two call sites were failing, so `ParameterBinder.cs:81` and
  `:918` now compile clean with no `#pragma` and no justification text to maintain. **AC2's "two
  IL2072 deliberately left standing, deferred to spec 009 by name" is void** — nothing stands.
  Amending AC2 is T21's, and no diagnostic is now silenced anywhere in `src/Jaunty`.
- [ ] **T6** `TypedKeyGuard` control — **Option 1 (escaping box)**, chosen over fencing — files:
  `tests/Jaunty.Tests/Unit/Read/TypedKeyGuardTests.cs:74-83` — covers: §3.4, AC8 — done when: a
  control asserts `> 0` on **both** net8 and net10, and the class XML-doc records that .NET 10
  elides the `ThrowIfNull` box so the original comparison is true on net8/net472 and moot on net10.
  - Rationale for Option 1 over the plan's Option 2, which review recommended: fencing
    `TheOldFormStillBoxes` behind `#if !NET10_0_OR_GREATER` leaves net10 with **all three**
    remaining tests asserting `== 0`, so a blind harness returning 0 passes every one. Review
    argued the Guid sibling's 32-byte history covers this; it does not — that figure is in a
    doc-comment, not an assertion. Option 2 removes the only vacuity guard on the new target.
  - **Assertion must be `> 0`, never a magnitude** — 99,999 of the 100,000 stores into the
    `static object` sink are dead, so a future JIT that elides dead static stores would leave the
    control above zero by a single box. A magnitude assertion becomes a JIT-version tripwire.
  - **Measurement gate, not an assumption:** if the escaping box measures 0 on net10 through
    `Measure()` itself, escape analysis defeated it — fall back to Option 2 and record why. Do not
    quote any figure not produced by `Measure()`; the plan's first-draft numbers were withdrawn for
    exactly that (239,952 = 24 × 9,998 was a ~10k run reported against a 100k harness).
- [ ] **T7** Verify PR1 whole — files: none — covers: AC2 — done when:
  `dotnet build src/Jaunty/Jaunty.csproj -f net10.0 --no-incremental -nodeReuse:false` in the probe
  worktree reports **0 errors**, and the same clean build on `dev` for net8/net472/netstandard2.0 is
  unchanged. Expected residue after T4+T5: zero.
  **DONE 2026-07-30 for `src/Jaunty`.** `dotnet build src/Jaunty/Jaunty.csproj --no-incremental -c
  Release -f net10.0` in `.worktrees/net10-measure` (rebased onto the merge at `76eb2948`): **0
  warnings, 0 errors** — the residue is zero and no suppression was added for it, which is a better
  outcome than AC2 asked for. The same command for net8 on the main tree: 0/0.
  Proved non-vacuous rather than assumed: commenting out `ParameterCache`'s `IL2070` suppression and
  rebuilding clean produced exactly `2 IL2070`, so ILLink analysis is running on the run that
  reported zero. File restored via `git checkout --`; probe tree clean.
  **Still open for T14:** this covers `src/Jaunty` only. The other eight `src/` projects have not
  been net10-measured, and three of them (`Jaunty.Fluent`, both `FlatFiles`) gained reflection this
  merge.

## PR2 — the retarget

- [ ] **T8** Capture the net8 benchmark baseline **before any retarget commit** — files:
  `benchmarks/BENCHMARK-RESULTS.md` — covers: §3.7, AC7 — done when: a full net8 run is recorded
  with its commit SHA. **Ordering is load-bearing: once T10 lands there is no net8-only tree to
  measure.** Blocks T20.
- [ ] **T9** Widen the **5 net8.0-conditioned `PropertyGroup`s** — files:
  `src/Jaunty/Jaunty.csproj:41-45`, `src/Jaunty.FlatFiles/…:24-28`, `src/Jaunty.Fluent/…:23-25`,
  `tests/Jaunty.Tests/…:16-22`, `tests/Jaunty.Tests/…:40` — covers: §3.2, AC2, AC3 — done when:
  each condition uses
  `$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))` (the idiom already at
  `src/Directory.Build.props:11-12`).
  **This is the most dangerous task in the spec and the one both plan drafts missed.** None of
  these fail loudly: a lost `DefineConstants` compiles `ASYNC_ENUMERABLE_SUPPORT` out, so net10
  `Jaunty.dll` would ship **without the `IAsyncEnumerable` streaming API**; a lost
  `EnableTrimAnalyzer` reports zero trim warnings, which reads as success and silently voids PR1.
  `tests/Jaunty.Tests/…:40` is conditioned on `Configuration` **and** TFM — widen both halves.
  Done when additionally: a test asserts an `ASYNC_ENUMERABLE_SUPPORT`-gated member is present in
  the net10 build, and `IsAotCompatible`/`EnableTrimAnalyzer` are confirmed set on the net10 inner
  build via `dotnet msbuild -getProperty:`.
- [ ] **T10** Add `net10.0` to the **19** solution projects — files: the 19 enumerated above —
  covers: §3.2, AC1, AC4 — done when: every one targets `net8.0;net10.0` (or `netstandard2.0;net8.0;net10.0`),
  no existing target is dropped, and `src/Jaunty.SourceGenerator` is untouched. Traps: `<TargetFramework>`
  → `<TargetFrameworks>` requires the **tag rename**, not just a value edit;
  `src/Jaunty.Scaffolding/Jaunty.Scaffolding.csproj:4` already uses the **plural** tag with a single
  value so a singular-tag sweep misses it; `src/Jaunty/Jaunty.csproj:5`'s commented-out tag must
  stay commented.
- [ ] **T11** Duplicate the **21 net8.0 pins** for net10.0 across the **11** files enumerated above
  — covers: §3.2, AC1 — done when: each net8-pinned `ProjectReference` has a net10 sibling in an
  `ItemGroup Condition="'$(TargetFramework)' == 'net10.0'"`, per the existing idiom at
  `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj:30-45`.
  **No centralisation.** A `JauntyPinnedTfm` property in root `Directory.Build.props` was designed,
  probed and **falsified**: root props import before the csproj body, so `$(TargetFramework)` is
  visible there only as a *global* property, i.e. only in multi-targeting inner builds.
  `samples/NativeAOT-Basic` at net10.0 saw `[]` and resolved `net8.0` — it would have silently
  AOT-published the net8 build. The 10 `netstandard2.0` pins stay as they are; the 4 missing
  `SkipGetTargetFrameworkProperties` attributes are **out of scope** (3 of the 4 are netstandard
  pins) — one line in `work/todo.md` instead.
- [ ] **T12** Decide the **4 tracked non-solution projects** — files: `samples/NativeAOT-FluentQuery`,
  `tests/Jaunty.Fluent.SourceGen.Tests`, `samples/torture-test-sakila-queries/SakilaQueries.csproj`,
  `tools/native/…/MangleMap.csproj` — covers: §3.2, AC5 — done when: each is retargeted or
  explicitly recorded as staying on net8.0 with a reason.
  **`NativeAOT-FluentQuery` is not in `Jaunty.slnx`**, so no solution build and no CI step has ever
  touched it — yet **AC5 says four samples**. Either add it to the solution or give AC5 an explicit
  route to it; leaving it out makes AC5 unverifiable for a quarter of its subject.
- [ ] **T13** Loader assertion — files: new test under `tests/Jaunty.Tests/` — covers: §3.2, AC1 —
  done when: each net10 test leg asserts the `Jaunty.dll` it loaded was *compiled* as net10:
  ```csharp
  var tfm = typeof(Jaunty.Jaunty).Assembly
      .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!
      .FrameworkName;                                   // ".NETCoreApp,Version=v10.0"
  Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
  ```
  **Do not use `Assembly.Location`.** A referenced `Jaunty.dll` is copied into the *consuming*
  project's output directory, so on a net10 test leg the path contains `net10.0` whether the net8 or
  net10 build was copied — green in exactly the failure case it exists to catch. It is also empty
  under single-file and NativeAOT, so it could not serve AC5's samples.
  This assertion is **load-bearing twice**: it is also the stated ground for treating atomicity as a
  preference rather than a requirement. Verify it fails when a pin is deliberately reverted.
- [ ] **T14** Full clean solution build and both suites on every target — covers: AC1, AC3, AC4 —
  done when: `dotnet build Jaunty.slnx --no-incremental` is 0 errors and the suites pass on net8.0,
  net10.0, net472 and netstandard2.0 with skip counts no higher than the net8 baseline.
  Note the file is **`Jaunty.slnx`**, not `Jaunty.sln` (`dotnet build Jaunty.sln` → MSB1009).
  `ExpressionCaching_ImprovesQueryPerformance` is a known load-sensitive flake on **both** targets
  and will intermittently redden AC3's "0 failures" for reasons unrelated to net10.

## PR3 — CI, release, AOT

- [ ] **T15** `WarningsNotAsErrors` for the third-party `ilc` diagnostics — files:
  `src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj` — covers: §3.6, AC6 — done when:
  `<WarningsNotAsErrors>IL2104;IL3053</WarningsNotAsErrors>` is conditioned on net10.0 with a
  comment naming the six offending assemblies so it can be retired one dependency at a time.
  De-fatalising a *third-party publish* diagnostic is not an AC2 suppression: AC2 governs
  `dotnet build`, while `IL2104`/`IL3053` come from `ilc` at publish, which AC2's build never runs.
- [ ] **T16** `SQLiteSchemaReader.cs:65` `IL2057` — **confirm or clear on a clean publish** — files:
  `src/Jaunty.Scaffolding/Providers/SQLite/SQLiteSchemaReader.cs` — covers: §3.3, AC2 — done when:
  a clean publish either reproduces it (then fix it properly) or shows it gone (then record that).
  It is **first-party**, so `010-spec.md:252-256` explicitly forbids sweeping it into T15's
  `WarningsNotAsErrors`. It appeared once and vanished under incremental analysis — the plan's own
  first draft lost it to precisely the trap its first risk bullet describes.
- [ ] **T17** `--no-incremental` on the CI build step — files: `.github/workflows/ci.yml:73` —
  covers: §3.1, AC2 — done when: CI cannot report a false-clean net10 build.
- [ ] **T18** net10 legs for the five test suites plus the T13 assertion — files:
  `.github/workflows/ci.yml:75-121`, `.github/workflows/release.yml:49` — covers: §3.5, AC3, AC6 —
  done when: all five suites run on both targets and are green. **Duplicated steps, not a job
  matrix** — the runner is self-hosted and a matrix re-spins the SQL Server service container per
  leg. Two live CI facts to expect: pushes currently queue **two runs per SHA** (a known open
  issue), and the runner's Docker is a WSL2-backed Docker Desktop that must be up for the service
  container.
- [ ] **T19** AOT publish leg — files: `.github/workflows/ci.yml:182-189` — covers: §3.6, AC5, AC6 —
  done when: the net8 publish is **kept as the control**, `-f net10.0` is added, the size check
  survives, and each sample satisfies the three rescoped AC5 checks (publish exit 0 with no new
  `ilc` diagnostic; exit code equals net8; stdout equals net8 modulo timings and paths).
  **A sample that fails identically on both targets passes AC5** — `NativeAOT-Basic` already fails
  at runtime on net8 today with `No mapper found for type Product` (`009-spec.md:27-30`), which is
  009's defect, not this migration's. net8-works/net10-fails is the regression to catch.
- [ ] **T20** Benchmark delta — files: `benchmarks/BENCHMARK-RESULTS.md` — covers: §3.7, AC7 — done
  when: net10 is measured against T8's baseline and the delta recorded. Depends on T8.
- [ ] **T21** Set `Status: done` — files: `docs/specs/010-net10-migration/010-spec.md` — covers: all
  — done when: every AC is met or explicitly amended, with the command that produced each claim.

## Out of scope, recorded so it is not re-derived

- **T0 — the 3 torture-port csproj.** `Conduit`, `Infrastructure` and `FunctionalTests` declare
  **no TFM anywhere**, are absent from the only solution file, and inherit nothing (neither
  `Directory.Build.props` sets `TargetFramework`). They cannot build as they stand, so there is
  nothing to retarget. Note in `work/todo.md`; do not touch here.
- **§3.8 — adopting net10 features behind `#if NET10_0_OR_GREATER`.** Deferred with a trigger:
  spec §4 found no `System.Data`/ADO.NET changes, and every runtime gain arrives with no code
  change from targeting net10. If T20's delta exposes a hot path a conditional could improve, it
  becomes its own spec.
- **The 4 missing `SkipGetTargetFrameworkProperties` attributes.** An earlier revision wanted them
  added "so the sentence in Approach becomes true" — editing working build config to make prose
  accurate. The prose was corrected instead.

## Coverage check

| Spec requirement | Tasks |
|---|---|
| §3.1 FlatFiles/DuckDB on net10 | already measured at spec time; T17 keeps CI honest |
| §3.2 add net10.0 alongside net8.0, incl. pins | T9, T10, T11, T12, T13 |
| §3.3 the four ILLink diagnostics | T1 , T4 (upstream), T5 dissolved, T7 measured 0; T16 for the IL2057 |
| §3.4 `TypedKeyGuard` control | T6 |
| §3.5 CI/release net10 legs | T18 |
| §3.6 NativeAOT publish | T15, T19 |
| §3.7 benchmark baseline then delta | T8 → T20 |
| §3.8 net10 feature adoption | deferred with a trigger (above) |
| AC1 | T10, T11, T13, T14 |
| AC2 | T1 , T4 , T7 , T16, T17 — **unrelaxed and now unsilenced**: T5 dissolved, so AC2's clause deferring two IL2072 to spec 009 is void and T21 must strike it |
| AC3 | T14, T18 (subject to the known flake) |
| AC4 | T10, T14 |
| AC5 | T12, T19 — as rescoped by T3 |
| AC6 | T15, T18, T19 |
| AC7 | T8, T20 |
| AC8 | T6 |

**No orphans:** every task traces to a plan section; every requirement and AC has ≥1 task.

**One gap, named rather than papered over:** AC5 says four `NativeAOT-*` samples, but only three
are in `Jaunty.slnx`. T12 must resolve `NativeAOT-FluentQuery` or AC5 cannot be verified for it.
