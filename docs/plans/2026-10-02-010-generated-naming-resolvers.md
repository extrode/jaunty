# Naming resolvers on the source-generated path

## Context

`JauntyConfig.TableNameResolver`, `SchemaNameResolver` and `ColumnNameResolver` only work for
reflection-mapped entities. A source-generated entity uses the names fixed at build time, so the
resolvers are ignored for:
- CRUD SQL,
- Fluent SQL,
- generated binders (the `"@Id"` literals),
- generated readers (the `GetOrdinal("Id")` literals).

Meanwhile non-strict `QueryPartial*` reads of the same entity go through reflection and *do*
apply the column resolver. One entity can end up with two different column names.

The docs present the resolver tier as universal, and the existing column flowchart
(`metadata-system-spec.md:154`) is imprecise: it says `[Column]` wins, but an empty `[Column("")]`
falls through. The owner wants the generated path to follow the reflection order, documented with
mermaid diagrams.

**The order, which both paths will follow after this change:**

| Name | 1st: attribute, if non-empty | 2nd: resolver, if set and its result is non-null | 3rd: default |
|---|---|---|---|
| Table | Jaunty `[Table(name)]`, else DataAnnotations `[Table(name)]` | `TableNameResolver(type)` | C# type name |
| Schema | `[Table(.., schema)]` / `Schema =` (both attributes) | `SchemaNameResolver(type)` | none (null) |
| Column | Jaunty `[Column(name)]`, else DataAnnotations `[Column(name)]` | `ColumnNameResolver(propertyName)` | C# property name |

A resolver result of `""` is used verbatim, as reflection does today, and is not treated as a fallback.

## Approach

### 1. One precedence function in core
- New `internal static class NameResolution` in `src/Extrode.Jaunty/Internals/Entity/`:
  - `Table(Type, string? explicitName, string defaultName)`
  - `Schema(Type, string? explicitSchema)`
  - `Column(string? explicitName, string propertyName)`
  
  Each implements the table above. A resolver is invoked only when no attribute name applies.
- `MetadataBuilder.Build<T>()` (`src/Extrode.Jaunty.Extensions.Reflection/Internals/MetadataBuilder.cs:40-115`)
  keeps its attribute reading but calls `NameResolution` (InternalsVisibleTo already covers
  Reflection).
- Small behaviour change: reflection no longer calls a resolver for a name an attribute already
  fixes. This only matters to a resolver with side effects or one that throws. CHANGELOG notes it.

### 2. A per-generation name cache for generated code (public bridge, like `GeneratedBindingSupport`)
- New `src/Extrode.Jaunty/Core/GeneratedNames.cs`:
  - `[EditorBrowsable(Never)] public sealed class GeneratedNameCache<TState>`.
  - Ctor: `(Type entity, string defaultTable, string? explicitTable, string? explicitSchema, string[] propertyNames, string?[] explicitColumns, Func<ResolvedNames, TState> build)`.
  - `TState Current`: reads `ConfigurationGeneration.Current` first, then returns the cached state
    if the generation matches; otherwise it resolves through `NameResolution`, calls `build`, and
    publishes. A race builds twice but stays correct, which is the existing pattern.
  - `ResolvedNames` holds `TableName`, `SchemaName`, `ColumnNames[]`, and `ParameterNames[]`
    (`"@" + column`, matching `CrudSqlCache.cs:229-246`).
- No resolver set means everything is built once, then each access costs one int compare.
  The code is AOT-safe: `typeof(T)` only, no reflection.

### 3. Generator emits resolved-name lookups (`src/Extrode.Jaunty.SourceGenerator/JauntyGenerator.cs`)
- **Model:**
  - `PropertyMetadata` keeps `ColumnName` (the build-time default) and gains `ExplicitColumnName`
    (null when there is no non-empty attribute).
  - `GetTableNameAndSchema` (~1894) also returns whether the table name was explicit.
- **New nested `private sealed class __JauntyNamed`:** its constructor takes `ResolvedNames` and
  builds the name-dependent members:
  - `PrimaryKeyColumnNames`, `InsertColumns`, `UpdateColumns`, `DeleteColumns` and `EntityColumns`,
    using resolved names by property index.
  - `ParameterMap`, filled first-wins, keeping the current duplicate behaviour. Runtime duplicates
    are still rejected by `EntityMetadata.ThrowIfDuplicateColumnNames`, the same as reflection.
  
  Field: `private static readonly GeneratedNameCache<__JauntyNamed> __jauntyNames`.
- **Public statics forward to the cache, so they agree with the SQL:**
  - `TableName`, `SchemaName`, `PrimaryKeyColumnNames`, `*Columns`, `ParameterMap` and
    `EntityColumns` become `=> __jauntyNames.Current.X`.
  - `IEntityMetadataSource` explicit members are unchanged, since they forward to the statics.
- **Readers:**
  - `OrdinalMap.Resolve` uses `reader.GetOrdinal(names[i])`.
  - `CacheEntry.Matches` compares against the current names, so a resolver change while a reader is
    live re-resolves instead of reusing stale ordinals.
- **Binders:** `BindInsert/Update/Delete` take `ParameterNames[i]` from one `Current` read per call.
- The build-time duplicate-column diagnostic stays, but checks attribute and default names only.
  A resolver-made duplicate is caught at runtime, as on reflection.
- Core consumers need no change. `SourceGeneratedMetadataResolver.TryBuild` reads the interface,
  and `CrudSqlCache`, `WriteParameterCache` and `FluentMetadataCache` already rebuild per generation.

### 4. Docs (mermaid, rendered by docsgen into `dist/docs-site/`)
- `docs/02-architecture/metadata-system-spec.md`:
  - Replace the column-only flowchart with a "Name resolution" section, using the 3-tier table above.
  - Add one flowchart per name kind (table, schema, column), showing the non-empty checks, the
    Jaunty-then-DataAnnotations order, and the null result from the resolver going to the default.
  - Add a sequence diagram for the generated path: a call reads `GeneratedNameCache.Current`, the
    generation compare, a rebuild through `NameResolution`, then reader/binder/SQL use the same names.
  - Add a worked example with expected names for both paths.
- `docs/01-api-reference/attributes.md` (32, 91, 275-277, 324-338, 406) and `configuration.md`
  (50-84, 180-186, 345-347, 433): remove the "generated path ignores resolvers" claims and caveats,
  state the order once, and link the diagram.
- Also check `api-summary.md`, `schemas.md` and `crud-operations.md` for the same claim.
- `JauntyConfig.cs:90-120` XML docs: state the order and that it applies to both mapping modes.
- `CHANGELOG.md`: describe the user-facing change. Generated entities now honour resolvers; with no
  resolver set, nothing changes.
- Rebuild the docs site: `scripts/build-docs.sh`.

### Out of scope (one line each in `work/todo.md`)
- FlatFiles has its own `TableNameResolver`, which defaults to the lowercased class name and ignores
  all three JauntyConfig resolvers. It is a third path with different defaults, and changing it
  would change existing file-to-table names.

## Tests (each break-verified)
- **Core `NameResolution` truth table** (UnitTests):
  - non-empty attribute wins
  - `[Column("")]` / `[Table("")]` fall through
  - resolver returning null gives the default
  - resolver returning `""` is used verbatim
  - resolver not called when the attribute applies
  - schema from the constructor argument vs the `Schema =` named argument
- **`GeneratedNameCache`:**
  - builds once per generation
  - rebuilds after a resolver set
  - no rebuild without a bump
- **Generator:** re-approve every snapshot under `tests/Extrode.Jaunty.SourceGenerator.Tests/Snapshots/`
  (all ~50 change shape). Add a snapshot entity mixing explicit and defaulted table, schema and
  columns.
- **Runtime, generated entity on SQLite** (SourceGenerator.Tests), with snake_case resolvers:
  - Insert, Get, Update, Delete round-trip
  - strict read via the generated mapper
  - Fluent `Where` on a defaulted column
  - attribute-named column unaffected
  - changing the resolver between calls switches the names
  - `Entity.TableName` static agrees with the SQL
- **Parity:** one truth table of entity shapes and expected names, asserted against
  `MetadataBuilder.Build<T>` in Jaunty.Tests and against generated `IEntityMetadataSource` in
  SourceGenerator.Tests.
- Existing resolver tests in Jaunty.Tests (`ConfigResolverTests`, `ConfigurationTests`,
  `MetadataBuilderShadowedPropertyTests`) must still pass.

## Verification
- `dotnet test` across SourceGenerator, Fluent.SourceGen, Scaffolding, Jaunty.Tests, UnitTests and
  Fluent, on net8 and net10. Then the full `dotnet test Jaunty.slnx` before merge.
- `scripts/verify-nativeaot.ps1`: no new reflection.
- `laws-check --run`.
- Docs build renders the new diagrams.
- Review: a `review` worker on the diff, findings verified, all fixed.
- Branch `feat/generated-naming-resolvers` off dev, then merge `--no-ff` and run the structure audit.

## Order of work
1. Finish the current branch `fix/fable-review-r38-survivors` (Jaunty.Tests still running; the rest
   is green), then merge.
2. Core: `NameResolution`, `GeneratedNameCache`, and tests. Switch `MetadataBuilder` over.
3. Generator emission and snapshots, then runtime and parity tests.
4. Docs, diagrams, CHANGELOG, docs build.
5. Review, fix, merge.
