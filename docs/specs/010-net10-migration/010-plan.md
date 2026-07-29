# 010 — Plan

Spec: ./010-spec.md · Created: 2026-07-29 · Status: ready for `/tasks`

## Approach

Three increments, **code fixes before the retarget**. The spec's open questions were closed by
measurement rather than assumption (§6a), and those measurements decide the shape: the net10 build
is not blocked by the retarget itself — restore is clean, 185/185 and 582/583 pass, and the AOT
binary is 6% smaller — it is blocked by four pre-existing ILLink diagnostics that only .NET 10's
stricter analyzer surfaces. So PR1 makes a clean net10 build green **while the repo is still
net8-only**, PR2 flips the targets in one atomic commit, PR3 wires CI. At no point is the repo
left in a state where `dev` is red.

The retarget is deliberately **atomic rather than incremental**, which is the one place this plan
chooses the bigger commit. Partial retargets are not merely inconvenient, they are silently
wrong: `SkipGetTargetFrameworkProperties="true"` on all 21 pins bypasses TFM negotiation, so a
project building `net10.0` while its `ProjectReference` still pins `net8.0` compiles and runs
happily against the net8 assembly. Forgetting a pin passes; setting one too early fails loudly.
Given that asymmetry, a partially-retargeted repo produces green net10 test legs that are
actually exercising the net8 build of the library — a false AC3. One commit, one verification.

Alternatives rejected: *replace net8.0* (settled in spec Q1 — strands the three net8-only projects
behind `NU1201` and drops `IsTrimmable`/`IsAotCompatible` for net8 consumers); *bottom-up per-project
retarget* (23 PRs of identical mechanical change, each leaving the silent-drift window open);
*`TreatWarningsAsErrors=false` for the AOT publish* (discards first-party signal along with
third-party noise — see PR3.1).

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
framework, not the language version — they are independent and deliberately so.

## Changes by area

### PR1 — green clean net10 build, still net8-only

| Path | Change | Req |
|---|---|---|
| `src/Jaunty/Internals/Parameters/ParameterCache.cs:40` | Close over the already-DAM-annotated `Type` from `:36-38` instead of passing the `BuildMetadata` method group to `GetOrAdd`; `TryGetValue` fast path so the closure does not allocate per call. No suppression. | §3.3 |
| `src/Jaunty/Internals/Parameters/ParameterBinder.cs:81`, `:920` | `#pragma warning disable IL2072` naming spec 009. Unannotatable — `object.GetType()` cannot carry DAM. | §3.3 |
| `src/Jaunty/Interceptors/LoggingInterceptor.cs:174` | Same named-pragma deferral. A lambda only trades IL2111 for IL2067. | §3.3 |
| `tests/Jaunty.Tests/Unit/Read/TypedKeyGuardTests.cs:79-83` | Replace the dead control (see Data/interfaces). | §3.4 |
| `docs/specs/009-aot-annotation-pass/009-spec.md:141-144` | Scope amendment: strike `LoggingInterceptor` from out-of-scope, add `ParameterBinder`; `ParameterCache` leaves 009 entirely (fixed here). | §3.3 |

### PR2 — the retarget, one commit

| Path | Change | Req |
|---|---|---|
| 23 `*.csproj` | `net8.0` → `net8.0;net10.0` in `<TargetFrameworks>`. The four core packages become `netstandard2.0;net8.0;net10.0`; `Jaunty.FlatFiles.DuckDB`, `Jaunty.Scaffolding`, `Jaunty.Scaffolding.Cli` become `net8.0;net10.0`. | §3.2 |
| 21 pins across 11 csproj | See Data/interfaces — centralised, not duplicated. | §3.2 |
| `benchmarks/BENCHMARK-RESULTS.md` | net8 baseline captured on the commit **before** this one. | §3.7 |

### PR3 — CI, release, AOT

| Path | Change | Req |
|---|---|---|
| `src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj` | `<WarningsNotAsErrors>IL2104;IL3053</WarningsNotAsErrors>`, conditioned on net10.0, with a comment naming the six offending assemblies so it can be retired one dependency at a time. **Measured to work.** | §3.6 |
| `.github/workflows/ci.yml:73` | `--no-incremental` on the build step. | §3.1 |
| `.github/workflows/ci.yml:75-121` | Five test steps gain a net10 leg. Duplicated steps rather than a job matrix — the runner is self-hosted and a matrix re-spins the SQL Server service container per leg. | §3.5 |
| `.github/workflows/ci.yml:182-189` | Keep the net8 publish as control, add `-f net10.0` plus the existing size check. | §3.6 |
| `.github/workflows/release.yml:49` | Same net10 leg. | §3.5 |

## Data / interfaces

**The pin mechanism.** In root `Directory.Build.props`:

```xml
<PropertyGroup>
  <JauntyPinnedTfm>net8.0</JauntyPinnedTfm>
  <JauntyPinnedTfm Condition="'$(TargetFramework)' == 'net10.0'">net10.0</JauntyPinnedTfm>
</PropertyGroup>
```

The 21 modern pins become `SetTargetFramework="TargetFramework=$(JauntyPinnedTfm)"`, and their
enclosing `ItemGroup Condition` widens from `'$(TargetFramework)' == 'net8.0'` to
`$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))` — the idiom already in
use at `src/Directory.Build.props:11-12`. The 10 `netstandard2.0` pins are untouched.

This makes "referenced TFM equals consuming TFM" structural instead of 21 hand-maintained
pairings, and the next target is a one-line change. **Tradeoff, stated plainly:** a csproj no
longer says which TFM it references, so the failure mode moves from a greppable literal to a
property evaluation you have to trust. Exact-TFM groups stay wherever the group also carries
TFM-specific `PackageReference` versions (e.g. `tests/Jaunty.Tests/Jaunty.Tests.csproj:87-88`,
`Microsoft.Extensions.*` 8.0.1) — those split on their own merits, and whether net10 takes 10.x
versions of those packages is a task-level decision.

**The replacement boxing control.** `TheOldFormStillBoxes` asserted that
`ArgumentNullException.ThrowIfNull(int)` allocates. Measured: net8 = 239,952 bytes, **net10 = 0**.
Replace with a box the runtime cannot elide because it escapes — assign a `Guid` into a `static
object` sink inside the measured loop. Measured on both runtimes:

| Control | net8.0 | net10.0 |
|---|---|---|
| `ThrowIfNull(int)` (old) | 239,952 | **0** |
| escaping `Guid` → static (new) | 3,200,000 | **3,200,000** |

`Guid` rather than `int` so future small-value box caching cannot invalidate it. The three
`*_AllocatesNothing` tests stay unchanged — they assert a real `KeyGuard` invariant — but the
class XML-doc must record that .NET 10 elides the `ThrowIfNull` box, so the original
"KeyGuard beats ThrowIfNull" comparison is true on net8/net472 and moot on net10. That recording
is what satisfies AC8.

## Risks & mitigations

- **A local incremental build reports net10 clean when it is not.** The four ILLink errors appear
  only under `--no-incremental` or after `dotnet clean`; the first AOT publish also hid them this
  way. *Mitigation:* `--no-incremental` in CI (PR3), and no task may claim "builds clean" without
  saying which build produced the claim.
- **Silent net8 fallback if a pin is missed** (see Approach). *Mitigation:* atomic PR2, plus a
  verification step asserting each net10 test assembly loaded the net10 build of `Jaunty.dll`.
- **`ExpressionCaching_ImprovesQueryPerformance` is a load-sensitive flake** — passes in isolation
  twice, fails under full-suite load, on both targets. *Mitigation:* out of scope to fix here, but
  AC3's "0 failures" will be intermittently red for reasons unrelated to net10. Track separately.
- **AC5 was already unmeetable before this migration** — 009 measured `samples/NativeAOT-Basic`
  failing at runtime with "No mapper found for type Product" on net8 today. *Mitigation:* rescope
  to "publishes on net10 and behaves no worse than the net8 baseline"; runtime correctness is
  009's.
- **Security/data exposure:** none. No auth, input handling, data exposure or external calls
  change. The only external surface touched is CI pulling the same SDK it already pulls.
- **The `.worktrees/` and `.worktrees/` copies double-count every `grep -r`.** This already
  produced a wrong pin count (39 vs the true 21). *Mitigation:* every sweep in `/tasks` must
  exclude both.

## Coverage check

| Spec requirement | Covered by |
|---|---|
| §3.1 run FlatFiles/DuckDB on net10 | **Done** — measured, spec §2/§6a Q4 |
| §3.2 add net10.0 alongside net8.0 incl. pins | PR2 + Data/interfaces |
| §3.3 four ILLink diagnostics, three-way split | PR1 |
| §3.4 `TypedKeyGuard` control | PR1 + Data/interfaces |
| §3.5 CI/release net10 legs | PR3 |
| §3.6 NativeAOT publish | PR3 + `WarningsNotAsErrors` (measured) |
| §3.7 benchmark baseline before, delta after | PR2 (baseline) + PR3 (delta) |
| §3.8 `#if NET10_0_OR_GREATER` adoption | **Not covered — deliberately deferred.** See below. |
| AC1, AC4 | PR2 |
| AC2 | PR1, with the amendments in §G below |
| AC3 | PR3, subject to the DuckDB flake |
| AC5 | Rescope required — see Risks |
| AC6, AC7 | PR3 |
| AC8 | PR1 |

**Uncovered, flagged rather than silently dropped:** §3.8 (adopt net10 features behind
`#if NET10_0_OR_GREATER`). Nothing in the .NET 10 library surface is worth a conditional today —
spec §4 found no `System.Data`/ADO.NET changes at all — and every runtime gain listed in §4 arrives
with **no code change**, collected simply by targeting net10. Writing conditionals before the
benchmark delta (§3.7) exists would be speculation. This plan therefore treats §3.8 as gated on
PR3's benchmark results: if the delta shows a hot path that a conditional could improve further,
it becomes its own spec. That is a deferral with a trigger, not an omission.

**Acceptance-criteria amendments this plan requires** (§G): AC2 must (a) extend its named
exemption to `LoggingInterceptor.cs:174`, not just the `ParameterBinder` pair, (b) state that
de-fatalising third-party ilc diagnostics is not a "suppression" — otherwise AC2 and AC6 are
mutually unsatisfiable on net10 — and (c) say "on a clean build". Without (a) and (b) this plan
cannot satisfy the spec as written.
