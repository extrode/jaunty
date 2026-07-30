# API-Wide AOT Annotation — Feature Specification

> spec.md — The "what" and "why". No technical implementation details.

Status: draft · Created: 2026-07-29 · Origin: AUD-R26 (round 26, batch 3 — the finding is
deliberately still OPEN; see §7)

---

## 1. Problem Statement

Jaunty declares itself AOT-ready. `src/Directory.Build.props` sets `IsTrimmable` and
`IsAotCompatible` for net8.0 and later, the repo ships four `samples/NativeAOT-*` projects, and
NativeAOT support is a stated pillar in the coding standards ("No runtime reflection in core").

Two of the caches on the hot path do reflect at runtime, by method name:

- `MappedCache<T>` calls `typeof(T).GetMethod("ReadEntity")` and `GetMethod("CreateRowMapper")`
- `WriteParameterCache<T>` calls `typeof(T).GetMethod("BindInsert" | "BindUpdate" | "BindDelete")`

Nothing arranges for those methods to survive trimming. There is no `[DynamicDependency]`, no
`[DynamicallyAccessedMembers]` on `T`, and no ILLink descriptor. What there was instead, until
round 26, were three `[UnconditionalSuppressMessage]` attributes asserting that the members
"are source-generated and always preserved" — which is not a mechanism, and the analyzer
warning they silenced was correct.

The consequence is that the trimmer is free to remove the very methods the source generator
emitted, and on a trimmed or AOT-published application it does. Measured in round 26:
`samples/NativeAOT-Basic`, published with `PublishAot=true`, throws **"No mapper found for type
Product"** — the source-generated mapper is gone from the binary.

The suppressions were rewritten in round 26 to state this honestly. **The defect itself is
unfixed**, and this spec exists because fixing it properly is larger than the three attributes
it appears to be about.

## 2. Why This Is a Spec and Not a Patch

The correct annotation is known and was proven in round 26 to satisfy the analyzer: mark the
generic parameter `[DynamicallyAccessedMembers(PublicMethods)]` at the point of reflection.

It does not stop there. Trim annotations are *viral upward*: annotating `MappedCache<T>` makes
`T` an annotated parameter, so every caller that passes its own `T` through must carry the same
annotation or produce IL2091. The chain runs `MappedCache<T>` → `DrDispatcher.Resolve<T>` →
`QueryCore<T>` → the public API.

Measured against the compiled assembly on 2026-07-29 — counted from metadata, not from source
text, because a source-text sweep is exactly the thing that missed 15 sites in AUD-R26-030:

| | |
|---|---|
| public static methods on `Jaunty` | 711 |
| generic method definitions | 672 |
| **with a `new()`-constrained type parameter** | **645, across 60 distinct method names** |

`where T : new()` is the entity-type parameter — the one that reaches these caches. So the
population that may need annotating is 645 overloads, not the three attribute sites, and not
the "~300" the round-26 suppression text estimated. (Determining precisely which of the 645
actually flow into a reflecting cache is planning work, not spec work; 645 is the bound.)

Landing this halfway is worse than not landing it. An annotation on the cache without the
annotations on the callers compiles fine for us and emits **IL2091 in the consumer's build** —
we would move our warning into their project and fix nothing.

That is why round 26 reverted the annotation and corrected the justification text instead. The
precedent is the round-3 `ParameterCache.cs` finding that this one cites: a suppression whose
stated justification was false, corrected rather than papered over.

## 3. Vision

> "A consumer publishes with `PublishAot=true`, and Jaunty either works or fails at build time
> with a message naming what to do — never at runtime with a missing mapper."

## 4. Target Users

- **Developers publishing NativeAOT or trimmed applications** — today the failure is a runtime
  exception naming an entity type, with nothing connecting it to trimming.
- **Everyone else, indirectly** — the annotation must not introduce warnings into builds that
  do not enable trim analysis, and must not change behaviour on the JIT.
- **Us** — three suppressions currently document a known defect. They should document a design.

## 5. User Stories

### US-1: A published AOT app maps entities
> As a developer publishing with `PublishAot=true`, I want `Query<T>` to return mapped
> entities, not throw "No mapper found".

**Acceptance Criteria:**
- `samples/NativeAOT-Basic`, published with `PublishAot=true` and **run**, reads and writes
  entities successfully
- The same for `NativeAOT-CustomMapper` and `NativeAOT-FluentQuery`
- This is asserted by executing the published binary, not by a clean build — a clean build is
  what the current state already produces

### US-2: The library is honest at build time
> As a developer, I want trim warnings to tell me about my code, not about Jaunty's.

**Acceptance Criteria:**
- Publishing a trimmed or AOT app that uses Jaunty produces **no IL2xxx warnings originating
  in Jaunty**
- Building Jaunty itself produces no suppressed-but-live trim warnings: every remaining
  `[UnconditionalSuppressMessage]` in `src/` states a real preservation mechanism
- A consumer whose entity type genuinely cannot be preserved gets a build-time diagnostic
  naming the type, not a runtime exception

### US-3: Non-AOT consumers are unaffected
> As a developer on the JIT who has never heard of trimming, I want none of this to reach me.

**Acceptance Criteria:**
- Public API surface is unchanged (annotations are attributes, not signature changes)
- netstandard2.0 and net472 targets build unchanged — `DynamicallyAccessedMembersAttribute` is
  net5+, so every annotation is conditional
- No measurable change in JIT-path allocations or timings for `Query<T>`, verified the way this
  repo verifies performance claims: measured before and after

### US-4: The next person can tell whether it is done
> As a maintainer, I want a regression here to fail a test rather than surface as a user's bug
> report six months later.

**Acceptance Criteria:**
- A test asserts that the AOT sample's published binary runs and produces correct output
- A test or analyzer check asserts the annotation is present on every path that reaches
  `MappedCache<T>` and `WriteParameterCache<T>` — enumerated from compiled metadata, not from a
  source regex

## 6. Scope

**In scope**

- `MappedCache<T>` and `WriteParameterCache<T>`, the two reflecting caches, and the three
  suppressions on them
- The annotation chain from those caches out to the public API: `DrDispatcher`, `QueryCore`,
  and whichever of the 645 `new()`-constrained overloads are on a path that reaches them
- The corresponding Fluent entry points, which reach the same caches
- Whether `[DynamicDependency]` or an ILLink descriptor on the *generated* code is a better
  mechanism than annotating the *consuming* API — see §9
- The `samples/NativeAOT-*` projects, as the executable proof

**Out of scope**

- `Jaunty.Extensions.Reflection`. It is reflection by name and by design; it is not
  AOT-compatible and should say so rather than be annotated.
- The other `[UnconditionalSuppressMessage]` sites in `src/` (`Jaunty.Init`, `CsvImport`,
  `JauntyDiagnosticListener`). They are a separate audit; only the known-false ones are in scope.

**Scope amendment, 2026-07-30 (from spec 010).** Retargeting to `net10.0` surfaced four ILLink
diagnostics under the stricter net10 analyser, three of which landed in code this section had put
out of scope — so as the two specs originally stood, nobody owned them. Corrected:

| Site | Was | Now | Why |
| --- | --- | --- | --- |
| `ParameterBinder.cs:81` and `:920` (`IL2072`) | unlisted | **in scope** | `ParameterCache.Get(type)` called on an `object.GetType()` result. These are the two diagnostics 010 deliberately leaves standing, and `010-spec.md` AC2 defers them to this spec **by name**. Binder and cache are one flow; fixing the cache without its callers fixes nothing. |
| `ParameterCache.BuildMetadata` `IL2070` suppression (`:48-50`) | out of scope | **in scope** | Its own justification says "suppressed pending a source-generated parameter-binding path", which is this spec's remit by definition. |
| `ParameterCache.cs:40` `IL2111` | out of scope | **closed by 010** | Fixed in ~1 line by giving `GetOrAdd` a closure instead of a method group. Independently fixable, so it did not wait. |
| `LoggingInterceptor` `IL2111` + false justification | out of scope | **closed by 010** | The `[DynamicallyAccessedMembers]` annotation was decorative — the sole call site passes an unannotated `parameters.GetType()`. Removed on 2026-07-30, clearing the diagnostic with no suppression, and `:194`'s justification (which `010-spec.md:67` records as false for named POCOs) was rewritten to state the real limitation and point here. **No longer this spec's to fix; the remaining limitation is genuinely deferred and now says so.** |

The residue for this spec is therefore the `ParameterBinder` pair plus `ParameterCache`'s `IL2070`
suppression, all three on one code path, which is a better-shaped unit of work than the original
split.

**Superseded the same day.** That residue is now
[spec 011](../011-aot-parameter-binding/011-spec.md), which is `Status: implemented` — it deleted the
unsatisfiable annotation on `ParameterCache.Get`, which dissolved both `ParameterBinder` `IL2072`
diagnostics outright, and moved preservation to generated call-site rooting
(`JauntyAot.PreserveParameters<T>()`, with `JAUNTYGEN003` where the generator cannot see the type).
The three sites this table claimed for 009 are therefore all closed; measured on the merged tree,
`src/Jaunty` builds for `net10.0` with **0 warnings**. What remains 009's is unchanged and is the
harder half: `MappedCache<T>` and `WriteParameterCache<T>`, whose reflection is by *method name* on
`T` rather than by property on a runtime `Type`, so 011's call-site rooting does not reach it.
- Making `Jaunty.Extensions.Reflection`'s consumers AOT-safe.
- Trim-size optimisation. This is about correctness, not footprint.

## 7. Current State

The round-26 finding is **OPEN on purpose** and should stay open until this spec is planned.
Its `Status:` line and the three suppression justifications in `MappedCache.cs` and
`WriteParameterCache.cs` are the current record. They state the risk, the measured
reproduction, and why the annotation was not landed.

One practical note recorded there, because it cost time twice: the reproduction command from
the original round-3 finding **does not run as written**. `dotnet publish -p:PublishAot=true`
on a project that references Jaunty propagates `PublishAot` to the project references and fails
`NETSDK1207` on their netstandard2.0 targets. The sample's own csproj has to carry
`PublishAot`. That is very likely why the finding went twenty-three rounds without anyone
reproducing it.

## 8. Success Metrics

- All four user stories pass automated tests
- `samples/NativeAOT-Basic`, `-CustomMapper` and `-FluentQuery` published with `PublishAot=true`
  execute correctly — the current measured failure, "No mapper found for type Product", is gone
- Zero IL2xxx warnings attributable to Jaunty in a consumer's trimmed publish
- Every `[UnconditionalSuppressMessage]` remaining on the two caches names a real mechanism
- Public API unchanged; netstandard2.0 and net472 build unchanged

## 9. Open Questions

These block `/plan`, not this spec.

1. **Annotate the API, or root the generated code?** Annotating flows `[DynamicallyAccessedMembers]`
   out through up to 645 overloads. The alternative is to make the *generator* emit
   `[DynamicDependency]` (or an embedded ILLink descriptor) on the entity type it generates for,
   so the members are rooted at the definition site and no caller needs to know. That would be a
   far smaller change — but it only covers source-generated entities, and `MappedCache<T>`'s
   instance-`ReadEntity` fallback and the reflection-binder hooks are not generated. Does the
   generated-code route cover enough to be the answer, with a build-time diagnostic for the rest?
2. **How many of the 645 are actually on a reflecting path?** The bound is 645; the real number
   is smaller and must be derived from call-graph analysis rather than assumed. If it is a
   handful, question 1 answers itself.
3. **What does the build-time diagnostic look like for an entity that cannot be preserved?**
   US-2 requires failing at build rather than at runtime. A generator diagnostic naming the
   entity type is the obvious shape; whether it can fire at all for a hand-written `IMapped<T>`
   implementation is not obvious.
4. **Is `IsAotCompatible=true` currently a false claim we should withdraw first?** It is set for
   all net8+ targets today, while a published AOT sample demonstrably fails. Leaving it set
   while this is unfixed asserts something untrue to every consumer's build. Withdrawing it is a
   visible admission and possibly a package-metadata change; keeping it needs a reason.

## 10. Related

- `audit/round26/03-findings.md` → `MappedCache.cs, WriteParameterCache.cs` (OPEN)
- `src/Jaunty/Internals/Read/MappedCache.cs`, `src/Jaunty/Internals/Write/WriteParameterCache.cs`
  — the three corrected justifications
- `src/Directory.Build.props` — `IsTrimmable` / `IsAotCompatible`, per question 4
- `samples/NativeAOT-Basic` — the reproduction
  nothing", which is why §2's counts come from compiled metadata
