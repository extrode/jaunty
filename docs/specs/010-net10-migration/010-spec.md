# .NET 10 Migration — Feature Specification

> spec.md — The "what" and "why". No technical implementation details.

Status: draft · Created: 2026-07-29 · Origin: SDK/CI divergence investigation, 2026-07-29

---

## 1. Problem Statement

Every runtime Jaunty targets leaves support on the same day.

| Target | Released | Support ends |
|---|---|---|
| .NET 10 (LTS) | 2025-11-11 | **2028-11-14** |
| .NET 9 (STS) | 2024-11-12 | **2026-05-12 — already out of support** |
| .NET 8 (LTS) | 2023-11-14 | **2026-11-10** |
| .NET Framework 4.8 | — | indefinite (Windows component) |

Corrected 2026-07-29: an earlier revision of this table gave .NET 9 an end date of 2026-11-10.
STS is 18 months, not 36, so .NET 9 left support on 2026-05-12 — before this spec was written.
Nothing in the repo targets .NET 9, so the error was cosmetic, but the "everything expires
together" framing rested partly on it.

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

**They are not four instances of one problem, and 009 does not currently own any of them.** An
earlier revision of this spec called them "the identical problem already specified in
[009-aot-annotation-pass](../009-aot-annotation-pass/009-spec.md)". That was wrong twice over:
009 §6 *explicitly lists* `LoggingInterceptor` and `ParameterCache` as **out of scope**
(009-spec.md:141-144, "a separate audit; only the three on the two caches are known-false"), and
the four sites do not share a single fix. They split three ways:

| Site | Class | Fix |
|---|---|---|
| `ParameterCache.cs:40` (IL2111) | Distinct and self-contained | The DAM-annotated `Type` is already in hand at `:36-38`; closing over it instead of passing the method group restores flow analysis. No suppression, ~5 lines. Use a `TryGetValue` fast path so the closure does not allocate per call. |
| `ParameterBinder.cs:81`, `:920` (IL2072) | 009's hazard class | Both pass `parameters.GetType()` off an unannotatable `object`. No annotation can express this; it needs the source-generated binding path 009 exists to build. |
| `LoggingInterceptor.cs:174` (IL2111) | Third case | No annotated `Type` to close over, so a lambda merely trades IL2111 for IL2067. Its existing suppression at `:194` ("anonymous types and records whose properties are always preserved") is **false for named POCOs**, which is the same defect class 009 was opened to correct. |

So the sequencing is: fix `ParameterCache.cs:40` here and independently; **widen 009's scope** to
cover `LoggingInterceptor` and the two `ParameterBinder` sites, or move them into this spec. As
the two specs stand today, nobody owns three of the four. Patching them with fresh suppressions
would repeat the mistake 009 exists to correct.

**Tests on `net10.0`**, Release, warnings non-fatal:

| Suite | Result |
|---|---|
| Jaunty.Tests | 2864 passed, **1 failed**, 2437 skipped |
| Jaunty.SourceGenerator.Tests | 72 passed |
| Jaunty.Fluent.Tests | 1189 passed, **4 failed** |
| Jaunty.Scaffolding.Tests | 555 passed, 24 skipped |
| Jaunty.FlatFiles.Tests | **not run on net10.0** |
| Jaunty.FlatFiles.DuckDB.Tests | **not run on net10.0** |

**The probe covered four of CI's five test steps, not five.** The FlatFiles/DuckDB step
(`ci.yml:113-121`) was never executed against net10.0, so whether `DuckDB.NET.Data.Full 1.3.0`'s
native binaries load there is unmeasured — the one place in the repo with a native dependency, and
therefore the most likely to break. Running it is the first task of the plan, not an assumption of
it. (Both suites pass on net8.0: 185 and 583.)

The 2437 skips are pre-existing and environmental — no SQL Server, PostgreSQL or MySQL
connection strings on the probe machine. CI supplies SQL Server, so the CI skip count is lower.

**The 4 Fluent failures were not a net10 problem — they were a shipping defect, now fixed.**
Bisected against the language version on `net8.0` with everything else held fixed: C# 14 fails
4/4, C# 13 passes 4/4, C# 12 passes 4/4.

An earlier revision of this spec attributed that to instance `List<T>.Contains` and static
`Enumerable.Contains` swapping argument positions, and treated the `LangVersion 13.0` pin as
sufficient. **Both were wrong.** Measured against the .NET 10 SDK on 2026-07-29, for `int[] ids`:

| LangVersion | Tree emitted for `ids.Contains(p.Id)` |
|---|---|
| 13 | `System.Linq.Enumerable.Contains` — arg0 `MemberAccess : Int32[]` |
| 14 | `System.MemoryExtensions.Contains` — arg0 `Call : ReadOnlySpan<int>` (`ReadOnlySpan<T>.op_Implicit`) |

The rebinding is array-specific, which is why `Visit_ListContains_GeneratesInClause` never failed.
`WhereExpressionVisitor.cs:263` required `DeclaringType == typeof(Enumerable)` and `:265` a
non-null receiver, so the span form matched neither and fell through to the `:302` fallback, which
compiled a subtree still referencing `p`.

**The expression tree is built by whoever writes the lambda.** Jaunty's `LangVersion` pin governs
only Jaunty's own compilation, so any consumer on the .NET 10 SDK writing
`.Where(p => ids.Contains(p.Id))` hit this against the shipped package. The pin made Jaunty's tests
green while the product stayed broken. Fixed on `fix/csharp14-span-contains`: the visitor now
recognises the `MemoryExtensions` form and unwraps the span conversion, and the fallback throws
`NotSupportedException` naming the method when the subtree references the lambda parameter. Six
tests build the tree by hand in the C# 14 shape, so they hold under the 13.0 pin; the Fluent suite
passes 1199/1199 under **both** language versions.

**The 1 Jaunty.Tests failure is genuinely net10-specific.**
`TypedKeyGuardTests.TheOldFormStillBoxes_WhichIsWhatMakesTheMeasurementMeaningful` passes on
net8.0 under both C# 13 and C# 14, and fails only on net10.0. The test asserts that an old code
path *does* box, as the control for a boxing measurement. If .NET 10 no longer boxes there, the
control is invalid and the test — not the product code — needs rethinking. **Unverified:** the
cause has not yet been confirmed; it is assumed to be a runtime behaviour change.

## 3. Scope

1. Run the FlatFiles/DuckDB suites on net10.0 — the one unmeasured leg (§2).
2. Add `net10.0` alongside `net8.0` (Q1, resolved — §6), including the `ProjectReference` pins.
3. Resolve the four ILLink diagnostics per the three-way split in §2.
4. Resolve the `TypedKeyGuard` control test.
5. Add `net10.0` legs to CI and release: six `--framework net8.0` occurrences across `ci.yml` (5)
   and `release.yml` (1), plus the AOT-publish job.
6. Confirm the NativeAOT samples publish and run on net10.0.
7. Capture the net8.0 benchmark baseline **before** the retarget lands, then re-baseline on
   net10.0 and record the delta (§4).
8. Adopt net10-only improvements behind `#if NET10_0_OR_GREATER`, matching the existing
   `NET5_0_OR_GREATER` idiom, wherever a measured win justifies the branch (§4).

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

Since `net8.0` is retained (Q1), anything net10-only is reached through
`#if NET10_0_OR_GREATER`, the same idiom the codebase already uses for `NET5_0_OR_GREATER`. A
repo-wide check on 2026-07-29 found no exact-TFM conditionals — every one is `_OR_GREATER` — so
adding a third target does not silently change which branch an existing conditional takes.

C# 14's `extension` members are a genuine fit for Jaunty's extension-method-heavy public API. They
are still **out of scope** here: they require raising `LangVersion` to 14.0, which is now
*unblocked* (the `WhereExpressionVisitor` defect in §2 is fixed) but is its own decision with its
own risk, and the 13.0 pin has independent justification.

## 5. Non-goals

- Raising `LangVersion` to 14.0. No longer blocked, but a separate decision.
- Dropping `netstandard2.0`, `net472` or `net8.0`.
- Adopting any .NET 10 library API for its own sake — §4 finds nothing worth having.
- Restructuring the public API around C# 14 `extension` members.

## 6. Resolved questions

**Q1 — add `net10.0`, or replace `net8.0`? → ADD.** Decided 2026-07-29. Targets become
`netstandard2.0;net8.0;net10.0` for the four core packages, and `net8.0;net10.0` for
`Jaunty.FlatFiles.DuckDB`, `Jaunty.Scaffolding` and `Jaunty.Scaffolding.Cli` (net8-only today —
verified at `Jaunty.FlatFiles.DuckDB.csproj:4`, `Jaunty.Scaffolding.csproj:4`,
`Jaunty.Scaffolding.Cli.csproj:5`).

What keeping `net8.0` buys, which is what decided it:

- Without it, net8 consumers fall back to the `netstandard2.0` build and lose `IsTrimmable` and
  `IsAotCompatible` entirely — both are conditioned on net8-compatible TFMs at
  `src/Directory.Build.props:11-12`. For an ORM whose pitch is NativeAOT, that is the whole story
  gone.
- The three net8-only projects have no `netstandard2.0` fallback at all, so replacing means `NU1201`
  for anyone on net8 — they could not reference them.
- net8 is LTS until 2026-11-10 and is supported for another fifteen weeks.

Cost, measured rather than asserted: a third build/test leg on the self-hosted runner, and net10
variants of the **39** `SetTargetFramework="TargetFramework=net8.0"` pins. (An earlier revision
claimed *every* `ProjectReference` pins net8.0; **18** pin `netstandard2.0`.)

## 6a. Open questions

**Q2 — does the NativeAOT publish job still work?** Not exercised by the probe. Given §2's ILLink
findings and 009's measured "No mapper found for type Product" failure under `PublishAot=true`,
this needs its own check.

**Q3 — what actually changed under `TypedKeyGuard`?** See §2.

**Q4 — do the DuckDB native binaries load on net10.0?** Unmeasured; see §2 and §6b.

## 6b. Risks

- **AC2 depends on a spec that cannot yet be planned.** "No ILLink diagnostic silenced by a new
  suppression" cannot be met for `ParameterBinder.cs:81/:920` until 009's source-generated binding
  path exists, and 009 is a draft whose own open questions block planning (009-spec.md:173). This
  is a hard dependency, not sequencing advice. Either 009 lands first, or AC2 is scoped to the
  three sites that can be fixed without it and the `ParameterBinder` pair is explicitly deferred.
- **DuckDB native binaries on net10 are unmeasured** (§2). If they do not load, `Jaunty.FlatFiles.DuckDB`
  cannot take a net10 leg on the schedule the rest of the work assumes.
- **The net8.0 benchmark baseline must be captured before the retarget**, or AC7's delta has
  nothing to compare against.

## 7. Acceptance criteria

1. Every project that targets `net8.0` also targets `net10.0`, and no existing target is dropped
   (Q1: add, not replace).
2. Full solution builds with `TreatWarningsAsErrors=true` and **0 errors** on every target, with
   no ILLink diagnostic silenced by a suppression added for this migration — except the
   `ParameterBinder.cs:81/:920` pair, which is deferred to 009 by name if 009 has not landed. Any
   other new suppression fails this criterion.
3. All five CI suites — including FlatFiles/DuckDB, unmeasured at spec time — pass on **both**
   `net8.0` and `net10.0` with **0 failures**, and the skip count is no higher than the current
   net8.0 baseline given the same environment.
4. `net472` and `netstandard2.0` continue to build and test unchanged.
5. The four `samples/NativeAOT-*` projects publish with `PublishAot=true` and run correctly.
6. CI, release and AOT-publish workflows are green on the self-hosted runner.
7. Benchmark results are re-baselined and the delta against net8.0 recorded in
   `benchmarks/BENCHMARK-RESULTS.md`.
8. The `TypedKeyGuard` control test is either passing or deliberately rewritten, with the reason
   recorded.

## 8. References

- Probe branch: `probe/net10-feasibility` — **keep it**. §2's measured evidence is not reproducible
  without it, so it is the evidence record, not throwaway scratch.
- [009-aot-annotation-pass](../009-aot-annotation-pass/009-spec.md) — owns one of the four ILLink
  findings today and needs widening to own two more; see the §2 table
- `Directory.Build.props` — the `LangVersion 13.0` pin and the reasons behind it
- `src/Jaunty.Fluent/Expressions/WhereExpressionVisitor.cs` — the C# 14 span-`Contains`
  translation, fixed 2026-07-29 on `fix/csharp14-span-contains`
- [.NET 10 runtime changes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)
- [.NET 10 library changes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
