# Implementation Plan: Fluent NativeAOT-Safe Metadata Resolution

**Branch**: `003-fluent-nativeaot-metadata`
**Date**: 2026-07-07
**Spec**: [spec.md](spec.md)
**Input**: `docs/specs/003-fluent-nativeaot-metadata/003-spec.md`

---

## Summary

**Primary Requirement**: Fluent queries and fluent CRUD against source-generated entities must
resolve table/column metadata with zero runtime reflection and zero dependency on
`Jaunty.Extensions.Reflection`, while leaving that package's behavior unchanged for
non-source-generated POCOs.

**Technical Approach**: mirror the pattern already proven to work in the non-fluent read path
(`MappedCache<T>` resolving `IMapped<T>.CreateRowMapper`/`ReadEntity` — already exercised
successfully under `PublishAot=true` by the `NativeAOT-Basic` sample). Extend the source
generator to emit additional static, compile-time metadata (table name, schema, PK column
names) on generated entities; add a source-gen-first lookup tier to both
`FluentMetadataCache.GetMetadata<T>()` and `CrudSqlCache.TryResolveMetadata<T>()`, falling back
to `JauntyConfig.ReflectionTableMetadataResolver` only when no source-generated metadata exists.

---

## Technical Context

| Field | Value |
|-------|-------|
| **Language** | C# (netstandard2.0, net8.0) |
| **Dependencies** | Zero (core); `Jaunty.Extensions.Reflection` remains a separate opt-in package |
| **Storage** | N/A (metadata resolution, not query execution) |
| **Testing** | xUnit; NativeAOT publish verification (existing `samples/NativeAOT-*` pattern) |
| **Platform** | Cross-platform (.NET), NativeAOT-compatible core |
| **Project Type** | Library |
| **Performance** | Metadata resolution must remain single-lookup-per-type, cached thereafter (matches existing `FluentMetadataCache`/`MappedCache` behavior) |
| **Constraints** | `netstandard2.0` target rules out C# 11 static abstract interface members as the discovery mechanism — must use the same well-known-static-member-name + reflection-on-first-access pattern `MappedCache<T>` already uses successfully under NativeAOT, or a `[ModuleInitializer]`-based direct-registration pattern (net8.0-only; see Key Design Decisions) |

---

## Constitution Check

- [x] Performance-first upheld — single cached lookup per type, no per-query reflection
- [x] NativeAOT-compatible — closes the gap that currently makes this the *least* AOT-compatible part of the primary API
- [x] Zero dependencies (core) — `Jaunty.Extensions.Reflection` remains separate and untouched
- [x] Tests before implementation — see Phase 1 below; NativeAOT publish check is a first-class test, not an afterthought

---

## Project Structure

### Documentation (this feature)

```
docs/specs/003-fluent-nativeaot-metadata/
├── spec.md          # Feature spec
├── plan.md          # This file
└── tasks.md         # Task list
```

### Source Code

```
src/
├── Jaunty.SourceGenerator/
│   └── JauntyGenerator.cs          # emit TableName/SchemaName/PrimaryKeyColumnNames statics
├── Jaunty/
│   ├── Internals/Entity/
│   │   ├── ColumnMetadata.cs       # resolve Property/delegate question (see Key Design Decisions)
│   │   └── EntityMetadata.cs
│   └── Internals/Write/
│       └── CrudSqlCache.cs         # add source-gen-first lookup tier (TryResolveMetadata<T>)
└── Jaunty.Fluent/
    └── Internals/
        └── FluentMetadataCache.cs  # add source-gen-first lookup tier (GetMetadata<T>)

samples/
└── NativeAOT-FluentQuery/          # new sample proving fluent query + PublishAot, no reflection pkg
    └── NativeAOT-FluentQuery.csproj

tests/
├── Jaunty.SourceGenerator.Tests/
│   └── TableMetadataEmissionTests.cs    # new: verifies emitted statics
├── Jaunty.Fluent.Tests/
│   └── Integration/
│       └── FluentMetadataResolutionTests.cs   # new: fluent query, no UseReflectionMapping()
└── Jaunty.Tests/
    └── Write/
        └── CrudSqlCacheSourceGenTests.cs      # new: fluent CRUD, no UseReflectionMapping()
```

---

## Key Design Decisions

1. **Discovery mechanism — two candidates, pick one during Phase 1**:
   - **(a) Reflection-on-first-access, matching `MappedCache<T>`**: source generator emits
     plain static properties (`TableName`, `SchemaName`, `PrimaryKeyColumnNames`) with
     well-known names; `FluentMetadataCache`/`CrudSqlCache` look them up via
     `typeof(T).GetProperty(...)` once per type and cache the result. Lowest-risk — reuses an
     already-NativeAOT-proven pattern in this codebase verbatim.
   - **(b) `[ModuleInitializer]` direct registration**: source generator additionally emits a
     `[ModuleInitializer]`-attributed method per assembly that calls a registration method
     (e.g. `FluentMetadataCache.Register<T>(EntityMetadata)`) directly at assembly load, with
     zero runtime reflection at all — not even the one-time `GetProperty` lookup (a). More
     architecturally "pure" but `[ModuleInitializer]` is net8.0+ only (not available on
     netstandard2.0 consumers), and introduces a new registration code path to test.
   - **Recommendation**: start with (a) for Phase 1 (matches proven pattern, works on both
     targets); treat (b) as a Phase-3-or-later enhancement if (a)'s per-type reflection lookup
     is ever shown to matter for startup performance. Do not block MVP on (b).

2. **The `ColumnMetadata.Property : PropertyInfo` question — RESOLVED (T002 audit, 2026-07-07)**.

   Audited every consumer of `.Property` on a `ColumnMetadata` instance across the codebase
   (not just the two folders originally scoped — `EntityDataReader.cs` under
   `src/Jaunty/Internals/BulkCopy/` also turned out to be a consumer). Found 3 distinct usage
   shapes, not one:

   - **Category A — name-only lookup (~18 call sites, the large majority)**: every expression
     visitor and builder in `Jaunty.Fluent` (`WhereExpressionVisitor`, `SelectExpressionVisitor`,
     `GroupByExpressionVisitor`, `ExistsExpressionVisitor`, `JoinExpressionVisitor{,3,4}`,
     `InsertBuilder`, `JoinClauseBuilder`, `QueryBuilder`, `GroupedQueryBuilder`,
     `JoinedQueryBuilder{,3,4}`, `JoinedQueryBuilderOrderBy`, `SetOperationBuilder`,
     `QueryBuilderBase`, `FluentMetadataCache`) only ever does `.Property.Name == propertyName`
     — resolving a `MemberExpression`'s member name back to its `ColumnMetadata`. None of these
     call `GetValue`/`SetValue`/`PropertyType`. This category needs a `string`, never a
     `PropertyInfo`.
   - **Category B — actual value read/write (4 call sites)**: `src/Jaunty/Write/Upsert.cs`
     (`col.Property.GetValue(entity)`, per-parameter, **uncached** — a pre-existing perf gap,
     unrelated to this spec, not fixed here) and `src/Jaunty.Fluent/Builders/Insert/InsertBuilder.cs`
     (same uncached pattern) both call `PropertyInfo.GetValue` directly inline, every execution.
     `src/Jaunty.Fluent/Builders/Join/JoinedQueryBuilder.cs` additionally calls
     `col.Property.PropertyType` + `col.Property.SetValue(entity, convertedValue)` when
     materializing joined-entity values. These 3 files are Fluent's own value-binding paths,
     structurally separate from core `Jaunty`'s cached path below.
   - **Category C — cached compiled-getter construction (2 call sites, already the "good"
     pattern)**: `src/Jaunty/Internals/Write/WriteParameterCache.cs` and `MultiRowInsertCache.cs`
     (core `Jaunty`, the non-fluent CRUD path) pass `columns[i].Property` into a helper that
     builds `Expression.Property(param, prop)` once, compiles it via `.Compile()`, and **caches**
     the resulting delegate — never re-reflecting per row. **Empirically verified NativeAOT-safe**
     during this audit: a standalone scratch project doing the identical
     `PropertyInfo → Expression.Property → Expression.Lambda.Compile()` pattern was published
     with `PublishAot=true` (win-x64) and ran correctly with no AOT warnings — .NET's
     `Expression.Compile()` falls back to an interpreter under NativeAOT rather than throwing.
     This was checked directly rather than assumed, since it would have materially changed this
     spec's scope if it had failed. (Note: `WriteParameterCache`/`MultiRowInsertCache` are
     currently reachable ONLY via the reflection-resolved path today, since `CrudSqlCache`
     requires non-null metadata from `ReflectionTableMetadataResolver` — see `spec.md`'s FR-003.
     Once T003/T007 land, they become reachable for source-gen-metadata'd entities too, so they
     must also be updated to tolerate a null `Property`, same as Category B.)
   - **Category D — type-only**: `src/Jaunty/Internals/BulkCopy/EntityDataReader.cs` reads
     `.Property.PropertyType` to answer `IDataReader.GetFieldType(i)` for bulk-copy. Needs a
     `Type`, never full reflective capability.

   **Decision: option (a) from `spec.md`'s Design Note #2, refined with the above categories.**
   - `ColumnMetadata.Property` becomes `PropertyInfo?` (nullable) — populated only by
     `MetadataBuilder.Build<T>()` (the reflection path), unchanged from today.
   - `ColumnMetadata` gains `string PropertyName` (always populated by both paths) — Category A's
     ~18 call sites migrate from `.Property.Name` to `.PropertyName` directly, dropping their
     `PropertyInfo` dependency entirely regardless of which metadata source populated the entry.
   - `ColumnMetadata` gains `Type PropertyType` (always populated) — Category D's one call site
     migrates to `.PropertyType` directly.
   - `ColumnMetadata` gains `Func<object, object?>? Getter` / `Action<object, object?>? Setter`
     — populated by the source-gen path with **real emitted C# lambdas closing over the
     generated entity's own properties** (not `Expression.Compile()` — the generator can just
     emit literal `entity => (object?)((T)entity).Prop` source text, compile-time-checked,
     faster than the interpreted-Expression path and requiring zero `System.Linq.Expressions`
     involvement at all). Left `null` by the reflection path for now (Category B/C's existing
     `PropertyInfo`-based code keeps working unchanged for reflection-resolved entities; wiring
     the reflection path to also populate `Getter`/`Setter` as a unification/perf follow-up is
     out of scope for this spec — noted as a nice-to-have, not required for MVP).
   - Category B/C consumers (`Upsert.cs`, `InsertBuilder.cs`, `JoinedQueryBuilder.cs`,
     `WriteParameterCache.cs`, `MultiRowInsertCache.cs`) are updated to prefer `Getter`/`Setter`
     when present, falling back to `Property!.GetValue`/`SetValue` otherwise — the exact same
     fallback-tier shape already used for metadata *resolution* itself (Key Design Decision 1),
     so the spec tells one consistent story throughout: source-gen first, reflection fallback,
     at every layer.
   - This resolves the option (a) vs (b) question from `spec.md`: it's effectively "(a), but
     `PropertyName`/`PropertyType` are hoisted out to always-populated fields since the audit
     showed they're overwhelmingly what's actually needed" — a smaller, safer diff than either
     original option contemplated in the abstract, because the audit happened before committing
     to a shape.

3. **Older-generator-version edge case** (spec.md Edge Cases): if `IMapped<T>` exists but
   lacks the new static members (stale generated code), the reflection-on-first-access lookup
   in decision (1a) simply returns `null`/not-found and falls through to the
   `ReflectionTableMetadataResolver` tier exactly as it does today for any other
   non-source-gen type — no special-casing needed, this falls out of the lookup-order design
   for free. No compile-time enforcement is added (would require a generator version check,
   out of scope).

4. **Backward compatibility (User Story 3)**: `JauntyConfig.ReflectionTableMetadataResolver`
   and `Jaunty.Extensions.Reflection` are not touched by this feature at all — the new lookup
   tier is strictly additive and runs *before* the existing one, never replacing it.

---

## Implementation Phases

### Phase 1: Foundational (Blocks All User Stories)
- **T001**: Source generator emits `TableName` (string), `SchemaName` (string?),
  `PrimaryKeyColumnNames` (string[]) as public static properties on generated entities,
  alongside existing `ColumnInfo` collections.
- **T002**: Audit every `ColumnMetadata.Property` consumer (see Key Design Decision 2); write
  the decision (2a vs 2b) into this plan before proceeding.
- **T003**: Implement the chosen `ColumnMetadata` change from T002.

### Phase 2: MVP User Stories 1–3 (Fluent read, fluent CRUD, no regression)
- Add source-gen-first lookup tier to `FluentMetadataCache.GetMetadata<T>()` (User Story 1).
- Add identical tier to `CrudSqlCache.TryResolveMetadata<T>()` (User Story 2), closing the
  existing acknowledging code comment there.
- Full existing `Jaunty.Fluent.Tests`/`Jaunty.Tests` suites re-run unmodified to confirm User
  Story 3 (no regression) across all 4 real dialects.

### Phase 3: User Story 4 (Actionable failure) + NativeAOT proof
- Improve the no-metadata-found exception message to name both remedies.
- New `samples/NativeAOT-FluentQuery/` sample: fluent query (not just `Query<T>`) compiling
  and running under `PublishAot=true`, no `Jaunty.Extensions.Reflection` reference.
- Update `samples/torture-test-sakila-queries/` to drop `Jaunty.Extensions.Reflection` and
  `UseReflectionMapping()`, re-run all 15 queries against all 5 dialects (SC-4).

### Phase 4: Verification & Polish
- `dotnet publish -p:PublishAot=true` on the new sample, confirm no reflection-related AOT
  warnings.
- XML docs on any new public surface.
- Update `docs/jaunty-torture-test-gaps-log.md` gap #11 status to "fixed" with a pointer to
  this spec/branch.

---

**Next**: `tasks.md` — full task breakdown.
