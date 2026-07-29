# .NET 10 Migration — Feature Specification

> spec.md — The "what" and "why". No technical implementation details.

Status: draft · Created: 2026-07-29 · Origin: SDK/CI divergence investigation, 2026-07-29

---

## 1. Problem Statement

Every runtime Jaunty targets leaves support on the same day.

| Target | Released | Support ends |
|---|---|---|
| .NET 10 (LTS) | 2025-11-11 | **2028-11-14** |
| .NET 9 (STS) | 2024-11-12 | 2026-11-10 |
| .NET 8 (LTS) | 2023-11-14 | **2026-11-10** |
| .NET Framework 4.8 | — | indefinite (Windows component) |

Jaunty's shipping targets are `netstandard2.0` and `net8.0`, plus `net472` in `Jaunty.Tests`.
`netstandard2.0` and `net472` are unaffected. **`net8.0` is the exposure**, and it expires in
roughly fifteen weeks. Nothing about the SDK-9-to-SDK-10 change already made in CI addresses
this: that changed the compiler, not what Jaunty ships against.

A second, softer motivation: .NET 10's benefits for a micro-ORM are almost entirely in the
runtime, and they are collected simply by targeting it — see §4.

## 2. Evidence — the feasibility probe

Retargeting is not speculative. On 2026-07-29 every `net8.0` in 22 project files was rewritten to
`net10.0` on the throwaway branch `probe/net10-feasibility`, then restored, built and tested. What
follows is measured, not estimated.

**Restore:** clean, no `NU` errors. **Build with warnings non-fatal:** whole solution, 0 errors.

**Build with the repo's real settings** (`TreatWarningsAsErrors=true` in `src/`): fails. Four
diagnostics, all in `src/Jaunty`, all raised by .NET 10's stricter ILLink analyzer and none of
them reported by net8.0:

| Code | Site |
|---|---|
| IL2111 | `Interceptors/LoggingInterceptor.cs:174` |
| IL2111 | `Internals/Parameters/ParameterCache.cs:40` |
| IL2072 | `Internals/Parameters/ParameterBinder.cs:81` |
| IL2072 | `Internals/Parameters/ParameterBinder.cs:920` |

Every other `src/` project is clean on its own code; they fail only because they rebuild
`src/Jaunty` as a dependency.

**These are not new defects.** `ParameterCache.BuildMetadata` already carries an
`[UnconditionalSuppressMessage]` for IL2070 whose justification states the hazard in full —
parameter POCOs may be trimmed away, and NativeAOT callers "must ensure their parameter POCOs are
otherwise rooted". The .NET 10 analyzer reports the same hazard under diagnostic IDs that
suppression does not cover. This is the identical problem already specified in
[009-aot-annotation-pass](../009-aot-annotation-pass/009-spec.md), reaching a second pair of
caches. **009 should be resolved first, or jointly**; patching four suppressions here would
repeat the mistake 009 exists to correct.

**Tests on `net10.0`**, Release, warnings non-fatal:

| Suite | Result |
|---|---|
| Jaunty.Tests | 2864 passed, **1 failed**, 2437 skipped |
| Jaunty.SourceGenerator.Tests | 72 passed |
| Jaunty.Fluent.Tests | 1189 passed, **4 failed** |
| Jaunty.Scaffolding.Tests | 555 passed, 24 skipped |

The 2437 skips are pre-existing and environmental — no SQL Server, PostgreSQL or MySQL
connection strings on the probe machine. CI supplies SQL Server, so the CI skip count is lower.

**The 4 Fluent failures are not a net10 problem and are already resolved.** Bisected against the
language version on `net8.0` with everything else held fixed: C# 14 fails 4/4, C# 13 passes 4/4,
C# 12 passes 4/4. C# 14's first-class span conversions change which overload
`Enumerable.Contains` binds to; the emitted expression tree changes shape and
`WhereExpressionVisitor.VisitMethodCall` then evaluates a subexpression still referencing the
lambda parameter, throwing `InvalidOperationException: variable 'p' of type Product referenced
from scope '', but it is not defined` from `ExpressionEvaluator.cs:40`. The repo is pinned to
`LangVersion 13.0` for exactly this reason. The probe only surfaced it because net10 implies the
newer compiler.

**The 1 Jaunty.Tests failure is genuinely net10-specific.**
`TypedKeyGuardTests.TheOldFormStillBoxes_WhichIsWhatMakesTheMeasurementMeaningful` passes on
net8.0 under both C# 13 and C# 14, and fails only on net10.0. The test asserts that an old code
path *does* box, as the control for a boxing measurement. If .NET 10 no longer boxes there, the
control is invalid and the test — not the product code — needs rethinking. **Unverified:** the
cause has not yet been confirmed; it is assumed to be a runtime behaviour change.

## 3. Scope

1. Decide the target set (§6, open question) and apply it.
2. Resolve the four ILLink diagnostics — jointly with 009, not by fresh suppression.
3. Resolve the `TypedKeyGuard` control test.
4. Move CI and release workflows to the new framework: six `--framework net8.0` occurrences
   across `ci.yml` (5) and `release.yml` (1), plus the AOT-publish job.
5. Confirm the NativeAOT samples publish and run on the new target.
6. Re-baseline the BenchmarkDotNet suites and record the delta (§4).

## 4. Why this is worth more than a support-date chore

The .NET 10 **library** surface offers Jaunty essentially nothing. Reviewing the whole "what's new
in the libraries" list, the only items within reach are `CompareOptions.NumericOrdering`,
additional `OrderedDictionary<TKey,TValue>` overloads, and stricter `System.Text.Json` options —
none load-bearing, and there are no `System.Data` or ADO.NET changes at all.

The **runtime** surface is the opposite, and every item lands on a hot path Jaunty actually has:

- **Delegate escape analysis and stack allocation.** Parameter binding invokes cached getter
  delegates per property per row (`meta[i].Getter(parameters)`). Delegates that do not escape are
  now stack-allocated.
- **Escape analysis for local struct fields**, which previously forced heap allocation of arrays
  referenced through a non-escaping struct.
- **Stack allocation of small arrays**, of both value and reference types. `ParameterMetadata[]`
  and the SQL parameter-name arrays are exactly this shape.
- **Array interface method devirtualization and enumeration de-abstraction**, removing virtual
  dispatch when arrays are walked through `IEnumerable<T>`.
- **Struct arguments packed directly into shared registers**, no intermediate stack store.
- **NativeAOT type preinitializer** support for all `conv.*` and `neg` opcodes.

None of these require a code change. They are collected by retargeting, which makes the
benchmark re-baseline in §3.6 the main evidence that the migration was worth doing rather than
merely necessary.

C# 14's `extension` members are a genuine fit for Jaunty's extension-method-heavy public API, but
they are **out of scope**: they require raising `LangVersion` to 14.0, which is blocked on the
`WhereExpressionVisitor` defect in §2.

## 5. Non-goals

- Raising `LangVersion` to 14.0, or fixing `WhereExpressionVisitor`'s handling of the C# 14
  `Enumerable.Contains` expression shape. Separate work, separate risk.
- Dropping `netstandard2.0` or `net472`.
- Adopting any .NET 10 library API.
- Restructuring the public API around C# 14 `extension` members.

## 6. Open questions

**Q1 — add `net10.0`, or replace `net8.0`?** The probe replaced, because that is the fastest way
to learn what breaks; it is not necessarily the right shipping answer.

- *Add* (`netstandard2.0;net8.0;net10.0`) keeps existing consumers whole and is the conventional
  choice for a published library. Costs a third build/test leg, and every `ProjectReference` in
  the repo hardcodes `SetTargetFramework="TargetFramework=net8.0"`, so each needs a net10 variant.
- *Replace* halves the matrix and is defensible once net8.0 is out of support, but strands anyone
  who cannot move by November.

`netstandard2.0` already covers old consumers for the core packages, which weakens the case for
keeping `net8.0` — but not for `Jaunty.FlatFiles.DuckDB`, `Jaunty.Scaffolding` and
`Jaunty.Scaffolding.Cli`, which are `net8.0`-only today.

**Q2 — does the NativeAOT publish job still work?** Not exercised by the probe. Given §2's ILLink
findings and 009's measured "No mapper found for type Product" failure under `PublishAot=true`,
this needs its own check.

**Q3 — what actually changed under `TypedKeyGuard`?** See §2.

## 7. Acceptance criteria

1. No project targets `net8.0`, or all projects targeting `net8.0` also target `net10.0` —
   per the Q1 decision.
2. Full solution builds with `TreatWarningsAsErrors=true` and **0 errors**, with no ILLink
   diagnostic silenced by a suppression added for this migration.
3. All five CI suites pass on the new target with **0 failures**, and the skip count is no higher
   than the current net8.0 baseline given the same environment.
4. `net472` and `netstandard2.0` continue to build and test unchanged.
5. The four `samples/NativeAOT-*` projects publish with `PublishAot=true` and run correctly.
6. CI, release and AOT-publish workflows are green on the self-hosted runner.
7. Benchmark results are re-baselined and the delta against net8.0 recorded in
   `benchmarks/BENCHMARK-RESULTS.md`.
8. The `TypedKeyGuard` control test is either passing or deliberately rewritten, with the reason
   recorded.

## 8. References

- Probe branch: `probe/net10-feasibility` (throwaway; retains the retarget and its results)
- [009-aot-annotation-pass](../009-aot-annotation-pass/009-spec.md) — the ILLink findings in §2
  are the same defect reaching two more caches
- `Directory.Build.props` — the `LangVersion 13.0` pin and the C# 14 casualties behind it
- [.NET 10 runtime changes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)
- [.NET 10 library changes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
