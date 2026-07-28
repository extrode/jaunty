# Constitution — jaunty

Binding project rules. These are the WON'T-change decisions. Contributors obey them;
changing one is a deliberate governance act, not a casual edit. This is NOT lessons-learned
(see docs/lessons/) and NOT mechanical conventions (see docs/conventions.md).

Status: active · Last reviewed: 2026-07-29
Content grafted from `docs/constitution.md` (version 2026-03) when the repo moved

## Stack constraints
- **Zero dependencies in core.** The `Jaunty` package has none. Extensions ship separately.
  Nothing is added to core.
- **NativeAOT-compatible.** No runtime reflection in core hot paths. `[DynamicallyAccessedMembers]`
  where needed. Multi-targets `netstandard2.0` + `net8.0`.
- **ADO.NET only.** All database integration goes through `IDbConnection` and `DbDataReader`.
  Dialects are implementations of `ISqlDialect`, never special cases in the core.

## Architecture invariants
- **Performance-first.** Zero unnecessary allocations in hot paths, no LINQ in tight loops,
  aggressive caching of expressions, delegates and metadata. Benchmark before and after.
- **Strict mapping by default.** Silent partial mapping is a bug: `Query<T>` requires every
  property mapped. Intentional partial shapes use `QueryPartial<T>`.
- `ConfigureAwait(false)` everywhere. Pre-size collections. `ArgumentNullException.ThrowIfNull`
  for validation. XML docs on every public API.
- Source layout, with `tests/` mirroring it:

```
src/
  Jaunty/                    # Core (netstandard2.0, net8.0)
  Jaunty.Fluent/             # Fluent API
  Jaunty.FlatFiles/          # Flat file interfaces
  Jaunty.FlatFiles.DuckDB/   # DuckDB implementation (net8.0+)
docs/specs/                  # Specifications, NNN-slug/NNN-spec.md
```

## Security / data
- None recorded. jaunty is a library with no network surface, no credential handling and no
  data at rest of its own; connection strings belong to the consuming application.

## Testing
- **Tests before implementation.** 90%+ coverage target.
- Integration tests run against real databases, not fakes.
- Performance benchmarks accompany any change to a critical path.

## Governance
- Supersedes ad-hoc practices. To change a rule: record the change + date + why, right here.
- **Spec-first.** No code without a spec and a plan under `docs/specs/NNN-slug/`. Each
  milestone carries entry and exit criteria; acceptance criteria are testable assertions.
- One commit per completed task, `type(component): T### — description`, including the test
  count. PRs verify compliance; a deviation is documented in the PR or it did not happen.
