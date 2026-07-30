# Implementation Plan: API-Wide AOT Annotation

**Branch**: `dev`
**Date**: 2026-07-30
**Spec**: [009-spec.md](009-spec.md)
**Status**: implemented, with two deliberate exclusions (see §7) · Origin: AUD-R26 (round 26, batch 3)

---

## Summary

**Primary Requirement**: a consumer publishing with `PublishAot=true` gets working entity mapping and
parameter binding, rather than a runtime `No mapper found for type 'X'`.

**Technical Approach — and it is not the one the spec proposed.** The spec framed the work as an
annotation pass bounded at **645** `new()`-constrained public overloads that would each need
`[DynamicallyAccessedMembers]` propagated through them (§2). Planning changed the answer, because
annotation only propagates *from a reflection site that still exists*. Removing the site removes the
obligation.

1. New public `IGeneratedAccessors<T>` (`src/Jaunty/Interfaces/IGeneratedAccessors.cs`) exposing the
   generated mapper and the three binders as delegates.
2. The generator implements it per entity, each body a **static method group reference** — an ordinary
   IL call the trimmer must honour. That reference is the entire preservation mechanism.
3. `MappedCache<T>` and `WriteParameterCache<T>` resolve through the interface first, keeping their
   `GetMethod` paths only for types the generator did not produce.
4. The three `[UnconditionalSuppressMessage]` justifications rewritten to describe a design rather
   than a defect.
5. `JAUNTYGEN002` warns when a type implements `IMapped<T>` by hand — the population the fallback
   still serves, and the one the trimmer still breaks.

**645 became 0.** No public overload needed annotating.

### Why this shape

Two facts made the reflection site removable, and both were already in the repo:

- `IMapped<T>` already declares `static abstract T ReadEntity(IDataReader)` on net8+
  (`src/Jaunty/Interfaces/IMapped.cs`) — the member is a contract, not a convention.
- The same generator already exposed entity **metadata** reflection-free through an instance interface
  reached by a cast: `SourceGeneratedMetadataResolver.TryBuild<T>()` does
  `typeof(IEntityMetadataSource).IsAssignableFrom(typeof(T))` then `(IEntityMetadataSource)new T()`,
  and has consequently never needed an annotation or a suppression
  (`src/Jaunty/Internals/Entity/SourceGeneratedMetadataResolver.cs:22-25`).

Metadata took the safe route; mappers and binders took the reflective one and collected the
suppressions. This makes the second follow the first.

Instance members rather than `static abstract` ones, deliberately: an instance interface reached by a
cast behaves identically on netstandard2.0, where static abstract members do not exist and
`DynamicallyAccessedMembersAttribute` is unavailable. One mechanism on every target beats a net8-only
path that only AOT consumers exercise. Cost is one `new T()` per closed generic — already paid by
`ResolveMetadata`.

---

## Technical Context

| Field | Value |
|-------|-------|
| **Language** | C# (netstandard2.0, net8.0) |
| **Dependencies** | none added |
| **Testing** | xUnit; plus published-and-executed AOT binaries |
| **Platform** | measured on macOS arm64, SDK 10.0.301, ilcompiler 8.0.28 |
| **Public API** | one additive interface; no existing signature changed |
| **Performance** | one `new T()` + one cast per closed generic, replacing `GetMethod` + `CreateDelegate` |

---

## Step 0 — Baseline, measured 2026-07-30 before any change

Published with `dotnet publish -c Release` from each sample's own directory; the csproj already carries
`PublishAot`, and passing `-p:PublishAot=true` fails `NETSDK1207` on netstandard2.0 project references
(spec §7).

| Sample / probe | Before | Cause |
|---|---|---|
| `NativeAOT-Basic` | **FAIL** `No mapper found for type 'Product'` | `MappedCache` `GetMethod` found nothing |
| `NativeAOT-CustomMapper` | PASS | `CommandOptions<T>.WithMapper` is a delegate — `DrDispatcher` step 1 |
| `NativeAOT-FluentQuery` | **FAIL** `binary operator GreaterThan is not defined for … 'System.Decimal'` | consumer's own expression tree; `decimal.op_GreaterThan` trimmed |
| `NativeAOT-WithReflection` | **FAIL** `Column 'category_id' does not map to any property` | `Jaunty.Extensions.Reflection`; out of scope per §6 |
| probe: parameters | **FAIL** anonymous *and* named POCO | `ParameterCache`; getters trimmed |
| probe: write path | **FAIL** `No parameter binder found for type 'Widget'`, 0 rows | `WriteParameterCache` `GetMethod` |

**Control**: the same `NativeAOT-Basic` code under `dotnet run` (JIT) produced fully correct output. So
the members were generated and then trimmed — trimming, not generation.

**Jaunty-origin warnings: exactly 4, and not one of them IL2090.**

| Warning | Site |
|---|---|
| IL2111 | `src/Jaunty/Dialects/SqlDialectFactory.cs:281` |
| IL2111 | `src/Jaunty/Internals/Parameters/ParameterCache.cs:40` |
| IL2072 | `src/Jaunty/Internals/Parameters/ParameterBinder.cs:81` |
| IL2072 | `src/Jaunty/Internals/Parameters/ParameterBinder.cs:918` |

The absence of IL2090 is the point of US-2's third criterion: the three suppressions kept the warning
list clean while the runtime failed. **A green build was never evidence here.**

### The spec's three blocking questions, answered by measurement

**Q1 — annotate the API, or root the generated code?** Neither, quite: *eliminate the reflection*.
Rooting via `[DynamicDependency]` would have preserved the target of a reflective lookup; handing over
a delegate removes the lookup. Also avoids a `ModuleInitializerAttribute` polyfill for netstandard
consumers.

**Q2 — how many of the 645 are on a reflecting path?** **Zero**, once the path is not reflective.

**Q3 — what does the build-time diagnostic look like?** `JAUNTYGEN002`, Warning, naming the type and
all three ways out. Warning rather than Error because a hand-written `IMapped<T>` is legitimate on the
JIT and must not break those builds (US-3).

**Q4 — is `IsAotCompatible` a false claim to withdraw?** Kept set, by decision. Clearing it also clears
`EnableAotAnalyzer`, which would silence the very warnings the work is measured by.

### Two step-0 findings that changed the plan

- **`IdSetter` was *not* added to the interface.** The plan allowed it if `CreateIdSetter`'s
  suppressions (`WriteParameterCache.cs:349-350`) proved false. At step 0 the question was masked by the
  binder failure. Re-measured after step 3: the write probe reported `IdSetter wrote Id=1`, so the
  suppressions are **true** and the identity setter survives trimming. No public API added on an
  unmeasured premise.
- **`Expression.Lambda(...).Compile()` does not warn.** Zero IL3050 across all four publishes, so the
  five sites (`WriteParameterCache.cs:320,379`, `MultiRowInsertCache.cs:61`, `ParameterCache.cs:100`,
  `EntityDataReader.cs:258`) are covered by LINQ's interpreter fallback. **That risk is closed.**

---

## What changed

| File | Change |
|---|---|
| `src/Jaunty/Interfaces/IGeneratedAccessors.cs` | **new** — the five accessors |
| `src/Jaunty.SourceGenerator/JauntyGenerator.cs` | third interface on the generated class; accessor emission; `JAUNTYGEN002` |
| `src/Jaunty.SourceGenerator/AnalyzerReleases.Unshipped.md` | `JAUNTYGEN002` registered |
| `src/Jaunty/Internals/Read/MappedCache.cs` | `Accessors` field; interface-first in both resolvers; two justifications rewritten |
| `src/Jaunty/Internals/Write/WriteParameterCache.cs` | `Accessors` field; interface-first in `Bindings.Build()`; one justification rewritten |
| `tests/Jaunty.SourceGenerator.Tests/GeneratedAccessorsEmissionTests.cs` | **new**, 14 |
| `tests/Jaunty.Tests/Unit/Internals/GeneratedAccessorsResolutionTests.cs` | **new**, 9 |

Two details that are load-bearing:

- **Pre-net8 `RowMapper` constructs per row.** Below net8.0 `ReadEntity` is an instance member, and the
  reflective path it replaces did `r => openDelegate(new T(), r)`. Hoisting the instance out of the
  lambda would silently change behaviour for any `ReadEntity` that touches `this`, on exactly the
  targets least likely to be exercised.
- **`Accessors` is declared before the fields that read it.** Static initialisers run in textual order.
  In `WriteParameterCache` the lookup sits *inside* `Bindings.Build()`, so the AUD-R26 batch-4
  configuration-generation fix keeps working: order is still generated → reflection-by-name →
  `JauntyConfig` resolver.

---

## Verification

**Negative verification is what proves this, and it was done on the published binary.** With the
interface preference reverted in place (`cp` backups, never `git stash`),
`NativeAOT-Basic` failed again with `No mapper found for type 'Product'`, and 6 of the 9 new unit tests
went red — precisely the 6 asserting the interface wins; the 3 that stayed green are the ones that
should pass either way. Restored, and green again.

The failure mode under revert is itself informative: nothing casts to `IGeneratedAccessors<Product>`, so
the trimmer drops the accessor properties *and* the `ReadEntity` they referenced, and the reflective
lookup then finds nothing. The preservation chain is **cast → accessor → member**, and breaking the
first link collapses all three. Removing any one of them re-breaks AOT.

### Published binaries, after

| Sample | After | Note |
|---|---|---|
| `NativeAOT-Basic` | mapper **fixed** — all 5 products map; then fails on `new { Id = 1 }` | see §7 |
| `NativeAOT-CustomMapper` | PASS | unchanged |
| `NativeAOT-FluentQuery` | still fails on `decimal` operator | not 009; see §7 |
| `NativeAOT-WithReflection` | **now PASSES** | incidental, see below |
| probe: write path | **PASS** — row written, `IdSetter wrote Id=1` | |

`NativeAOT-WithReflection` passing is a side effect, not a design goal, and worth stating precisely:
`Category` is *also* a `[Table]` entity, so rooting its generated `ReadEntity` roots the property
accessors the reflection extension needs. An entity without `[Table]` would still fail, and
`Jaunty.Extensions.Reflection` remains out of scope and not AOT-safe in general.

### Test results

| Project | Result |
|---|---|
| Jaunty.Tests | 5,394 passed / **4 failed** — the 4 known environmental SQL Server CSV cases (AUD-R26-049: BULK INSERT cannot reach a host path from its container) |
| Jaunty.Fluent.Tests | 1,258 / 0 |
| Jaunty.FlatFiles.DuckDB.Tests | 620 / 0 |
| Jaunty.FlatFiles.Tests | 185 / 0 |
| Jaunty.Scaffolding.Tests | 555 / 0 |
| Jaunty.Scaffolding.Cli.Tests | 34 / 0 |
| Jaunty.SourceGenerator.Tests | 86 / 0 (72 + 14) |
| Jaunty.Fluent.SourceGen.Tests | 9 / 0 |

Release build clean on every TFM. Baseline matched exactly: 5,385 + 9 new = 5,394.

**One self-inflicted regression, found and fixed here.** The first version of
`GeneratedAccessorsResolutionTests` called the process-wide `JauntyConfig.Reset()` without isolating or
restoring it, and 46 tests elsewhere failed with `No mapper found for type 'Dictionary\`2'`/`'Category'`
— everything that ran afterwards and needed the reflection extension. Fixed with
`[Collection("Type Handler Operations")]` and a restoring `Dispose`, following
`ConfigurationGenerationTests`. Worth recording: the defect this spec exists to fix is a global-state
hazard, and the test for it walked into the same hazard.

`Jaunty.Fluent.SourceGen.Tests` is not listed in `Jaunty.slnx` and so is skipped by a solution-wide
`dotnet test`; run explicitly above. Pre-existing, unrelated to 009.

---

## 7. What is deliberately not done

**US-1 does not fully pass, and the reason is a genuine conflict inside the spec.** §6 places
`ParameterCache` out of scope; US-1 requires the published sample to run; and
`samples/NativeAOT-Basic/Program.cs:42` passes `new { Id = 1 }`. Measured both ways:

```
anonymous  : FAIL -> No property found on type '<>f__AnonymousType0`1' matching '@Id'. Available properties:
named POCO : FAIL -> No property found on type 'ProductQuery'          matching '@Id'. Available properties:
no params  : OK
```

`Available properties:` is **empty** — the getters are gone. Note the named POCO also fails: the usual
escape hatch ("the consumer roots their own parameter type") is not automatic even for a type they own,
and is impossible for an anonymous type. This is a second, independent AOT defect of the same shape as
the one 009 fixed, and it needs its own spec — plausibly the same remedy, a generated parameter
accessor. **Reported, not silently absorbed into this one.**

**`NativeAOT-FluentQuery`** fails in the consumer's own `Program.<Main>$`, building `x => x.UnitPrice >
20m`, because `decimal`'s operator methods are trimmed. It never reaches Jaunty. Third distinct defect,
also its own spec.

Consequently the round-26 batch-3 finding **stays OPEN**: it has been open on purpose for 23 rounds and
should close on a published binary running end to end, not on the half of it that now works. Its status
line records what is fixed and what is not.

---

## Risks

- **`IGeneratedAccessors<T>` is permanent public API.** Named before emitting;
  `IGeneratedMappingSource<T>` would sit closer to `IEntityMetadataSource` if a rename is wanted, and
  the window for that is now.
- **A prebuilt entity assembly compiled against an older Jaunty** keeps the reflective path and stays
  AOT-broken. `JAUNTYGEN002` cannot see it — different compilation. Release note.
- **`JAUNTYGEN002` fires for JIT-only consumers** who will never trim. If that proves noisy the fix is
  to gate it on the consumer's `PublishTrimmed` via a `CompilerVisibleProperty` in the package props,
  not to lower the severity — the condition it reports is real.
