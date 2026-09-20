# Coverage Gap Inventory — 2026-09-20

Refresh of the 2026-08-27 baseline in [`code-coverage.md`](../code-coverage.md), extended down to
file and method level. Produced by `scripts/coverage.ps1` (all 10 suites under `tests/`, no
`-Suite` filter), union-merged per `(assembly, file, method, line)` from the 10 cobertura reports.
Every uncovered method was then read against source and tests by seven independent `review-deep`
passes (one per assembly, `Extrode.Jaunty` split into 4 sub-batches for turn-budget reasons) and
classified into one of six reasons — see "How this was produced" at the end.

**Status update 2026-09-20 (post-review):** an independent `review-deep` pass verified this report
before the quick-wins batch was implemented. It confirmed bug #1 as real (now fixed) and bug #2 as
a false positive (no fix needed), and corrected two sub-claims — the HAVING `=` operator and the
`IsWithoutRowId` comment-handling item, both marked inline below. All 13 items below — the full
quick-wins batch plus the deferred bulk async `RollbackAsync` fixture, built afterward — are now
fixed and merged to `dev`; see the "FIXED" annotations throughout. `ExecuteReaderDirect` was
deleted as confirmed dead code rather than tested.

**Status update 2026-09-20 (assembly-by-assembly close-out):** after the quick-wins batch, every
remaining highest-value item in the `Extrode.Jaunty` (core) and `Extrode.Jaunty.Fluent` sections is
now also fixed, each verified by an independent `review-deep` pass before moving to the next
assembly. Core's closed-connection/`CommandTimeout`/`CommandType.StoredProcedure` sweep surfaced a
real production bug (`Upsert`/`UpsertAsync` silently dropping `CommandType`, see bug #3) and, per
the review pass, an initially incomplete async closed-connection test set — both are now fixed.
Fluent's flagged-uncertain `JoinedQueryBuilderSelect*` always-throws question is resolved (the
success path is real and reachable; every prior test just happened to hit a same-name-column
collision), and its non-`DbConnection`/closed-connection pattern gap is covered.

**Status update 2026-09-20 (close-out complete):** `Extrode.Jaunty.Extensions.Reflection`'s two
remaining genuine gaps (the read-path type-handler branch, the wrong-entity-type guard) and its
low-priority ~90-forwarder item are all fixed, as is `Extrode.Jaunty.SourceGenerator`'s
`HandWrittenMapper`/`ParameterRoot`/`ParameterSite` `Equals` gap — each verified by an independent
`review-deep` pass, with every correction that pass surfaced folded back into this report inline.
`Extrode.Jaunty.FlatFiles`/`FlatFiles.DuckDB`, `Extrode.Jaunty.Scaffolding`/`Scaffolding.Cli`, and
`Extrode.Jaunty.Extensions.Logging`/`Npgsql` were already fixed or corrected in the quick-wins batch
above. Every non-class-4 OPEN item in this report is now closed.

**Status update 2026-09-20 (class-4 live-engine pass): CLOSED except FlatFiles.DuckDB's remote
extension path.** SqlServer, PostgreSQL and MySQL/MariaDB were brought up locally (native service +
`docker start torture-postgres torture-mysql torture-mariadb`, reseeded via
`reset-test-databases.ps1`) and every suite re-run with `JAUNTY_REQUIRE_*` set so a skip would fail
instead of passing silently — 5042/5042 (`Extrode.Jaunty.Tests`) and 50/50
(`Extrode.Jaunty.Scaffolding.Tests`), zero skips, all three TFMs. This surfaced and fixed one real
production bug in the PostgreSQL client-side COPY path (a post-`Cancel()` `Dispose()` masking the
real decode failure with `ObjectDisposedException` — see the "0% only because of environment"
section below for the full writeup) and added the first live-connection test for
`NpgsqlCopyImportWriter`. The only remaining open item in this entire report is
`FlatFiles.DuckDB.ImportExecutor`'s remote-URI extension-loading branch, which needs network
reachability to DuckDB's extension repository rather than a database engine.

**Fixed in the process**: `scripts/coverage.ps1` passed `--nologo` to `dotnet test`, which broke
Microsoft.Testing.Platform's `--coverage` path outright — the run reported "Zero tests ran" (exit
5) with it present, and passed normally without it. All 10 suites now produce a cobertura report;
the prior 2026-08-27 baseline's own run may have hit the same silent failure for some suites (not
re-verified here).

## Per-assembly

| Assembly | Complexity | Lines | Cov % | Uncovered complexity |
|---|---:|---:|---:|---:|
| Extrode.Jaunty | 6,667 | 11,432/12,703 | 90.0 | 2,768 |
| Extrode.Jaunty.Fluent | 3,764 | 5,271/5,996 | 87.9 | 2,009 |
| Extrode.Jaunty.SourceGenerator | 952 | 1,077/1,186 | 90.8 | 690 |
| Extrode.Jaunty.FlatFiles.DuckDB | 1,389 | 1,971/2,084 | 94.6 | 466 |
| Extrode.Jaunty.Extensions.Reflection | 1,199 | 1,220/1,419 | 86.0 | 435 |
| Extrode.Jaunty.Scaffolding | 1,785 | 1,079/1,481 | 72.9 | 368 |
| Extrode.Jaunty.Extensions.Logging | 197 | 232/267 | 86.9 | 68 |
| Extrode.Jaunty.FlatFiles | 197 | 297/300 | 99.0 | 10 |
| Extrode.Jaunty.Extensions.Npgsql | 9 | 4/10 | 40.0 | 7 |
| Extrode.Jaunty.Scaffolding.Cli | 54 | 300/304 | 98.7 | 1 |

Ranked by uncovered complexity, same reasoning as the 2026-08-27 baseline: a class at 60% with two
branches isn't a target, a class at 85% with hundreds of units of uncovered complexity is. Overall:
**806 methods have at least one uncovered line (344 fully 0%), out of 3,987 methods across the 10
product assemblies.**

Top 10 files by uncovered complexity (full list of ~150 files with any gap was generated but isn't
reproduced here — these are where the volume actually sits):

| File | Assembly | Cov % | Uncovered cx |
|---|---|---:|---:|
| `Internals/Read/QueryCore.cs` | Extrode.Jaunty | 62.7 | 634 |
| `JauntyGenerator.cs` | SourceGenerator | 92.3 | 446 |
| `Expressions/WhereExpressionVisitor.cs` | Fluent | 84.9 | 350 |
| `ParameterRooting.cs` | SourceGenerator | 84.1 | 242 |
| `Internals/Read/QueryCoreAsync.cs` | Extrode.Jaunty | 93.8 | 237 |
| `Expressions/JoinedGroupByExpressionVisitor.cs` | Fluent | 84.3 | 212 |
| `Expressions/SelectExpressionVisitor.cs` | Fluent | 81.9 | 210 |
| `Import/CsvImport.cs` | Extrode.Jaunty | 88.2 | 207 |
| `Builders/Query/QueryBuilder.cs` | Fluent | 95.3 | 188 |
| `Internals/ExpressionTranslator.cs` | FlatFiles.DuckDB | 91.5 | 138 |

## Classification key

Used throughout below: **1** genuine gap (should get a test) · **2** dead platform path (TFM/OS
conditional CI never compiles or exercises) · **3** defensive/unreachable given caller invariants ·
**4** live-DB/container only, absent in this local run · **5** cross-project attribution artifact ·
**6** compiler/generated-code sequence-point artifact (e.g. a closing brace after `return`).

Headline finding: **class 5 (the Stryker-style cross-project attribution bug) essentially does not
apply here.** `scripts/coverage.ps1` runs every suite and merges by `(file, line)`, so unlike
Stryker's coverage-analysis pre-pass, a method tested only by `Extrode.Jaunty.Tests` still shows as
covered in this report. All seven reviewers checked for it explicitly and found either nothing or
one soft candidate (`TypeHandlerRegistry`, still uncovered in both projects — a real gap, not an
artifact).

## Real bugs found during triage (not gaps — defects)

1. **FIXED 2026-09-20.** Mis-bound test, not a mis-classified line. `InterceptorElapsedScopeTests.cs:118`
   called `ExecuteWithInterception(…, () => ran = true)`. An assignment expression has a value, so
   C# binds that lambda to the `Func<bool>` overload, not the `Action` overload the test was named
   for (`TheSyncActionOverload_RunsTheFullLifecycle`). Fixed to `() => { ran = true; }`.
2. **FALSE POSITIVE — confirmed by an independent `review-deep` pass, no fix needed.**
   `StoredProcedureNullParametersTests.cs:68` passes literal `null`, which overload resolution
   binds to the more specific `SpParameters?` overload. That's the overload the test's own
   surrounding comments say it's targeting — the test name is imprecise, but the test isn't wrong.
   Left as-is.
3. **FIXED 2026-09-20.** `UpsertCoreDirect`/`UpsertCoreDirectAsync` (`Write/Upsert.cs`,
   `Write/UpsertAsync.cs`) never assigned `command.CommandType` from `CommandOptions` — found while
   closing the closed-connection/`CommandTimeout`/`CommandType.StoredProcedure` sweep below.
   `CommandType.StoredProcedure`/`.TableDirect` were silently discarded on every `Upsert` call.

## Extrode.Jaunty (core) — highest-value genuine gaps

- **FIXED 2026-09-20.** `ExecuteNonQueryCoreAsync`'s entire async-interceptor path
  (`Internals/Write/ExecuteNonQueryCore.cs:165-217`, complexity 38, 59 lines) — covered via
  `WriteObservabilityTests.ExecuteAsync_IsIntercepted`.
- **FIXED 2026-09-20.** `BulkDeleteAsync`/`BulkUpdateAsync`'s own-transaction `RollbackAsync` after
  a mid-batch failure (`BulkDeleteAsync.cs:400`, `BulkUpdateAsync.cs:393`) — the only prior failure
  test threw before `BeginTransactionAsync`, so the guarded call never ran. Covered via
  `BulkAsyncMidBatchRollbackTests`: a two-row batch where the second row fails (FK violation for
  delete, unique-index violation for update) after the transaction already opened, confirming the
  first row's write is rolled back.
- **FIXED 2026-09-20.** `GetAllStream<T>(conn, options)` / `GetAllStreamAsync<T>(conn, options, ct)`
  — covered via `GetAllStreamAsyncTests.GetAllStreamAsync_WithCommandOptions_YieldsAllRows` and the
  sync twin in `GetAllTests`.
- **FIXED 2026-09-20.** `Execute(conn, sql)` and `Execute(conn, sql, CommandOptions)` — covered via
  `ExecuteTests.Execute_PlainSql_NoParameters_ReturnsRowsAffected` and
  `Execute_WithCommandOptionsOnly_NoParameters_ReturnsRowsAffected`.
- **FIXED 2026-09-20.** `QuerySingleAsync<T>(conn, sql, ct)` — covered via
  `QuerySingleAsyncTests.QuerySingleAsync_SqlOnly_NoParameters_ReturnsResult`.
- **FIXED 2026-09-20 — surfaced a real bug.** The closed-connection / `CommandTimeout` /
  `CommandType.StoredProcedure` sweep across `Get`/`GetAll`/`Delete`/`Update`/`Upsert`/`Query`/
  `ExecuteBatch`, sync and async, via `CommandOptionsSweepTests` (`RecordingDbConnection` for
  timeout/command-type assertions, a temp-file-backed connection for the closed-connection
  lifecycle — `:memory:` can't be used there since SQLite tears the database down when the last
  connection to it closes). An independent `review-deep` pass caught that the first version of
  this file only exercised the closed-connection lifecycle for the sync half of
  `GetAll`/`Delete`/`Update`/`Upsert`/`Query` and skipped `QueryAsync` in the timeout/command-type
  sweep entirely — the async code paths are separate implementations from sync (e.g.
  `UpsertCoreDirectAsync`'s own `finally` block) and weren't covered by testing the sync side.
  `QueryAsync_WithTimeoutAndCommandType_AppliesBoth` and the five missing
  `*Async_GivenAClosedConnection_OpensExecutesAndCloses` cases were added to close that gap.
  `Upsert`/`UpsertAsync` failed against production code, not the test:
  `UpsertCoreDirect`/`UpsertCoreDirectAsync` (`Write/Upsert.cs`, `Write/UpsertAsync.cs`) never
  assigned `command.CommandType` from `CommandOptions` at all — every other single-entity write
  sets it via its `*Core.cs` file, but Upsert implements its execution inline (AUD-R26) and this
  branch was missed there. `CommandType.StoredProcedure`/`.TableDirect` were silently discarded on
  every `Upsert`/`UpsertAsync` call. Fixed by adding the same conditional assignment the other
  writes use.
- **FIXED 2026-09-20.** `TypeHandlerRegistry.TryConvertFromDb`/`TryConvertToDb` no-handler-registered
  paths — covered via `TypeHandlerRegistryTests`' "No Handler Registered" region.

**DELETED 2026-09-20** (not tested — confirmed dead by an independent `review-deep` pass):
`ExecuteReaderDirect` (65 lines, cx 38) in `Internals/Read/ExecuteReader.cs` was unreachable —
every real ADO.NET `IDbConnection` is also a `DbConnection`, and all real call sites already
routed `DbConnection`s to the other overload first.

## Extrode.Jaunty.Fluent — highest-value genuine gaps

- **FIXED 2026-09-20.** The reversed-operand form of a WHERE comparison is never tested.
  `WhereExpressionVisitor.MirrorOperator` was 0/8 covered — covered via
  `WhereExpressionVisitorTests`' "Reversed-Operand Comparisons" region (`10 < p.UnitPrice`,
  `10 >= p.UnitPrice`).
- **FIXED 2026-09-20 — confirmed reachable, not an always-throws.** `JoinedQueryBuilderSelect*.SelectWithMapping`/
  `SelectWithMapper`(`Async`)'s row-loop and success return. Every existing custom-DTO join test
  joins two Northwind entities whose FK/PK share a column name (`category_id`), which always
  collides under the unaliased `SELECT *` these methods issue and throws before mapping a row.
  `JoinedQuerySelectDtoSuccessPathTests` adds two fixture tables that join on differently-named
  columns (`id`/`from_id`) — nothing collides, and the mapping loop genuinely runs and returns
  rows, for both `Select<T>()`/`SelectAsync<T>()` and `Select(mapper)`/`SelectAsync(mapper)`.
- **PARTIALLY FIXED 2026-09-20 — corrected.** `GROUP BY`/`HAVING` operators: the original claim that
  only `>` and `<` were exercised was wrong for `=` — `FluentGroupByTests.cs:406` already covered
  `Min(...) == "Chai"` before this pass (caught by an independent `review-deep` verification).
  `<>`, `>=`, `<=`, and the unsupported-operator throw were genuinely untested and are now covered
  via `FluentGroupByTests`/`FluentGroupByJoinTests` for both single-entity and joined group-by.
- **FIXED 2026-09-20.** `Sql.Year/Month/Day` in a SELECT projection — covered via
  `SelectExpressionVisitorTests.Visit_SqlYear_GeneratesYearFunction` and its Month/Day twins.
- **FIXED 2026-09-20.** The ~40-instance `if (_connection is not DbConnection dbConn) throw` guard
  and the ~25-instance `wasClosed` auto-open/close lifecycle — each one pattern repeated many times
  (every fixture hands in an already-open `SQLiteConnection`), not that many independent gaps.
  Covered via `QueryBuilderConnectionLifecycleTests` on `QueryBuilder<T>.Delete`/`DeleteAsync`
  (representative of the shared pattern): a non-`DbConnection` `IDbConnectionWrapper` proves the
  async guard throws while the sync path still works, and a temp-file-backed closed connection
  proves the auto-open/close lifecycle for both sync and async.
  **Correction (independent `review-deep` verification, 2026-09-20):** this closes the item by
  representation, not by coverage — the join builders' own `wasClosed` bodies and guards
  (`JoinedQueryBuilderSelect.cs`, sync and async) are separate code paths that remain unexecuted on
  their closed/non-`DbConnection` branches. No production bug was found in either path exercised.

Corrected against the 2026-07-04 report: `JoinedQuery4Builder<T1..T4>` is **no longer** 0/164
(partially covered now, 28 methods still gap); `JoinClause4Builder<T1..T4>` is **fully covered**
(drop from any future list); `Internals.QueryBuilderBase` **no longer exists** in `src/` (the type
was removed or renamed — don't carry the item forward).

## Extrode.Jaunty.Extensions.Reflection — highest-value genuine gaps

- **FIXED 2026-09-20.** `MetadataCache.CreateSetter`'s type-handler-on-`Nullable<T>` branch and its
  `handler.Parse`-throws branch on the *read* path had no test — the write-path equivalent
  (`ThrowingTypeHandlerContractTests`) was covered, the read path wasn't. Covered directly via
  `MetadataCache<T>.GetSetters`/`PropertySetter<T>.Set` against a hand-rolled `IDataReader`, per the
  established pattern in `ReflectionSetterCachingTests`, in
  `TypeHandlerReadPathReflectionTests.NullablePropertyWithARegisteredHandler_ConvertsTheParsedValueToTheUnderlyingType`
  and `...NullablePropertyWhoseHandlerThrows_WrapsTheFailureInAnInvalidOperationException`. **Note
  (independent `review-deep` verification, 2026-09-20):** the throws-arm test is non-vacuous and
  mutation-kills its branch; the nullable-conversion-arm test reaches the line but does not
  distinguish the `ChangeType` conversion sub-arm from an identity pass-through, because
  `TypeHandler<T>.Parse` always returns the already-underlying type — that sub-arm is effectively
  dead for any well-behaved handler and cannot be meaningfully pinned through the public
  `TypeHandler<T>` contract. Not a masked bug, just a reach-vs-pin distinction worth recording.
- **FIXED 2026-09-20 — corrected.** `JauntyReflectionExtensions`'s typed insert/update/delete
  binders' wrong-entity-type guard (`if (entityObj is not T entity) throw new
  InvalidOperationException(...)`, identical in all three) was claimed to have its insert equivalent
  already tested — a grep for the guard's exact message text across `tests/` found zero matches for
  any of the three binder kinds. Added `TypedBinderWrongEntityTypeTests`, covering insert, update,
  and delete for both a wrong-type object and `null`.
- **FIXED 2026-09-20.** ~90 pure one-line forwarder methods across the four `*DialectWithBulkCopy`
  wrapper classes (`EscapeStringLiteral`, `GenerateCoalesce`, window functions, etc.) — lowest
  priority in the whole inventory. `BulkCopyDialectFactoryTests.Wrapper_DelegatesNonBulkCopyMembersToInnerDialect`
  already existed as exactly the "one parameterized test per wrapper" the report suggested, but only
  asserted 12 of ~39 `ISqlDialect` members; extended to assert all of them (every member except the
  two deliberately-overridden bulk-copy ones, already covered by adjacent tests in the same file).
  No production bug found.
- `PostgreSqlBulkCopyProvider`/`SqlServerBulkCopyProvider`'s actual `CopyToServer(Async)` bodies are
  class 4 (need a live Postgres/SqlServer connection) — the *unreachable-branch* guards around them
  (Npgsql/SqlClient member-resolution failures) are class 3, not gaps, since those packages are
  always referenced by the test project.

## Extrode.Jaunty.SourceGenerator — a caching-correctness gap worth flagging

Most uncovered lines here are class 5 in a specific "build-time" sense: the generator's *output* is
tested by entities compiled into the test assembly at normal build time, which never runs under the
coverage collector (the generator itself only executes inside `csc`). Only tests using
`GeneratorHarness`/`RunGeneratorsAndUpdateCompilation` (`GeneratorCachingTests`,
`ParameterRootingEmissionTests`) actually exercise the generator in-process and count as real
coverage of it.

**FIXED 2026-09-20 — corrected.** `HandWrittenMapper.Equals` and `ParameterRoot`/`ParameterSite`'s
`Equals` were untested by the incremental-cache tests — `GeneratorCachingTests` never included a
hand-written mapper or a parameter call site in its second-run comparison. Added four tests:
`EditingAnUnrelatedFile_DoesNotRerunTheHandWrittenMapperStep` and
`RenamingTheConventionBinder_DoesRerunTheStep` (positive/negative pair covering `HandWrittenMapper`'s
comparer), and the equivalent pair for `ParameterRoot`/`ParameterSite`
(`EditingAnUnrelatedFile_DoesNotRerunTheParameterRootingStep` /
`ChangingTheRootedParametersShape_DoesRerunTheStep`). No production bug found.
**Correction (independent `review-deep` verification, 2026-09-20):** "Equals/GetHashCode" overstated
the fix — the Roslyn incremental driver's state tables compare cached values with `comparer.Equals`
only, never `GetHashCode`, so `HandWrittenMapper.GetHashCode`/`ParameterRoot.GetHashCode`/
`ParameterSite.GetHashCode` remain genuinely untested and should still show red in a future coverage
run; only the `Equals` half is closed. The review also caught that the first negative-control test
(inserting a new `BindInsert` method) grew the class's syntax span, so it could pass even if `Equals`
dropped `Supplies`/`Members` and kept only `Location` — fixed by renaming an already-present binder
to a same-length name (`BindInsert` → `BindUpdate`) instead, which isolates `Members`/`Supplies`
from `Location`. Both positive tests were also missing a first-run assertion that the pipeline
produced a non-null value at all (a transform that always returns `null` would pass the "everything
stayed cached" assertion trivially); both now assert the first run's diagnostic/generated text
before checking the second run is cached. Separately noted, not fixed: `ParameterSite.Location` is
still a raw Roslyn `Location` (unlike `HandWrittenMapper.Location`, which AUD-R35-230 already moved
to the tree-independent `LocationInfo`) — the same syntax-tree-rooting retention AUD-R35-230 removed
elsewhere, left in place here.

## Extrode.Jaunty.FlatFiles / FlatFiles.DuckDB

Mostly small, trivial one-line gaps (empty input, an unlisted enum arm, a format-string overload
combination) across `JsonFileSource`, `ParquetFileSource`/`TsvFileSource`, `DuckDb`,
`ExpressionTranslator`, `MappedPropertyFilter`, `ReaderValueConverter`, `TablePromoter`, and the
DuckDB import/write-back internals. Two worth calling out:
- **FIXED 2026-09-20.** `ExpressionTranslator`'s `NOT (...)` negation (`VisitExpression`, `!x.Flag`)
  had no predicate test at all — covered via
  `ExpressionTranslatorTests.Translate_NotOperator_GeneratesNegatedBoolColumnSql`.
- `ImportExecutor.ImportUsingDbBatchAsync` (31 lines) needs a live SqlClient/Npgsql target — SQLite
  reports `CanCreateBatch == false`, so this path can't be reached with the local fixture.

## Extrode.Jaunty.Scaffolding / Scaffolding.Cli

Confirms the 2026-07-04 report's "0% only because containers were absent" category is still
largely true: every `{SqlServer,PostgreSql,MySql}SchemaReader` method (connection open, schema
read, table/column/PK/FK reads) is gated by `RequiredEngine.cs`'s live-engine skip, same as before.
**PARTIALLY FIXED 2026-09-20 — corrected.** New finding as originally stated was half wrong: the
`--`/`/* */` comment-inside-column-body handling claim was already covered by
`IsWithoutRowId_ReadsTheTableOptionsTailOnly`'s existing `InlineData` rows (caught by an
independent `review-deep` verification) — no test was added for that half. The other half was
real but mis-described: the existing `IsWithoutRowId_ToleratesADifferentQuoteCharacterInsideAnIdentifier`
test (renamed from a doubled-`'` framing) doubles an unrelated `'` character, which SQLite never
treats as an escape — it never exercised the reader's actual doubled-*delimiter* (`""`) escape
branch. Added `IsWithoutRowId_HandlesADoubledDelimiterInsideAnIdentifier`, which doubles the real
`"` delimiter; the production code in `SQLiteSchemaReader.IsWithoutRowId` was already correct, so
this was a test-only fix.

## Extrode.Jaunty.Extensions.Logging / Npgsql

**FIXED 2026-09-20.** Small assemblies, mostly trivial "no test calls this public overload" gaps in
`LoggingConfiguration`'s fluent builder (`WithSensitiveParameters`, `WithSlowQueryThreshold`,
`WithMinimumLogLevel`) — covered via `ConfigurationContractTests`' new region. `Extensions.Npgsql`
is 40% covered but only 9 units of complexity total — `NpgsqlCopyImportWriter`'s constructor/dispose
surface needs a live Postgres connection (class 4, still open), consistent with the 2026-07-04
report's Postgres-BulkCopy category.

## 0% only because of environment (class 4 — verify with containers before writing tests)

**Status update 2026-09-20 (live-engine verification pass): CLOSED for SqlServer/MySQL/Postgres.**
The torture-test containers (`torture-postgres`, `torture-mysql`, `torture-mariadb`) and the
local native SqlServer instance were brought up and reseeded via
`scripts/reset-test-databases.ps1 --execute -SkipLocal`, then every suite was re-run with
`JAUNTY_REQUIRE_SQLSERVER=1 JAUNTY_REQUIRE_POSTGRESQL=1 JAUNTY_REQUIRE_MYSQL=1` (a stopped/absent
engine fails loudly instead of skipping — see `DialectReachability`/`RequiredEngine`), so a green
run here really means these were exercised end-to-end, not silently skipped:
- `Extrode.Jaunty.Extensions.Reflection.BulkCopy.{PostgreSql,SqlServer}BulkCopyProvider`'s
  `CopyToServer(Async)` bodies — **verified**, `Extrode.Jaunty.Tests` 5042/5042 with 0 skips
  (net8.0/net10.0/net472).
- `Extrode.Jaunty.Scaffolding.Providers.{SqlServer,PostgreSql,MySql}SchemaReader` (all methods) —
  **verified**, `Extrode.Jaunty.Scaffolding.Tests` 50/50 with 0 skips.
- `Extrode.Jaunty.Extensions.Npgsql.NpgsqlCopyImportWriter` — **FIXED, and a real bug found.** No
  test had ever opened a real `NpgsqlConnection` against this class before (the existing
  `CsvImportPostgreSqlStdinTests` fakes the factory with a recording writer that never reproduces
  Npgsql's real `Dispose`-after-`Cancel` behavior). Added
  `NpgsqlCopyImportWriterLiveTests.cs` against the live container, which surfaced a genuine
  production defect: `ImportPostgreSql(Async)` wrapped the write loop in `using (copy)`; on a
  mid-file failure it called `copy.Cancel()` (which ends the underlying Npgsql stream), then the
  `using` block's implicit `finally` called `copy.Dispose()` again, which Npgsql throws
  `ObjectDisposedException` for — and an exception thrown from a `finally` while another
  propagates *replaces* it, so callers saw "The COPY operation has already ended" instead of the
  decode error that actually caused the import to fail. Fixed in `CsvImport.cs`'s
  `ImportPostgreSql`/`ImportPostgreSqlAsync` by disposing explicitly inside the try/catch (swallowed
  on the cancel path) instead of via an enclosing `using`. Reproduced and re-verified fixed against
  the live container on net8.0/net10.0/net472.
  **Correction (independent `review-deep` verification, 2026-09-20):** the root cause and fix are
  correct, but the pass found one real pre-existing leak the fix should also close and two test
  weaknesses, all now fixed: (1) `RequireFileOnThisMachine(filePath)` ran *before* the `try` around
  the write loop, so a missing file left a caller-owned, already-open connection stuck in Npgsql's
  CopyIn mode with nothing to cancel or dispose it — moved inside the `try` (both sync and async) so
  it is cancelled/disposed like any other mid-copy failure; (2) the original test's file was small
  enough to fail decoding on the very first buffered read, so despite its name no bytes had actually
  reached the live COPY stream before the failure — replaced with a fixture that spans multiple
  4096-char read buffers, so earlier rows really are written before the invalid byte sequence hits;
  (3) the assertion only checked `IsNotType<ObjectDisposedException>`, which would also pass for an
  unrelated masking exception — tightened to `IsType<DecoderFallbackException>` (confirmed as the
  real exception type by running the test) plus a `Count(...) == 0` check that the connection was
  left clean. An async-sibling live test was also added, since only the sync path had one. A
  one-sentence addition to `ICopyImportWriter.Cancel`'s remarks now documents the implicit
  ordering contract (mark aborted before anything that can throw) that made the Npgsql fix safe.
- `Extrode.Jaunty`'s in-transaction FK-toggle branches in Bulk{Insert,Delete,Update}(Async) —
  Postgres/MySQL only, SQLite and SqlServer don't take that branch shape — **verified**, part of
  the same 5042/5042 zero-skip run.
- `CsvImport`'s live MySQL/SqlServer/Postgres import paths, and `ExecuteStoredProcedure*`'s
  output-parameter paths (SQLite has no stored procedures) — **verified**, same run.
- `FlatFiles.DuckDB.ImportExecutor.ImportUsingDbBatchAsync` and `EnsureExtensionsLoadedAsync`'s
  remote-URI branch — **still open.** Out of scope for this pass: needs network reachability to
  DuckDB's extension repository, not a SqlServer/MySQL/Postgres engine, so bringing up the three
  containers above didn't touch it. No test for it exists yet.

The 2026-07-04 report's original caveat ("no live containers available") is resolved for the three
engines this pass covered. `FlatFiles.DuckDB`'s remote-extension path remains the one genuinely
open class-4 item in this report.

## How this was produced

1. `scripts/coverage.ps1` (fixed this session — see top), all 10 suites, default `net10.0`/`Release`.
2. Cobertura XML parsed per suite; per-`(assembly, file, class, method)` line-hit data unioned
   across all 10 reports (a line counts as covered if *any* suite's run hit it) — this is what
   makes class 5 (cross-project attribution) largely moot here, unlike Stryker's per-project
   coverage-analysis pre-pass.
3. Nested/compiler-generated class names folded to their outer class for file/assembly rollups;
   partial classes keyed by name.
4. Every method with `lines_covered < lines_valid` (806 of 3,987) was handed to a `review-deep`
   worker (`~/.claude/tools/worker.js`, fable) scoped to one assembly (Extrode.Jaunty split into 4
   file-bounded sub-batches to fit the turn budget), each with read access to source, tests, and
   this doc's own prior triage philosophy, and asked to classify per the key above with a concrete
   file:line or test-name reference.
5. This document synthesizes those seven reports; the ~90 trivial-forwarder and ~40-repeated-guard
   entries are collapsed into single line items above rather than listed individually. The raw
   worker output isn't retained (scratch, not committed) — re-run the same batching against a fresh
   `tmp/coverage_methods.json` to regenerate it if a specific area needs re-checking in this detail.

## How to reproduce

```powershell
pwsh -NoProfile -File scripts/coverage.ps1
```

## See Also

- [`../code-coverage.md`](../code-coverage.md) — methodology, `coverage.runsettings`, why mutation
  testing (not line coverage) is the real gate for logic correctness.
- [`coverage-gaps-2026-07-04.md`](coverage-gaps-2026-07-04.md) — prior report; several of its
  findings are confirmed fixed or superseded above.
