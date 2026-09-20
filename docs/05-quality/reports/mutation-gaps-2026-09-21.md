# Mutation Testing Gaps — 2026-09-21

`CI_MUTATION_ENABLED` is still off (pending the Blacksmith runner activation — an existing owner
blocker), so the nightly mutation matrix in `.github/workflows/nightly.yml` has never run against
the current, widened 6-project `stryker-config.json` scope. The only prior score on record is a
2026-08-27 combined run (`Extrode.Jaunty` + `Extrode.Jaunty.Fluent`, 96.19%, 2h10m on an M1 Mac) —
stale (predates the namespace rename and several import-path changes) and not broken out per
project. This report measures the two newest/highest-risk configs live, on the dev box, and
records what actually survives.

## Results

| Assembly | Mutate scope | Score | Killed / Survived / Timeout / Tested | Threshold (`high`/`low`) |
|---|---|---|---|---|
| `Extrode.Jaunty.FlatFiles.DuckDB` | `Internals/**`, `WriteBack/**` | **82.37%** | 975 / 110 / 11 / 1096 | 80 / 60 — above `high` |
| `Extrode.Jaunty.Scaffolding` | `Providers/**`, `Scaffolder.cs` | **74.04%** | 846 / 235 / 21 / 1102 | 80 / 60 — between |
| `Extrode.Jaunty` | `Internals/Parameters/**`, `Dialects/**` | 96.19%* | not run this pass | — |
| `Extrode.Jaunty.Fluent` | `Expressions/**` | 96.19%* | not run this pass | — |
| `Extrode.Jaunty.SourceGenerator` | per its config | never measured | not run this pass | — |
| `Extrode.Jaunty.UnitTests` (+ cross-project `Extrode.Jaunty.Tests` coverage) | shared w/ `Extrode.Jaunty` | not separately isolated | not run this pass | — |

\* Combined score from the one historical run; not per-project, and now stale. Treat as
approximate context, not current data.

Reports: `tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/StrykerOutput/2026-09-21.00-40-51/reports/mutation-report.html`,
`tests/Extrode.Jaunty.Scaffolding.Tests/StrykerOutput/2026-09-21.01-57-26/reports/mutation-report.html`.

## Survivor distribution by file

**`Extrode.Jaunty.FlatFiles.DuckDB`** (121 survived+timeout):

| File | Count |
|---|---|
| `Internals/Import/SqlServerImportDialect.cs` | 31 |
| `Internals/Import/ImportExecutor.cs` | 20 |
| `Internals/Import/PostgreSqlImportDialect.cs` | 17 |
| `WriteBack/DuckDbWriteAsync.cs` | 10 |
| `Internals/ExpressionTranslator.cs` | 7 |
| `WriteBack/DuckDbWriteBackAsync.cs` | 7 |
| `WriteBack/DuckDbWriteBack.cs` | 6 |
| `Internals/Import/SqliteImportDialect.cs` | 5 |
| `Internals/TablePromoter.cs` | 5 |
| `WriteBack/DuckDbWrite.cs` | 4 |
| `Internals/Import/ImportTypeMapping.cs` | 2 |
| `Internals/MappedPropertyFilter.cs` | 2 |
| `Internals/ReaderValueConverter.cs` | 2 |
| `Internals/ColumnMappingCache.cs` | 1 |
| `Internals/DuckDbObservation.cs` | 1 |
| `Internals/Import/ImportDialectResolver.cs` | 1 |

**`Extrode.Jaunty.Scaffolding`** (256 survived+timeout):

| File | Count |
|---|---|
| `Providers/SQLite/SQLiteSchemaReader.cs` | 104 |
| `Scaffolder.cs` | 46 |
| `Providers/MySql/MySqlSchemaReader.cs` | 33 |
| `Providers/SqlServer/SqlServerSchemaReader.cs` | 29 |
| `Providers/PostgreSql/PostgreSqlSchemaReader.cs` | 26 |
| `Providers/MySql/MySqlTypeMapper.cs` | 7 |
| `Providers/PostgreSql/PostgreSqlTypeMapper.cs` | 6 |
| `Providers/SQLite/SQLiteTypeMapper.cs` | 4 |
| `Providers/SqlServer/SqlServerTypeMapper.cs` | 1 |

## Discovery 1 — uncommon CLR-type→SQL-type mappings in the Import dialects are never asserted

`SqlServerImportDialect.cs` and `PostgreSqlImportDialect.cs` (48 of DuckDB's 121 survivors, ~40%)
each contain a `switch`-style `clrType == typeof(X) => "SQL_TYPE"` type map
(`MapClrTypeToSqlType`). Mutating the string literal for `DateOnly`, `TimeOnly`, `TimeSpan`,
`char`, `ulong`, `sbyte`, `uint`, `byte` to `""` survives in every case — e.g.:

```csharp
_ when clrType == typeof(DateOnly) => "DATE",       // -> "" survives
_ when clrType == typeof(TimeOnly) => "TIME",       // -> "" survives
_ when clrType == typeof(sbyte)    => "SMALLINT",   // -> "" survives
```

The only tests that exercise these dialects
(`ImportDialectIdentifierEscapingTests.cs`, `ImportDialectKeyNullabilityTests.cs`,
`ImportDialectKeyOnlyUpsertTests.cs`) never reference `DateOnly`, `TimeOnly`, `TimeSpan`, `sbyte`,
`uint`, or `ulong` — grepped and confirmed absent. The live `ImportUsingDbBatchLiveTests.cs`/
`ImportPipelineTests.cs` round-trip fixtures only use common types (`int`, `string`, `decimal`,
`bool`), so a broken mapping for an uncommon type would only surface as a live SQL Server/Postgres
failure the first time a caller actually imports one of those types — not caught by any test
today. **This is a genuine, actionable gap**, not a mutation-testing artifact: an empty SQL type
string passed to `CREATE TABLE` is a real syntax error, so nothing here is a false-equivalent
mutant.

The rest of the two dialects' survivors are separator-joining logic (`if (i > 0) sb.Append(", ")`
mutated to `i >= 0`/`i < 0`) in multi-column `INSERT`/`MERGE` SQL builders — also unasserted at the
exact-SQL-text level, though harder to judge as "real" without checking whether any live test
imports a 2+-column entity through these specific dialects (not yet checked here).

## Discovery 2 — Scaffolding's hand-rolled `CREATE TABLE` tokenizer is 74% of its own file's survivors

`SQLiteSchemaReader.cs` is 104 of Scaffolding's 256 survivors (40% of the whole assembly's
survivor count on its own). Of those 104, **77 (74%) cluster in one region — lines ~276-420**, a
hand-written character-by-character scanner that walks a `CREATE TABLE` DDL string to skip quoted
identifiers (`'`, `"`, `` ` ``, `[...]`), line comments (`--`), and block comments (`/* */`) while
tracking paren depth, used by `IsWithoutRowId` and the DDL-tail probing this session's earlier
`coverage-gaps-2026-09-20.md` work already touched once (`IsWithoutRowId_HandlesADoubledDelimiterInsideAnIdentifier`,
`IsWithoutRowId_ReadsTheTableOptionsTailOnly`).

Representative survivors — boundary conditions on the scan loop are essentially untested:

```csharp
while (i < createSql.Length)          // -> i <= / i > / !(...) all survive
if (createSql[i] != quote)            // -> == survives
i++;                                  // -> i-- / removed survive (twice, two loops)
if (i + 1 < createSql.Length && createSql[i + 1] == quote)  // -> i-1, <=, > all survive
if (ch == '-' && i + 1 < createSql.Length && createSql[i + 1] == '-')  // -> || , != survive
if (ch == '/' && i + 1 < createSql.Length && createSql[i + 1] == '*')  // -> || , != survive
if (sawBody && depth == 0)            // -> || survives
```

That only 1 test (`IsWithoutRowId_HandlesADoubledDelimiterInsideAnIdentifier`, added
2026-09-20) targets this parser directly, against 77 surviving boundary mutants, means the
comment-skipping and bracket-identifier branches in particular are effectively unexercised by any
edge case — a single off-by-one in the loop bounds, or `&&`/`||` swap in the comment-lookahead
checks, would not be caught today. The 2026-07-04 report already flagged schema-reading as
parsing-heavy and under-edge-case-tested; this is the first time it's been located to one specific
77-mutant region rather than described in general terms.

The remaining `MySqlSchemaReader.cs`/`SqlServerSchemaReader.cs`/`PostgreSqlSchemaReader.cs`
survivors (88 combined) and `Scaffolder.cs` (46) have not been drilled into yet.

## Discovery 3 — `Scaffolder.DetectProvider`'s connection-string heuristic had a handful of untested conjuncts

**Correction:** the original pass here claimed `grep -rn "DetectProvider" tests/**/*.cs` returned
nothing. That grep was run without `shopt -s globstar`, so `tests/**/*.cs` silently behaved like
`tests/*/*.cs` (exactly one directory level) and missed
`tests/Extrode.Jaunty.Scaffolding.Tests/Unit/ScaffolderTests.cs` and `ScaffolderDiagnosticsTests.cs`,
both of which contain extensive, pre-existing direct coverage of `DetectProvider` (12+ `[Theory]`/
`[Fact]` cases covering SQLite, SQL Server, PostgreSQL, MySQL, the Npgsql `Server=`/`Host=` alias
ambiguity, and the unparseable-input fallback). An independent fable `review-deep` pass caught this
and it led to briefly writing a ~90%-redundant new test file, which was reverted (`git rm`).

What live mutation data actually showed, once the existing coverage was accounted for: every `&&`/
`||` in the four detection heuristics *is* exercised by some existing test, but none of them isolate
a single conjunct by giving it its only signal — e.g. `Trusted_Connection=` and `User Id=` both
appear in tests that satisfy the SQL Server heuristic, but `Integrated Security=` (the third
alternative in that same conjunct) never appears anywhere. Three genuine, narrow gaps were
identified this way and closed with targeted `[Fact]` additions to the existing `ScaffolderTests.cs`
rather than a new file: `Integrated Security=` as the sole SQL Server auth signal, bare `User=` as
the sole MySQL identity signal, and the Postgres-port heuristic's negative/boundary case (a non-5432
port with the `Server=`/`Host=`-alias shape).

The method (`src/Extrode.Jaunty.Scaffolding/Scaffolder.cs:281`) is a deliberately-ordered set of
heuristics with real, commented-on rationale — e.g. Postgres is checked *before* SQL Server
specifically because Npgsql accepts `Server=`/`User Id=` as aliases for its own `Host=`/`Username=`
keys, so a valid Npgsql string could otherwise satisfy the SQL Server heuristic first. Not every
`&&`/`||` in that ordering survives mutation — several are already caught by existing tests via
short-circuit behavior (e.g. flipping the leading `&&` on line 315 or 320 changes which provider
an *already-tested* connection string resolves to, so those specific mutants are `Killed`). The
live mutation report showed these specific survivors before the three `[Fact]` additions above:

```csharp
if (hasDatabase && (keys.ContainsKey("username") || hasUserId) &&
    (keys.ContainsKey("host") || isPostgresPort))
    return DatabaseProvider.PostgreSql;

if ((hasServer || hasDataSource) &&                                  // -> || survives
    (hasInitialCatalog || hasDatabase) &&                             // -> && survives
    (keys.ContainsKey("trusted_connection") || hasUserId || keys.ContainsKey("integrated security")))
    return DatabaseProvider.SqlServer;                                // -> && / || survive here too

if (hasServer && hasDatabase && (keys.ContainsKey("uid") || keys.ContainsKey("user")))  // -> && / || survive
    return DatabaseProvider.MySql;
```

Given the code comments explicitly describe *why* the Postgres-before-SqlServer ordering exists
(to avoid misclassifying a Postgres string that happens to also satisfy the SQL Server shape),
this is exactly the kind of logic mutation testing exists to catch — a future edit that gets the
`&&`/`||` balance wrong on any of these lines would misdetect a provider and nothing today would
notice. Also worth a look: `TryGetValue`'s not-found path (`value = string.Empty; return false;`)
survives a mutation to `value = "Stryker was here!"`, meaning no caller ever checks the `out`
value on a `false` return — likely fine (the `bool` is the real signal) but confirms that path is
untested either way.

## Status

Discovery only — no fixes applied in this pass. All three findings above are candidates for
follow-up test work — dialect type-mapping `[Theory]` coverage for the uncommon CLR types, targeted
edge-case tests for the SQLite DDL tokenizer's comment/quote/bracket branches, and a
`DetectProviderTests` suite covering ambiguous/boundary connection strings for each of the four
providers — not yet done. The remaining survivor clusters (`MySqlSchemaReader.cs`,
`SqlServerSchemaReader.cs`, `PostgreSqlSchemaReader.cs`, and DuckDB's `WriteBack*.cs` files) have
not been drilled into.

Live-run reproduction: `dotnet stryker --concurrency 4` from
`tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/` or `tests/Extrode.Jaunty.Scaffolding.Tests/`
(requires `torture-postgres`/`torture-mysql`/local SQL Server up, same as the rest of this
project's live suites; DuckDB's run also needs `torture-postgres` reachable since its dialect
tests use a live Postgres target per `coverage-gaps-2026-09-20.md`'s `ImportUsingDbBatchAsync`
item).
