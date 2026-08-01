# Changelog

All notable changes to Jaunty are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and versioning follows [SemVer 2.0](https://semver.org). Package versions are
set at release time from the git tag (`vMAJOR.MINOR.PATCH`); the local/dev
default lives in `src/Directory.Build.props`.

## [Unreleased]

### .NET 10 migration (spec 010, 2026-07-30)

Every project now targets `net8.0` + `net10.0` (ns2.0/net472 support unchanged), with 21
project-reference pins duplicated per TFM and a loader assertion that fails any leg that
silently loads the wrong build. The first clean net10 builds exposed six latent defects green
CI had hidden — a dropped package group (CS0234), a non-generic `Expression.Lambda` (IL3050),
trim-analyzer strictness (IL2060/75), and IL2070/IL2075/IL2057 in FlatFiles and Scaffolding —
four fixed properly, two recorded as reflection-by-design scope exclusions. CI gained net10
test legs and a dual net8/net10 NativeAOT publish; all four AOT samples publish and run with
byte-identical stdout on both TFMs. Benchmarks: net10 is a median 2–3% faster with no
regression; the flagship 1-row query narrowed from 1.51x to 1.19x of Dapper.

Twenty-six audit rounds since `v1.0.0-rc.1`, plus the Dapper-parity and torture-test
work. The suite runs 12,207 tests green across `net8.0` and `net472` with all four
server dialects configured.

### Security

Every item here was reachable from caller-supplied input.

- **Identifier-escaping bypass in generated DML/DDL.** Table and column names reached
  the generated statement unescaped on several paths.
- **`LIKE` wildcard escaping** in `Contains`/`StartsWith`/`EndsWith` — `%` and `_` in a
  caller's value were treated as wildcards.
- **Embedded quotes** unescaped in `ExpressionTranslator` and `ImportExecutor`
  identifiers, and in `CsvFileSource`'s `Delimiter`/`QuoteChar` SQL.
- **Alias injection and parameter-name collisions** in joined queries; interpolated
  parameter placeholder names are now sanitised.
- **Caller-supplied column names** are validated and escaped in `SelectPartial*` and
  `ToSql`.
- **Import paths hardened:** `CsvImport` identifier escaping; the sqlite3 CLI import
  path validates `dbPath` and dialect-escapes `tableName`; `ImportExecutor` quotes its
  target per dialect.
- **Connection strings leaked through interceptors** — the masking prefix gap exposed
  passwords to interceptor output.
- **Format-string injection** on a logging path.

### Added

- **Fluent API fanout (2026-08-02)** — three long-standing asymmetries between sibling
  interfaces closed together, all additive:
  `IUpdateWhereClause<T>` gains the `And`/`Or` × `Between`/`Exists`/`InSubquery` families
  (and their `Not` forms) that `IWhereClause<T>` already had;
  the 3-way and 4-way `IJoinClause` gain the predicate-expression `On(...)` overload the
  2-way interface had, with join values renumbered against the query-wide parameter
  sequence so a third or fourth join cannot re-mint a name the query already bound;
  `IGroupedJoinedQuery{,3,4}` gain the `Select`/`SelectAsync` `CommandOptions` overloads,
  so a grouped joined query can finally take a caller's transaction or a timeout.
- **`GROUP BY` on joined queries** — 2-way, 3-way and 4-way, via the new
  `JoinedGroupByExpressionVisitor`. Aggregate and grouping columns are qualified with
  the table alias, and compound `HAVING` works across joins.
- **Async multi-entity query parity for arities 3–7**, matching the sync surface.
- **`CommandOptions` and transaction overloads** across the terminal read methods, bulk
  `Delete`/`Update`, the join-builder family and `QueryPartialList`. Adds
  `MultiEntityCommandOptions<T1,T2>` and the missing `QueryStreamAsync<T1,T2>`.
- **`On<TValue>` parameterised raw-condition overload** on 3- and 4-way joins, and raw
  join conditions can now name their parameter.
- **Source-gen-first metadata tier.** `CrudSqlCache` and the fluent resolver consult
  generated metadata before falling back to reflection. The generator emits
  `TableName`, `SchemaName` and `PrimaryKeyColumnNames` statics, and the new
  `IEntityMetadataSource` contract replaces reflection over that generated metadata.
  Internally `ColumnMetadata` gained `PropertyName`/`PropertyType`/`Getter`/`Setter` so
  a column no longer requires a `PropertyInfo` (`Property` is now nullable) — it is
  `internal`, so this changes no public surface.
- **Dual licensing:** ISL-EULA for binaries, ISL-R for source.
- **Span-based `array.Contains()`** is recognised for `IN`-clause detection, and `Sql`
  function comparisons to null translate to `IS NULL`/`IS NOT NULL`.
- **Reference ports** preserved as samples: Conduit/RealWorld and eShopOnWeb migrated
  from EF Core, 15 canonical Sakila queries via the fluent API, a NativeAOT sample, and
  4-dialect docker-compose infrastructure with seed scripts.

### Changed

- **CSV import rejects ragged rows** instead of silently dropping the extra fields.
- **`ParameterBinder` throws** instead of silently dropping scalar parameters bound to
  stored procedures.
- **`SpParameters` overloads treat null as empty** instead of throwing.
- **`OrderBy`/`Take`/`Skip` are rejected** on set-operation operands and on the outer
  chain, where they were silently ignored.
- **`HAVING` rejects closure-captured variables and parameters** rather than emitting
  SQL that could not bind.
- **docker-compose host ports remapped** so the test stack cannot collide with a locally
  installed server: PostgreSQL `5432` → **`5433`**, MySQL `3306` → **`3308`**.

### Removed

- `MultiEntityCommandOptions.Mapper1..MapperN` — per-position custom mapper fields that
  were never wired to anything.
- `WriteBackOptions`, which nothing constructed or consumed.

### Fixed

**Test suites that never ran (2026-08-01)**

- `tests/Jaunty.Fluent.SourceGen.Tests` was absent from `Jaunty.slnx` from the day spec 003
  created it, so solution-wide builds and tests skipped it silently. Now listed.
- Neither `Jaunty.Fluent.SourceGen.Tests` (17 tests) nor `Jaunty.Scaffolding.Cli.Tests`
  (34) had a CI step, and `release.yml`'s name-based filter matched neither
  (`~Jaunty.Fluent.Tests` does not match `Jaunty.Fluent.SourceGen.Tests`, and
  `~Jaunty.Scaffolding.Tests` does not match the CLI suite). Both now run in CI on
  `net8.0` and `net10.0` and in the release gate.
- `SolutionLayoutTests` asserts every `tests/**/*.csproj` on disk appears in `Jaunty.slnx`,
  so the next omission fails a test instead of disappearing.

**Transactions and connection lifetime**

- `AsyncTransactionValidator` wired into 8 files: async paths silently dropped a
  transaction that was not a `DbTransaction`. Sync `command.Transaction` assignments
  guarded across 12 more.
- Fluent `QueryBuilder.ExecuteNonQueryAsync` silently dropped a non-`DbTransaction`.
- `ExecuteQueryMultiple(Async)` leaked its command and connection, and mis-set
  `CommandType` and the transaction.
- Sync bulk-write rollback no longer masks the original exception.
- `GridReader` row loops are wrapped in `try`/`finally` around `Advance()`, and dispose
  asynchronously in the internal-disposal callback overloads.

**Mapping and metadata**

- `EntityDataReaderCache` cached layout-blind, so a reader with a different column
  layout reused the wrong plan. The arity-2 `MultiEntityMapper` is now keyed by reader
  schema; `BuildSchemaKey` was missing its separator, and the layout key components are
  separated with `0x1F`.
- Nullable `DateTime`/`Guid` properties made the generator emit uncompilable code, as
  did three further entity shapes.
- Get-only and init-only properties are excluded; indexer properties are skipped; the
  parameter-limit undercount is corrected.
- Flat-file mapping ignored `[Ignore]` and `[NotMapped]`.
- The read path baked in `DefaultEnumStorage` while the write path re-checked it.

**Dialects**

- PostgreSQL: `bit`/`bit varying`/`varbit` collapsed to `bool`; dollar-quote misparsing
  and prefix-detection gaps; `InsertBuilder` identity SQL hardcoded an `'id'` column.
- SQL Server: `Upsert` MERGE identity-PK bug; `OFFSET`/`FETCH` emitted without
  `ORDER BY`; the schema reader crashed marking PK columns; user-defined alias types now
  resolve to their base type; `SqlServerImportDialect` bracket-quotes identifiers.
- SQLite: composite PKs reported declaration order rather than column order;
  case-insensitive `LIKE` could not pair with its own patterns; import `Skip` diverged
  from the sibling dialects.
- `AND`/`OR` `WHERE`-precedence bug, and `CachedDialectMetadata` double-escaped column
  names that were already escaped — which broke keyword-named columns.

**Bulk copy and import**

- `BulkCopyOptions.EnableStreaming` was documented but never wired.
- `SqlServerBulkCopyProvider` returned `-1` instead of the actual `RowsCopied`, did not
  validate the connection type, and had a null-type guard missing in
  `MapBulkCopyOptions`. `EntityMetadata.SchemaName` is now passed through
  `IBulkCopyProvider`.
- CSV import threw `NotSupportedException` for every engine after `UseNativeBulkCopy()`.
- `CountCsvRows` honoured neither `CsvImportOptions.Encoding` nor RFC 4180 quoted
  newlines; `HasHeader=false` was broken; the keyless-entity Skip/Upsert conflict
  strategy was unguarded.
- SQL Server `BULK INSERT` now sniffs the CSV's line endings. Under `FORMAT = 'CSV'`,
  SQL Server expands the `'\n'` `ROWTERMINATOR` escape to `\r\n`, so an LF-only file
  failed outright with `IID_IColumnsInfo`; `0x0a` fixes that but leaves a trailing CR on
  a CRLF file.
- `BulkInsert` undercounted when an id was `> 0`.

**Interceptors and logging**

- `Insert`/`Update`/`Delete`/`GetById` bypassed the interceptor pipeline entirely.
- `SpParameters` stored-procedure overloads were invisible to interceptors and the
  logger, as were `QueryStreamMultiEntityCore`/`Fast` for arities 2–7, which also failed
  to set `CommandType`.
- Diagnostics never fired without a registered interceptor;
  `LoggingConfiguration.LogExecutionTime` was a no-op; a premature `MinimumLogLevel`
  gate blocked slow-query warnings.
- Interceptor TOCTOU race, and `JauntyConfig.InterceptorPipeline` is restored around
  `Reset()`.

**Culture**

- Six `Convert.ChangeType` sites parsed numbers under the host's locale rather than
  invariantly. Scalar conversion is culture-invariant, `GROUP BY` constants are
  culture-safe, and joined-query `HAVING` literals are parameterised rather than
  formatted into the SQL.

**Scaffolding and source generator**

- Scaffold writes are atomic; `ScaffoldAsync` propagates cancellation; the LOB
  `max_length` sentinel is handled.
- Scaffolded output composes with the source generator instead of colliding with it.
- Hint-name collisions, string-literal escaping and a `GetValue`-fallback cast.
- Singularisation only alternates `f`/`fe` for the closed `-ves` class.
- Computed-column binding and semantic attribute matching.

**Other**

- Swallowed exceptions causing data corruption, dialect state discarded, a `Guid`
  conversion gap, silent type-guard returns, dispose-on-throw, sync-over-async, and
  chunking defects, all found by audit.
- FlatFile registry thread safety, SQL null checks, and an `.xls` write mismatch.
- `TsvFileSource.GenerateCopyToOptions()` honours `NullString`;
  `CsvFileSource.GenerateCopyToOptions` honours `Delimiter`/`QuoteChar`/`NullString`.

### Performance

- **The source generator had no incremental caching at all** — every keystroke re-ran
  the full pipeline.
- `ResultMapperPlan` looked up column ordinals once per row; `GridReader.Read`/
  `ReadStream` resolved the mapper before reading rather than after.
- `EvaluateExpression` compiled an expression tree for every closure-captured value.
- `TypeHandlerRegistry` resolution moved from per-read to static initialisation.
- Expression visitors bypassed `CachedDialectMetadata`.
- Reflection cached for anonymous-object parameter and value binding, for the seven
  `UseReflectionMapping` resolvers, and for `Write`/`WriteAsync` `MethodInfo` in
  `PostgreSqlBulkCopyProvider`.
- `ExecuteBatch` uses `Prepare()`.

### NativeAOT

- Trim-safety attributes added to `CsvImport`'s Npgsql reflection probe.

## [1.0.0-rc.1] - 2026-07-05

### Changed (2026-07-04 enterprise readiness pass)

- **Versioning switched from CalVer to SemVer.** Dev baseline is `1.0.0-rc.1`;
  the first GA tag should be `v1.0.0`.
- **BREAKING:** `Beparey.Jaunty` no longer depends on the full
  `Microsoft.Extensions.DependencyInjection` container package - only
  `.Abstractions`. `ApplyJauntyInterceptors(IServiceCollection)` was removed
  (it built a throwaway provider - ASP0000); use
  `ApplyJauntyInterceptors(IServiceProvider)` after `Build()`.
- All library awaits now use `ConfigureAwait(false)` (CA2007 enforced as error
  for `src/`).
- Scaffolding CLI migrated from `System.CommandLine` 2.0.0-beta4 to 2.0.9 GA.
- Microsoft.Extensions.* netstandard2.0 pins bumped from EOL 6.0.0 to 8.0.x;
  High-severity transitive vulnerabilities remediated
  (SQLitePCLRaw native sqlite GHSA-2m69-gcr7-jv3q, Regex GHSA-cmhx-cq75-c4mj).
- Package validation (`EnablePackageValidation`) enabled; benchmarks/samples/
  source generator excluded from packing.
- **BREAKING (netstandard2.0 only):** streaming APIs unified across TFMs.
  `QueryStreamAsync`, `QueryPartialStreamAsync`, `QueryPartialUnbufferedAsync`,
  and `GetAllStreamAsync` now return `IAsyncEnumerable<T>` on netstandard2.0
  (via `Microsoft.Bcl.AsyncInterfaces`) exactly as on net8.0; the
  `ValueTask<IEnumerable<T>>` variants were removed. `EntityMetadata.ParameterMap`
  is now `IReadOnlyDictionary<string, ColumnMetadata>` on both TFMs. The
  ApiCompat suppression baseline is gone - the public API is identical across
  TFMs and validation now fails on any new divergence.

### Added

- `ISyncCommandInterceptor`: synchronous interceptor hooks. The sync APIs
  (`Query<T>`, `Execute`, ...) no longer block a thread on the async interceptor
  pipeline when interceptors implement it; built-in `LoggingInterceptor` and
  `AuditInterceptor` do. Async-only interceptors still work on the sync path
  (invoked with a blocking wait, as before).

- **BREAKING:** internals hygiene before GA. `EntityMetadata` and
  `ColumnMetadata` are now `internal` (use `JauntyConfig` resolvers, which are
  `object`-typed by design). `MappingMode` and `IBulkCopyProvider` moved from
  `Jaunty.Internals.*` namespaces to `Jaunty.Configuration` (they are real
  public contracts - config resolvers and custom dialects). No public type
  remains under a `Jaunty.Internals` namespace.

- Test infrastructure: all test projects now on xunit.v3 3.2.2 (xunit 2.9.3 was
  flagged deprecated/legacy). Custom dialect data attributes migrated to the v3
  `DataAttribute` API; unavailable providers now surface as skipped rows.

- Release pipeline publishes to GitHub Packages (private feed) using the
  built-in `GITHUB_TOKEN`; the `NUGET_API_KEY` secret is no longer required.
- Source generator: per-result-set `CreateRowMapper` + direct typed getters;
  10k-row SQLite reads went from 1.80x to 1.03x vs hand-coded ADO.NET
  (Dapper parity). See docs/05-quality/reports/BENCHMARKS-2026-07-04.md.

### Fixed

- **Generated mapper NULL handling (correctness):** the source-generated
  `DbDataReader` fast path mapped SQL `NULL` to `0`/`false`/default instead of
  `null` for nullable value type properties (`decimal?`, `int?`, `bool?`, ...).
  The generated ternary now uses a property-typed `default`. (PRD-001)
- **Packaging:** `Beparey.Jaunty` now ships `Jaunty.SourceGenerator.dll` under
  `analyzers/dotnet/cs`. Previously the published package contained no
  analyzer, so `IMapped<T>` mappers were never generated for package consumers.
  (PRD-013)
- **Release build:** the solution no longer excludes the source generator from
  Release configuration; `dotnet build Jaunty.slnx -c Release` succeeds from a
  clean clone. (PRD-012)
- `JoinedQueryBuilder.SelectPartialFirstAsync` (non-generic) now throws
  `InvalidOperationException` on an empty result, per the `IJoinedQuery`
  contract, instead of returning `null`. (PRD-016)

### Added

- `tests/Jaunty.SourceGenerator.Tests`: runtime shape-safety validation of the
  actual generator output against SQLite (reordered columns, result-set
  changes, interleaved readers, narrowed shapes). (PRD-001)
- NuGet metadata for all packages: SourceLink, symbol packages (snupkg),
  license-acceptance flag, repository info, XML documentation. (PRD-013)
- `TreatWarningsAsErrors` for all `src/` projects; the codebase builds with
  zero warnings. (PRD-016)

### Changed

- `JauntyConfig.Logger` is now `Action<string, object?>` (parameter payload
  may be null). Source-compatible for typical lambda subscribers.

## [2026.01.01] - baseline

Initial internal version. Core query/CRUD/bulk APIs, fluent builder,
scaffolding, FlatFiles/DuckDB providers, NativeAOT source generation,
logging and command interception.
