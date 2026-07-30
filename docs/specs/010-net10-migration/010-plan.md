# 010 — Plan

Spec: ./010-spec.md · Created: 2026-07-29 · Revised: 2026-07-30 after adversarial review
· Status: ready for `/tasks`

> **Revision note — two adversarial passes.** The 2026-07-29 first draft failed six claims: the
> centralised pin mechanism **does not work** (measured), two "required" AC amendments were wrong,
> and two "measured" numbers were not produced by the harness they were attributed to.
>
> The 2026-07-30 rewrite was reviewed again and failed four more. The worst was self-inflicted:
> its proposed loader assertion was **vacuous** — green in exactly the failure case it existed to
> catch. That pass also surfaced the largest omission in *both* drafts: **five net8.0-conditioned
> `PropertyGroup`s** that no version of this plan had scheduled, one of which gates the
> `IAsyncEnumerable` API. The `LoggingInterceptor` decision was reversed on the same pass.
>
> Everything below that says *measured* names the command that produced it. Two counts that were
> quoted with confidence and were wrong (pins, single-TFM projects) now name their sweep.

## Approach

Three increments, **code fixes before the retarget**. The spec's open questions were closed by
measurement (§6a), and those measurements decide the shape: the net10 build is not blocked by the
retarget itself — restore is clean and the AOT binary is 6% smaller — it is blocked by four
pre-existing ILLink diagnostics that only .NET 10's stricter analyzer surfaces on a clean build.
PR1 makes a clean net10 build green **while the repo is still net8-only**, PR2 flips the targets,
PR3 wires CI. At no point is `dev` left red.

**On probe greenness, stated honestly:** the probe was *not* uniformly green. It ran 185/185
FlatFiles and 582/583 DuckDB, but also 1 `Jaunty.Tests` failure and 4 `Jaunty.Fluent` failures
(010-spec.md:76-83). The Fluent four were fixed on `fix/csharp14-span-contains`; the one is the
`TypedKeyGuard` control this plan rewrites; the DuckDB one is a pre-existing load-sensitive flake.
So the residue is accounted for, but the first draft's "185/185 and 582/583 pass" was a selective
quotation and is corrected here.

**The retarget should be one commit — as a judgement, not a forced move.** The first draft argued
atomicity was compelled by `SkipGetTargetFrameworkProperties="true"` making partial states silently
wrong. Two problems with that argument. First, the premise is not universal: 4 of the 31 pins carry
no `Skip` attribute, though **only one of those four is a `net8.0` pin**
(`tests/Jaunty.Scaffolding.Tests/Jaunty.Scaffolding.Tests.csproj:46`) — the other three are
`netstandard2.0` pins and irrelevant to a net8→net10 partial state. The premise fails by one
migration-relevant pin, not four; an earlier revision of this paragraph inflated that. Second, this plan's
own risk mitigation — a verification step asserting each net10 test assembly loaded the net10
`Jaunty.dll` — closes the silent window, and once it is closed a per-area retarget is safe too.

The asymmetry is nonetheless real (forget a pin → the net8 assembly loads happily on net10;
set one too early → a loud restore failure), and one commit means one verification instead of
several. That is the reason to prefer it. **It is a preference, and the loader assertion is what
actually makes either choice safe.**

Alternatives rejected: *replace net8.0* (settled in spec Q1 — strands three net8-only projects
behind `NU1201` and drops `IsTrimmable`/`IsAotCompatible` for net8 consumers);
*`TreatWarningsAsErrors=false` for the AOT publish* (discards first-party signal along with
third-party noise — see PR3).

## Tech / dependencies

Verified on this machine, not assumed:

| Thing | Version |
|---|---|
| .NET SDK | 10.0.301 |
| net10 runtime | 10.0.7 |
| net8 runtime (control) | 8.0.26 |
| NativeAOT ILCompiler | 10.0.7 |
| DuckDB.NET.Data.Full | 1.3.0 — native binaries confirmed loading on net10 |

No new package references. `LangVersion` stays **13.0**; this migration changes the target
framework, not the language version.

## Changes by area

### PR1 — green clean net10 build, still net8-only

Baseline, measured 2026-07-30 with
`dotnet build src/Jaunty/Jaunty.csproj -f net10.0 --no-incremental -p:TreatWarningsAsErrors=false`
in the net10 worktree — exactly four, and note the codes, which the first draft got wrong:

```
LoggingInterceptor.cs(174)  IL2111    <- method group, NOT IL2070
ParameterBinder.cs(81)      IL2072
ParameterBinder.cs(920)     IL2072
ParameterCache.cs(40)       IL2111    <- method group, NOT IL2072
```

| Path | Change | Req |
|---|---|---|
| `src/Jaunty/Internals/Parameters/ParameterCache.cs:40` | `Cache.GetOrAdd(type, _ => BuildMetadata(type))` — close over the DAM-annotated `type` from the enclosing signature rather than passing the `BuildMetadata` method group. **Measured: 4 warnings → 3, nothing new.** Add a `TryGetValue` fast path so the closure is not allocated per call. No suppression. | §3.3 |
| `src/Jaunty/Internals/Parameters/ParameterBinder.cs:81`, `:920` | `#pragma warning disable IL2072` naming spec 009. Unannotatable — `object.GetType()` cannot carry DAM. | §3.3 |
| `src/Jaunty/Interceptors/LoggingInterceptor.cs:196-200` | **Remove the `[DynamicallyAccessedMembers]` annotation** from `GetPublicProperties`' parameter. **Measured 2026-07-30: clears exactly the one `IL2111`, 3 diagnostics → 2** in the probe worktree with the `ParameterCache` closure fix already in place; 4 → 2 for the two fixes together. An earlier draft credited the whole 4 → 2 to this change alone, which was wrong. Note also that suppressing `IL2111` instead would clear the same one diagnostic — **the count is not what separates the two options; the AC2 amendment is.** No pragma, no AC2 amendment. | §3.3 |
| `src/Jaunty/Interceptors/LoggingInterceptor.cs:194` | **Rewrite the suppression justification**, which `010-spec.md:67` records as false. Use the honest template already at `ParameterCache.cs:49` — *suppressed pending a source-generated binding path; POCOs must be otherwise rooted*. This is the half that makes the annotation removal safe rather than merely quiet. | §3.3 |
| `src/Jaunty/Interceptors/LoggingInterceptor.cs:202` | Delete the `// AOT-SAFE:` marker along with the annotation it describes. | §3.3 |
| `tests/Jaunty.Tests/Unit/Read/TypedKeyGuardTests.cs:79-83` | Rewrite the dead control — see Data/interfaces. | §3.4 |
| `docs/specs/009-aot-annotation-pass/009-spec.md:141-144` | Scope amendment — see below. | §3.3 |

**Decision, taken 2026-07-30 in the user's absence, then reversed the same day after review.**

The IL2111 at `:174` exists only because `GetPublicProperties`' parameter carries
`[DynamicallyAccessedMembers]` (`:196-200`) and the annotated method is converted to a delegate.
Removing the annotation removes the diagnostic — **measured, 4 warnings → 2** — and the resulting
IL2070 is absorbed by the *pre-existing* suppression at `:193-195`, which is not "a suppression
added for this migration", so AC2 passes unamended.

I first rejected that on the grounds that `010-spec.md:67` records the `:194` justification as
**false for named POCOs**, and routing a real diagnostic into a justification we know is untrue
makes a visible problem invisible. **That reasoning treated the justification text as immutable,
and it is not.** Correcting false suppression justifications in place is this repo's own
precedent — round 26 did exactly that for `MappedCache`/`WriteParameterCache`
(`009-spec.md:31-34, 63-67`), and `ParameterCache.cs:49` already carries the honest template.

So the third option dominates both: **remove the annotation *and* rewrite `:194` honestly, in one
commit.** Then the diagnostic is absorbed by a suppression that is **true**, the deferral to 009
stays greppable in that suppression's text and in the 009 scope amendment below, AC2 needs no
amendment, and `LoggingInterceptor` converges on the same shape
as `ParameterCache` — which this plan already endorses as the correct hand-off to 009. Keeping a
pragma *and* a dead annotation would have been three artifacts where two suffice, and would have
treated one defect class two different ways inside one PR.

The annotation is safe to remove because it is a promise the code cannot keep: `GetPublicProperties`
is private with exactly one call site, `:174`, where the argument is `parameters.GetType()` — a
runtime type no annotation can reach. It therefore never receives a statically-known type, and
trimming behaviour is byte-identical with or without it.

Also rejected: rewriting `:174` as a lambda. **Measured — it trades IL2111 for IL2067 at `:175`**
(`the parameter 't' of method 'lambda expression' does not have matching annotations`).

**009 scope amendment**, replacing the first draft's version, which was wrong about `ParameterCache`:

| File | 009 today | Should be | Why |
|---|---|---|---|
| `LoggingInterceptor` | out of scope | **in scope** | `:174` has no suppression-free fix, and `:194`'s justification is false for named POCOs — 009's defect class exactly, not a separate audit |
| `ParameterBinder` | unmentioned | **in scope** | AC2 defers `:81/:920` *to 009 by name*; 009 does not know it owns them |
| `ParameterCache` | out of scope | **stays out for IL2111, in scope for IL2070** | PR1 fixes the IL2111 at `:40`. The `[UnconditionalSuppressMessage("AOT","IL2070")]` at `:48-50` **survives**, and its own text says *"Suppressed pending a source-generated parameter-binding path"* — that is 009's remit by definition. The first draft said "leaves 009 entirely"; that was wrong. `ParameterBinder:81` is a caller of this same cache: they are one flow and must not be split across specs. |

### PR2 — the retarget

| Path | Change | Req |
|---|---|---|
| `*.csproj` | `net8.0` → `net8.0;net10.0`. **Exact file list to be enumerated in `/tasks`.** `git ls-files '*.csproj' \| xargs grep -l 'net8\.0'` gives **23** — the tracked, worktree-free, gitignore-respecting count, and the number `/tasks` should start from. An earlier revision said 26 by sweeping untracked files as well; the first draft said 23 by coincidence and the spec says 22. Only an enumeration that distinguishes `<TargetFramework>` from `<TargetFrameworks>` from a pin is worth acting on. |
| 21 net8.0 pins across 11 csproj | Duplicate the existing exact-TFM ItemGroup idiom for net10.0 — see Data/interfaces. The 10 `netstandard2.0` pins are untouched. |
| **5 net8.0-conditioned `PropertyGroup`s** | Widen to include net10.0. **This is the largest thing both earlier drafts missed, and it is worse than the pins.** See below. | §3.2 |
| single-TFM projects | **19 tracked csproj carry a singular `<TargetFramework>`** (an earlier revision said 13, which was a `Jaunty.slnx`-scoped count quoted as a repo-wide one). Decide per project whether it multi-targets or moves to net10. Two traps: `src/Jaunty.Scaffolding/Jaunty.Scaffolding.csproj:4` uses the **plural** `<TargetFrameworks>` tag with a single value, so a `<TargetFramework>` sweep misses it; and **`NativeAOT-FluentQuery` and `Jaunty.Fluent.SourceGen.Tests` are not in `Jaunty.slnx` at all** — the solution holds 3 of the 4 AOT samples, so no solution-level build or CI step has ever touched the fourth. AC5 says four. | §3.2 |
| `benchmarks/BENCHMARK-RESULTS.md` | net8 baseline captured on the commit **before** this one. | §3.7 |

### PR3 — CI, release, AOT

| Path | Change | Req |
|---|---|---|
| `src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj` | `<WarningsNotAsErrors>IL2104;IL3053</WarningsNotAsErrors>`, conditioned on net10.0, with a comment naming the six offending assemblies so it can be retired one dependency at a time. **Measured to work.** | §3.6 |
| `src/Jaunty.Scaffolding/Providers/SQLite/SQLiteSchemaReader.cs:65` | **Confirm or clear the IL2057** recorded at `010-spec.md:252-256`. It appeared once and vanished under incremental analysis; it is first-party and `010-spec.md` explicitly forbids sweeping it into `WarningsNotAsErrors`. The first draft dropped this to the very trap its own first risk bullet describes. | §3.3 |
| `.github/workflows/ci.yml:73` | `--no-incremental` on the build step. | §3.1 |
| `.github/workflows/ci.yml:75-121` | Five test steps gain a net10 leg, plus the loader assertion below. Duplicated steps rather than a job matrix — the runner is self-hosted and a matrix re-spins the SQL Server service container per leg. | §3.5 |
| `.github/workflows/ci.yml:182-189` | Keep the net8 publish as control, add `-f net10.0`, keep the size check. | §3.6 |
| `.github/workflows/release.yml:49` | Same net10 leg. | §3.5 |

## Data / interfaces

### The pin mechanism — centralisation was tried and it does not work

The first draft proposed a `JauntyPinnedTfm` property in root `Directory.Build.props`. **Probed on
2026-07-30 with `dotnet msbuild <proj> -getProperty:JauntyPinnedTfm`:**

| Project | Actual TFM | `$(TargetFramework)` as seen in `Directory.Build.props` | `JauntyPinnedTfm` |
|---|---|---|---|
| `samples/NativeAOT-Basic` (single-TFM) | net10.0 | `[]` — **empty** | **net8.0** |
| `src/Jaunty` (multi-TFM inner build) | net10.0 | `[net10.0]` | net10.0 |

Root `Directory.Build.props` is imported **before** the csproj body, so `$(TargetFramework)` is
visible there only when it arrives as a *global* property — i.e. in the inner builds of a
multi-targeting project. For the **13 single-TFM projects** the condition evaluates against an
empty string and every reference pins net8.0 while the project itself builds net10.0. Those 13
include both benchmark projects (AC7's evidence) and all four `NativeAOT-*` samples (AC5's proof):
the mechanism would have silently benchmarked and AOT-published the **net8** build of `Jaunty.dll`.

A second defect in the same proposal: it claimed the pins' enclosing `ItemGroup Condition` widens
from `'$(TargetFramework)' == 'net8.0'`. Only **4** ItemGroups carry such a condition
(`src/Jaunty`, `src/Jaunty.Extensions.Reflection`, `src/Jaunty.FlatFiles`, `tests/Jaunty.Tests`);
the rest are unconditioned.

**Therefore: no centralisation.** Duplicate the existing exact-TFM `ItemGroup` idiom for net10.0,
as at `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj:30-45`:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <ProjectReference Include="..\Jaunty\Jaunty.csproj"
                    SetTargetFramework="TargetFramework=net10.0"
                    SkipGetTargetFrameworkProperties="true" />
</ItemGroup>
```

More lines, and it must be repeated for net11 — but it is greppable, it cannot evaluate to the
wrong thing, and it is the idiom already in the tree. The first draft named indirection as this
mechanism's tradeoff and then shipped the indirection anyway; the probe is what settles it.

### The conditioned PropertyGroups — the biggest omission in both earlier drafts

Five `PropertyGroup`s are conditioned on `'$(TargetFramework)' == 'net8.0'` **exactly**. Adding a
net10.0 target without widening them produces a net10 build that silently loses what they set:

| File:line | What a net10 inner build loses |
|---|---|
| `src/Jaunty/Jaunty.csproj:41-45` | `Nullable`, `IsAotCompatible`, **and `ASYNC_ENUMERABLE_SUPPORT`** — net10 `Jaunty.dll` would ship **without the `IAsyncEnumerable` streaming API** and unmarked for AOT |
| `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj:24-28` | `Nullable`, `IsAotCompatible`, **`EnableTrimAnalyzer`** — the net10 leg stops running the trim analysis PR1 exists to keep green |
| `src/Jaunty.Fluent/Jaunty.Fluent.csproj:23-25` | same shape |
| `tests/Jaunty.Tests/Jaunty.Tests.csproj:16-22` | `ASYNC_ENUMERABLE_SUPPORT` and the `NoWarn` list on the net10 test leg |
| `tests/Jaunty.Tests/Jaunty.Tests.csproj:40` | `Configuration`-**and**-TFM conditioned; needs the same widening |

**None of these fail loudly.** A missing `DefineConstants` compiles out an API; a missing
`EnableTrimAnalyzer` reports zero trim warnings, which reads as success. AC2 and AC3 would both go
green on a net10 build that has quietly lost its streaming API and its trim analysis. Widen every
one with `$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))` — the idiom
already at `src/Directory.Build.props:11-12` — and verify by asserting
`ASYNC_ENUMERABLE_SUPPORT`-gated API is present in the net10 build.

By contrast the one net8.0-conditioned `ItemGroup` that holds `PackageReference`s
(`src/Jaunty/Jaunty.csproj:36-39`) fails *loudly* on net10 — missing `ILogger`. That asymmetry is
why the PropertyGroups are the dangerous half and the earlier drafts, which enumerated ItemGroups
precisely, missed the half that matters.

**The four missing `SkipGetTargetFrameworkProperties` attributes are out of scope.** An earlier
revision told `/tasks` to add them "so the sentence in Approach becomes true" — editing working
build config to make prose accurate, and contradicting this plan's own "the 10 `netstandard2.0`
pins are untouched", since three of the four are netstandard pins. The Approach sentence is
corrected instead. One-line note in `work/todo.md` if anyone wants them made uniform later.

**Loader assertion (the real safety net).** After the net10 test legs run, assert each test
assembly loaded the net10 build of `Jaunty.dll`.

**Do not use `Assembly.Location` — it is vacuous here.** A referenced `Jaunty.dll` is copied into
the *consuming* project's output directory, so on a net10 test leg its `Location` is
`tests/Jaunty.Tests/bin/…/net10.0/Jaunty.dll` — a path containing `net10.0` — **whether the net8
or the net10 build was copied there**. The folder name comes from the test project's TFM, not the
dependency's, so the check is green in exactly the failure case it exists to catch. It is also
empty in single-file and NativeAOT contexts, so it could not be reused for the AC5 samples.

Use the loaded assembly's own metadata instead:

```csharp
var tfm = typeof(Jaunty.Jaunty).Assembly
    .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!
    .FrameworkName;                                   // ".NETCoreApp,Version=v10.0"
Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
```

This reads what the dependency was *compiled* as, which is the thing a forgotten pin changes.
Without a working assertion a forgotten pin is invisible; with it, both atomic and incremental
retargets are safe — and since this assertion is the stated ground for downgrading atomicity to a
preference, getting it right is load-bearing twice over.

### The replacement boxing control

`TheOldFormStillBoxes` asserted that `ArgumentNullException.ThrowIfNull(int)` allocates. .NET 10
elides that box, so the control is dead.

**The first draft's numbers were wrong and are withdrawn.** It reported net8 = 239,952 bytes
against a 100,000-iteration harness; 239,952 = 24 × 9,998, i.e. a ~10k run, while its Guid row
(3,200,000 = 32 × 100,000) was a 100k run. The table mixed iteration counts across rows. The
qualitative finding — **net10 allocates 0 where net8 allocates a box per call** — is unaffected,
but every figure must be re-measured through `Measure()` itself before it is quoted anywhere.

Two candidate replacements. **`/tasks` should cost both and pick one:**

1. **Escaping box.** Assign a `Guid` into a `static object` sink inside the measured loop; an
   escaping box cannot be stack-allocated, so escape analysis cannot elide it. Caveat raised in
   review and accepted: 99,999 of the 100,000 stores are dead, so a future JIT that elides dead
   static stores leaves the control above zero only by one box. Keep the assertion `> 0`, never a
   magnitude, or it becomes a JIT-version tripwire.
2. **Fence the old control.** `#if !NET10_0_OR_GREATER` around `TheOldFormStillBoxes` with the
   XML-doc note. Near-zero cost, no new measurement to maintain, and AC8 says "passing **or
   deliberately rewritten, with the reason recorded**" — a documented fence satisfies that. The
   cost is that net10 then has no harness-liveness control at all.

Either way the class XML-doc must record that .NET 10 elides the `ThrowIfNull` box, so the
original "KeyGuard beats ThrowIfNull" comparison is true on net8/net472 and moot on net10. That
recording is what satisfies AC8. The three `*_AllocatesNothing` tests stay — they assert a real
`KeyGuard` invariant — but they are **vacuous as a comparison** on net10 and the doc must say so.

## Risks & mitigations

- **A local incremental build reports net10 clean when it is not.** The four ILLink errors appear
  only under `--no-incremental` or after `dotnet clean`. *Mitigation:* `--no-incremental` in CI,
  and no task may claim "builds clean" without naming the command that produced the claim. This
  plan's own first draft lost the `SQLiteSchemaReader` IL2057 to exactly this; PR3 now owns it.
- **Silent net8 fallback if a pin is missed.** *Mitigation:* the loader assertion above. This is
  the mitigation that makes the retarget safe; atomicity is secondary to it.
- **`ExpressionCaching_ImprovesQueryPerformance` is a load-sensitive flake** — passes in isolation
  twice, fails under full-suite load, on both targets. *Mitigation:* out of scope here, but AC3's
  "0 failures" will be intermittently red for reasons unrelated to net10. Track separately.
- **AC5 was already unmeetable before this migration** — `009-spec.md:27-30` measures
  `samples/NativeAOT-Basic` failing at runtime with "No mapper found for type Product" on net8
  today. *Mitigation:* rescope to "publishes on net10 and behaves no worse than the net8
  baseline", with a **concrete** assertion (exit code and expected stdout), or the rescoped
  criterion is itself unverifiable.
- **Security/data exposure:** none. No auth, input handling, data exposure or external calls
  change. The only external surface touched is CI pulling the same SDK it already pulls.
- **`.worktrees/` and `.worktrees/` double-count every `grep -r`.** This produced a wrong
  pin count once already (39 vs the true 21). *Mitigation:* every sweep in `/tasks` excludes both.

## Coverage check

| Spec requirement | Covered by |
|---|---|
| §3.1 run FlatFiles/DuckDB on net10 | **Done** — measured, spec §2/§6a Q4 |
| §3.2 add net10.0 alongside net8.0 incl. pins | PR2 + Data/interfaces |
| §3.3 four ILLink diagnostics, three-way split | PR1, + IL2057 in PR3 |
| §3.4 `TypedKeyGuard` control | PR1 + Data/interfaces (two options, `/tasks` picks) |
| §3.5 CI/release net10 legs | PR3 |
| §3.6 NativeAOT publish | PR3 + `WarningsNotAsErrors` (measured) |
| §3.7 benchmark baseline before, delta after | PR2 (baseline) + PR3 (delta) |
| §3.8 `#if NET10_0_OR_GREATER` adoption | **Not covered — deferred with a trigger.** See below |
| AC1, AC4 | PR2 |
| AC2 | PR1 — **no amendment needed**, given the reversed `LoggingInterceptor` decision |
| AC3 | PR3, subject to the DuckDB flake |
| AC5 | Rescope required — see Risks |
| AC6, AC7 | PR3 |
| AC8 | PR1 |

**Deferred with a trigger:** §3.8 (adopt net10 features behind `#if NET10_0_OR_GREATER`). Nothing
in the .NET 10 library surface is worth a conditional today — spec §4 found no `System.Data`/ADO.NET
changes at all — and every runtime gain in §4 arrives with **no code change**, collected by
targeting net10. Writing conditionals before the §3.7 benchmark delta exists is speculation. Gated
on PR3's results: if the delta shows a hot path a conditional could improve, it becomes its own
spec.

## Acceptance-criteria amendments

The first draft called three of these *required* and said the plan "cannot satisfy the spec as
written". **That was wrong on two counts** and is corrected here.

- **AC2 (a) — NO LONGER REQUIRED.** The first draft demanded the named exemption be extended to
  `LoggingInterceptor.cs:174`. With the annotation removed and the `:194` justification rewritten
  honestly (PR1), no suppression is added for this migration and AC2 stands unamended. **Of the
  three amendments the first draft called mandatory, only (c) survives.**
- **AC2 (b) — clarification, NOT required.** The first draft claimed AC2 and AC6 were "mutually
  unsatisfiable" without stating that de-fatalising third-party `ilc` diagnostics is not a
  suppression. That conflated build with publish: **AC2 governs `dotnet build`**
  (`010-spec.md:282-287`), while IL2104/IL3053 are emitted by `ilc` during AOT *publish*, which
  AC2's build never runs. `<WarningsNotAsErrors>` in the Cli csproj touches AC5/AC6 territory
  only. Saying so in AC2 is useful; calling the spec unsatisfiable without it was not true.
- **AC2 (c) — required.** Add "on a clean (`--no-incremental`) build", or AC2 is passable by
  running the wrong command.
- **AC5 — rescope required**, per Risks, and the rescoped wording needs a concrete assertion.
- **AC3 — note only.** The DuckDB flake will intermittently break "0 failures" on both TFMs.
