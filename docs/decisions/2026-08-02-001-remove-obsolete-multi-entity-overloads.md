# 001 — Remove the obsolete multi-entity overloads before v1.0.0

**Date:** 2026-08-02
**Status:** Accepted
**Affects:** `src/Jaunty/Read/QueryMultiEntity.cs`, `src/Jaunty/Read/QueryMultiEntityAsync.cs`

## Decision

Delete all 13 `[Obsolete]` multi-entity overloads — 7 sync, 6 async — with no replacement, and
document the supported mapping routes in `docs/01-api-reference/multi-entity-mapping.md` instead.

The 13 were marked obsolete on 2026-02-19 (`0644797a`) and never removed. All shared one shape,
everything after `sql` optional:

```csharp
Query<T1,T2>(this IDbConnection connection, string sql,
             object? parameters = null, CommandOptions options = default)
```

## Why

### The 12 duplicates were drifting

Twelve of the 13 had a full non-obsolete replacement family of six overloads each. They were not
inert copies — they had already diverged from the supported path twice:

- The sync `Query<T1,T2>` never received the eager `connection`/`sql` null-validation its async
  twins were retrofitted with.
- AUD-R26: they hardcoded a result-list capacity of 64, so a consumer who tuned
  `JauntyConfig.QueryResultCapacity` silently got the default here and nowhere else. The per-call
  `ExpectedRowCount` hint was unavailable to them at all, because the non-generic `CommandOptions`
  carries no such field.

They also forced `CommandOptionsGenericFormDriftTests` to carry an `ObsoleteAttribute` exemption
purely to accommodate them. Duplication that drifts is worse than deletion.

### The combiner had no counterpart — but its capability is covered

`Query<T1,T2,TResult>(sql, Func<T1,T2,TResult> map, ...)` was the only overload returning
`List<TResult>`, and its obsolete message pointed at the `CommandOptions<(T1,T2)>` overload, which
returns `List<(T1,T2)>` — a different job. Deleting it therefore looked like a capability loss.

It is not. `QueryStream<T1,T2>` is non-obsolete, name-mapped, honours `CommandOptions<(T1,T2)>`,
and streams — so graph assembly costs two extra lines and **no intermediate list**:

```csharp
List<ProductInfo> results = [];
foreach ((ProductInfo product, CategoryInfo category) in
         connection.QueryStream<ProductInfo, CategoryInfo>(sql))
{
    product.Category = category;
    results.Add(product);
}
```

Three further routes cover the rest: `Query<TDto>(sql)` for a flat DTO spanning both tables (column
name matching does not care which table a column came from), `CommandOptions<T>.WithMapper` for
arbitrary shapes read straight off the reader, and the Fluent join surface. The combiner's unique
contribution was ergonomic — typed, name-mapped entities handed to a projection lambda — not a
capability.

### The signature could not be fixed in place

The combiner also carried the AUD-R34-003 defect: a positionally-passed `CommandOptions` binds to
its `object? parameters` parameter and is silently discarded. The AUD-R34-002 implicit
`CommandOptions` → `CommandOptions<T>` conversion could not rescue it, because there was no
`CommandOptions<T>` sibling at that argument position for better-conversion-target to select.

Adding one was attempted and reverted: both overloads are then applicable to the named form
`(sql, map, options: x)`, which is CS0121 — the library's own tests called it that way. Keeping the
combiner through 1.0 therefore meant shipping the defect permanently.

### Cost of replacing it was disproportionate

A replacement family delegating to `QueryMultiEntityCore<T1,T2>` must materialise a
`List<(T1,T2)>` and project it, because that is the core's return type — the exact intermediate-list
allocation that argues against telling users to `.Select`. Avoiding it needs new projecting private
cores in both `QueryCore.cs` and `QueryCoreAsync.cs`, near-copies of existing methods including the
`options.Mapper` branch and the `DbConnection`/`IDbConnection` split: the drift-prone duplication
this decision exists to remove.

Worse, the combiner existed only at arity 2 while the tuple family runs to `T7`. A symmetric
replacement is 6 arities × 4 overloads × sync/async ≈ 48 public methods; an arity-2-only
replacement is a permanent asymmetry.

### Timing makes it free

Jaunty shipped v1.0.0-rc.1 with all 13 present but **is not public**. The cost asymmetry runs
entirely one way:

| | Now (pre-1.0) | Later |
|---|---|---|
| Remove | free | needs 2.0 |
| Add back | free | free, any 1.x |

Usage evidence: repo-wide the combiner had ~8 call sites, **all in its own tests** — zero in
`samples/`, `benchmarks/`, or the eShopOnWeb/Conduit torture-test ports.

## Revisiting

Re-introducing a combiner is purely additive and non-breaking in any 1.x. If a real consumer asks,
the shape to add is the four-overload family taking `CommandOptions<(T1,T2)>` (not
`CommandOptions<TResult>` — with `TResult` there are two independent producers of the result type,
`CommandOptions<T>.Mapper` being `Func<IDataReader,T>`, and no defined precedence), at whatever
arities are actually requested. Do **not** carry a combiner inside `MultiEntityCommandOptions<T1,T2>`:
that reintroduces mapper-in-options, which `CommandOptionsGenericFormDriftTests` exists to police and
whose dead `Mapper1..N` fields were already removed once.

## How this was decided

Two independent reviews, plus source verification. Both reviews changed
position on contact with facts, and both initial framings contained errors worth recording:

1. **First review** — verdict: delete all 13, but only if the combiner's deletion ships alongside a
   replacement family, because it is "the only map-projection entry point on the surface." It
   correctly demolished the idea that deleting the other 12 would fix the combiner's binding trap:
   `Query<T1,T2>` and `Query<T1,T2,TResult>` are different generic arities and never competed, and
   the trap is intrinsic to the combiner's own signature.

2. **The premise was wrong.** The request had not mentioned `CommandOptions<T>.WithMapper`, the flat
   DTO route, or the Fluent join surface. Map projection does not disappear when the combiner does.

3. **Second review** — verdict withdrawn and reversed to *delete, no replacement*. The decisive fact
   came from neither request: `QueryStream<T1,T2>` already provides typed, name-mapped, allocation-free
   graph assembly. It also observed that Fluent's own custom-shape route
   (`IJoinedQuery.Select<T>(Func<IDataReader,T>)`) is likewise raw-ordinal, so deleting the combiner
   creates no asymmetry with Fluent — Fluent never had a typed combiner either.

Corrections logged during the thread, all from the second review: the combiner's call-site count was
~8 rather than 3 (a regex missed multi-line calls); "no `Func<T1,T2,T3,TResult>` anywhere in `src/`"
was literally false because Jaunty.Fluent has `Expression<Func<...>>` selectors, though those are
expression-tree SQL projections rather than row combiners, so the arity argument stood.

## Consequences

- **Breaking for anyone compiling against rc.1** who used the obsolete overloads. Nobody outside
  this repo does.
- Deleted: `ObsoleteMultiEntityTests.cs`, `QueryMultiEntityAsyncLegacyEagerValidationTests.cs`,
  `ObsoleteQueryStreamMultiEntityTests.cs`, and the orphaned private
  `QueryStreamCore`/`QueryStreamCoreIterator`.
- Graph-assembly integration coverage moved to `QueryStream_TwoEntities_BuildsObjectGraph`.
- `QueryStreamEagerValidationTests` and `QueryStreamMultiEntityCommandTypeTests` now exercise the
  supported overload, reached by the AUD-R34-002 implicit conversion — the deletion and that fix
  compose rather than conflict.
- The `ObsoleteAttribute` filter in `CommandOptionsGenericFormDriftTests` and the vestigial `CS0618`
  pragmas across the fallback tests are gone.
- Release notes must carry this as a breaking change, alongside the AUD-R34-002 recompile behaviour
  change (call sites that silently dropped a transaction or timeout start honouring it with no source
  diff).
