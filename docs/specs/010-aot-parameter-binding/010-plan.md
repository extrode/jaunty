# Implementation Plan: AOT-Safe Parameter Binding

**Branch**: `dev`
**Date**: 2026-07-30
**Spec**: [010-spec.md](010-spec.md)
**Status**: implemented · Origin: round-27 carry-forward item 17

---

## Summary

**Primary requirement**: `connection.QueryFirst<Product>(sql, new { Id = 1 })` works in a NativeAOT
published binary with no change to consumer source.

**Technical approach.** The source generator reads the consumer's own `Query`/`Execute` call sites,
asks the semantic model what each parameters argument's static type is, and emits one
`JauntyAot.PreserveParameters<ThatType>()` per distinct type into a `[ModuleInitializer]`. The
annotation on that method's type parameter is what makes the trimmer keep the getters.

This was not the obvious approach, and it only became visible after measurement. Three facts, none of
which was assumed:

1. A `[DynamicallyAccessedMembers(PublicProperties)]` annotation on a generic type parameter preserves
   its type argument **program-wide**, and does **not** have to be at the call site. Without this the
   whole approach is impossible.
2. Anonymous type identity is **structural and per-assembly**, so `new { Id = default(int) }` emitted
   into a generated file *is* the type behind `new { Id = 1 }` in the consumer's file. This is the only
   way to reach a type that cannot be named.
3. The rooting call must be in **reachable** code, or the trimmer deletes it along with the
   annotations inside it.

Fact 2 was found by accident: the first probe was self-contaminating, because two cases that were
supposed to be independent shared one anonymous type. The contamination *was* the mechanism.

---

## Step 0 — Measure the candidate remedies (blocking)

Eight cases, published with `PublishAot=true` and executed. osx-arm64, net8.0, 2026-07-30. Each case
uses a type **no other case mentions** — the first attempt did not, and its results were meaningless
in the most flattering possible direction (six of eight "passing").

| Case | What the consumer writes | Result |
|---|---|---|
| A | `new { Pa = 1 }`, straight through | **FAIL** — `Available properties:` empty |
| B | `new QueryB { Pb = 1 }`, straight through | **FAIL** |
| C | `Params(new QueryC { … })` — DAM-annotated generic wrapper at the call site | OK |
| D | `Params(new { Pd = 1 })` — same wrapper, anonymous type | OK |
| E | `Dictionary<string, object?>` | OK |
| F | `Preserve<QueryF>()` once at startup, POCO then passed straight through | OK |
| G | Property read in ordinary consumer code, no annotation | **FAIL** |
| H | `[DynamicDependency(PublicProperties, typeof(QueryH))]` on the calling method | OK |

Then the same question asked of generated code — rooting calls in a **separate file**, nothing at the
call sites:

| Variant | Result |
|---|---|
| Rooting method present, never called | **FAIL** — trimmer removed the method and the annotations with it |
| Identical method with `[ModuleInitializer]` | **A and B both OK**, zero call-site changes |

What each row settled:

- **F** is why this design exists. Preservation is program-wide, so the generator does not have to
  rewrite call sites — it only has to name the types once, somewhere reachable.
- **G** contradicts the intuitive assumption and is worth keeping in mind: the trimmer keeps the field
  access and drops the getter *method*, which is what reflection needs. "But my code reads that
  property" is not preservation.
- **D** is the only mechanism that reaches an anonymous type. `PreserveParameters<T>()` and
  `[DynamicDependency]` both need a name.
- **H** means named types already had an escape hatch with no Jaunty change at all. It needed
  documenting, not inventing.

## Step 1 — `JauntyAot` (public, additive)

`src/Jaunty/JauntyAot.cs`. Three methods, all no-ops; the annotation on the type parameter is the
entire payload.

| Member | For |
|---|---|
| `T Parameters<T>(T)` | Call-site wrapper. The only form that reaches an anonymous type by hand. |
| `void PreserveParameters<T>()` | One-time registration for a named type. |
| `void PreserveParameters<T>(T witness)` | What the generator emits for anonymous types — inference from a throwaway instance of the same shape. |

Guarded `#if NET5_0_OR_GREATER`, so on netstandard2.0 and net472 they compile to plain no-ops. That is
correct rather than merely convenient: those targets have no trimmer, and consumer code calling these
then compiles unchanged everywhere.

## Step 2 — The generator emits the rooting

`src/Jaunty.SourceGenerator/ParameterRooting.cs`, a partial of `JauntyGenerator`. A second pipeline,
and the first one in this generator that reads **call sites** rather than type declarations.

- **Predicate** (syntax only, runs on every keystroke): an invocation named `Query…`/`Execute…` with
  ≥2 arguments, or `Values` with ≥1. Deliberately imprecise; the semantic filter rejects the rest.
- **Transform**: resolve the symbol, require a Jaunty namespace, find the `object`-typed parameter
  named `parameters`/`values`, take the bound argument's static type.
- **Skipped, needing nothing**: compile-time `null`, dictionaries (bound by key), scalars (named from
  the SQL text).
- **Rooted**: a nameable type as a type argument; an anonymous type as a reconstructed witness,
  preserving property order, because order is part of its identity.
- **Reported (`JAUNTYGEN003`)**: `object`, `dynamic`, a type parameter, or a type that cannot be named
  from generated code — except when the argument is a forwarded parameter.

Emitted output is sorted and de-duplicated, so unchanged source produces byte-identical output.
Emission is gated on the compilation actually having `ModuleInitializerAttribute` and C# 9.

## Step 3 — Clear the remaining warnings honestly

Four Jaunty-origin trim warnings survived spec 009. All four came from the **same mistaken pattern**:
`[DynamicallyAccessedMembers]` on a `Type`-typed parameter that is only ever fed by `GetType()`.

Such an annotation cannot be satisfied by any caller, propagates nothing, and preserves nothing. Its
only effects were to relocate the complaint (IL2072 at the call sites) and to add a new one (IL2111,
for handing the annotated method to `GetOrAdd` as a delegate — the trimmer is right that it cannot see
through a delegate).

| Site | Change | Warnings removed |
|---|---|---|
| `ParameterCache.Get` / `BuildMetadata` | Annotation removed; direct call instead of method group | 2 × IL2072, 1 × IL2111 |
| `SqlDialectFactory.BuildInnerConnectionAccessor` | Same | 1 × IL2111 |
| `LoggingInterceptor.GetPublicProperties` | Same | (not reachable in the sample) |

Each keeps one `[UnconditionalSuppressMessage]` at the site that actually reflects — the honest place —
with a justification naming where preservation really comes from.

**`LoggingInterceptor` is worth a note.** AUD-R26-055 replaced a false justification there with a
second false one: it said preservation came from the annotation on the type parameter. It did not, for
exactly the reason above. Round 26 corrected the sentence about anonymous types and left the mechanism
claim unexamined — the same error one layer in.

## Step 4 — Verification

1. **Generator tests** — `tests/Jaunty.SourceGenerator.Tests/ParameterRootingEmissionTests.cs`, 23
   cases: what is rooted, what is deliberately not, `JAUNTYGEN003` firing and not firing, and — the
   one a published binary cannot substitute for — that **the emitted rooting compiles**, over seven
   shapes including nested anonymous, generic, record and nullable.
2. **Drift test** — `tests/Jaunty.Tests/Unit/AotPreservationTests.cs`, asserted against compiled
   metadata: every type parameter on `JauntyAot` carries `PublicProperties`. Delete the attribute and
   everything still compiles, every test still passes, and the binary silently breaks. There is no
   other signal.
3. **The executable proof (US-1)** — all four samples published with `PublishAot=true` and run.
4. **Negative verification** — the emission reverted **in place**.
5. **No regression** — full suite and Release build on every TFM.

### Measured results

Samples, published and executed:

| Sample | Before 010 | After 010 |
|---|---|---|
| `NativeAOT-Basic` | mapped rows, then **FAIL** on `new { Id = 1 }` | **PASS**, end to end, source unchanged |
| `NativeAOT-CustomMapper` | PASS | PASS |
| `NativeAOT-WithReflection` | PASS | PASS |
| `NativeAOT-FluentQuery` | FAIL — `decimal` operator, in consumer code | FAIL, unchanged (item 18) |

Jaunty-origin trim/AOT warnings in a published app: **4 → 0**.

Suite: Jaunty.Tests 5,400 / 4 (baseline 5,394 / 4; +6 new, same four environmental SQL Server CSV
cases, AUD-R26-049), Fluent 1,258 / 0, DuckDB 620 / 0, FlatFiles 185 / 0, Scaffolding 555 / 0,
Cli 34 / 0, SourceGenerator 109 / 0 (was 86), Fluent.SourceGen 9 / 0. Release build clean on
netstandard2.0 and net8.0.

**Negative verification.** Deleting the single line that emits `[ModuleInitializer]` restored the
original failure exactly — `No property found on type '<>f__AnonymousType0`1' matching SQL parameter
'@Id'. Available properties:` — with the generated rooting file still present and looking correct. One
attribute is the whole difference between working and broken, which is why step 4.2 exists.

## What went wrong on the way, because it is the useful part

- **JAUNTYGEN003 broke Jaunty's own build, twice.** First it fired at all 170 internal sites that
  forward `object? parameters` to the next overload; excusing forwarded parameters fixed that. Then a
  refinement that reported forwarded *type parameters* — which is genuinely useful for a consumer's
  `Repository<T>` — fired six times on Jaunty's own generic write plumbing, where the entity is
  forwarded into an **observation-only** `parameters` argument that feeds interceptors and logging,
  not binding. The shapes are syntactically identical. Separating them would have been a guess, so the
  refinement was reverted and the gap documented.
- **A syntax check for `null` missed `(object?)null`.** The library's own no-parameter overloads spell
  it as a cast, not a literal, so twenty call sites in Jaunty were reported. Asking the semantic model
  for the constant value is both shorter and correct.
- **`parameters!` is not an identifier.** The forwarding check missed the single most likely spelling
  in nullable-enabled code. Caught only because a test probe happened to write it that way.
- **17 tests failed with empty generated source, which read exactly like a broken generator.** The
  harness had not referenced `System.Data.Common`, so `IDbConnection` was an error type, the
  invocation bound to nothing, and the generator correctly did nothing. The sibling harness documents
  this trap for `[Table]`; now this one does for `IDbConnection`.
- **One test asserted the wrong thing and found a real gap.** It claimed a consumer's `object`-typed
  wrapper would have its outer call site rooted. It is not, and cannot be. The test now asserts the
  gap so it is not rediscovered as a bug, or closed by accident without a decision.

## §7 — What is deliberately not done

- **`NativeAOT-FluentQuery` still fails.** Round-27 item 18, and out of scope per §4: it throws from
  `Expression.GetUserDefinedBinaryOperatorOrThrow` inside `Program.<Main>$`, building
  `x => x.UnitPrice > 20m`, before any Jaunty code runs.
- **The forwarding gap is not closed.** Closing it means inter-procedural analysis of the consumer's
  own call graph. Documented on `JauntyAot` with the two one-line fixes instead.
- **Open question 1 is still open.** Whether `JAUNTYGEN003` should be gated on the consumer's
  `PublishTrimmed` is a real question that JAUNTYGEN002 shares. Recorded, not answered.
- **The 170 `object? parameters` signatures are untouched.** Threading a generic type parameter
  through them would propagate annotations properly and is a breaking change with no advantage over
  what was built.
