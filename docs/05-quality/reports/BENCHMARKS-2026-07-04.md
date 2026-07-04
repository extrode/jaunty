# Benchmark Results — 2026-07-04 (PRD-002 partial)

Measured with BenchmarkDotNet v0.14.0 on Windows 11, AMD Ryzen 7 7840HS
(8C/16T laptop), .NET 8.0.26, `QueryBenchmarks` full config (Cold + Warm jobs).
Command: `dotnet run -c Release -f net8.0 -- --filter "*QueryBenchmarks*"`
from `benchmarks/Jaunty.Benchmarks`.

**Scope and caveats**

- Read path only (`Query<T>` materialization). Bulk-copy suites were NOT
  properly measured in this pass (the `--quick` dry-job numbers were discarded
  as statistically meaningless); the README's per-provider bulk-copy ranges
  remain unverified against Jaunty and are tracked by PRD-002.
- Laptop hardware; treat ratios, not absolute times, as the signal.
- Warm-job rows below (steady-state); Cold rows in the raw artifacts.

## SQLite (in-proc), Warm job

| Method | Rows | Mean | Ratio vs ADO.NET | Allocated | Alloc ratio |
|---|---|---|---|---|---|
| ADO.NET (hand-coded) | 100 | 143.6 us | baseline | 44.93 KB | — |
| Jaunty `Query<T>` | 100 | 211.6 us | 1.47x | 46.48 KB | 1.03x |
| Dapper `Query<T>` | 100 | 199.3 us | 1.39x | 51.51 KB | 1.15x |
| ADO.NET (hand-coded) | 1000 | 1,163.3 us | baseline | 425.89 KB | — |
| Jaunty `Query<T>` | 1000 | 2,307.2 us | 1.99x (±0.34) | 427.44 KB | 1.00x |
| Dapper `Query<T>` | 1000 | 1,705.4 us | 1.47x | 481.69 KB | 1.13x |
| ADO.NET (hand-coded) | 10000 | 13,522 us | baseline | 4,357 KB | — |
| Jaunty `Query<T>` | 10000 | 24,260 us | 1.80x | 4,359 KB | 1.00x |
| Dapper `Query<T>` | 10000 | 17,613 us | 1.30x | 4,906 KB | 1.13x |

## MariaDB (local), 10,000 rows, Warm job

| Method | Mean | Ratio vs ADO.NET | Alloc ratio |
|---|---|---|---|
| ADO.NET (hand-coded) | 3,526 us | baseline | — |
| Jaunty `Query<T>` | 6,085 us | 1.73x | 1.11x |
| Jaunty `Query<T>` (WithExpectedRowCount) | 4,688 us | 1.33x | 1.00x |
| Dapper `Query<T>` | 5,979 us | 1.70x | 1.40x |
| linq2db | 5,283 us | 1.50x | 1.11x |
| RepoDb | 6,239 us | 1.77x | 1.30x |
| EF Core `ToList` | 16,767 us | 4.75x | 2.34x |

## Honest takeaways

- Jaunty is **allocation-leanest** of the compared ORMs (1.00-1.11x over
  hand-coded ADO.NET vs Dapper's 1.13-1.40x) - the README's memory story holds.
- On throughput Jaunty is **competitive with Dapper, not uniformly faster**:
  ahead on the MariaDB path with `WithExpectedRowCount`, behind Dapper on
  large SQLite reads (1.80x vs 1.30x at 10k rows). The SQLite large-read gap
  is a real optimization target (relates to PRD-005 sync/async parity tuning).
- EF Core trails all micro-ORMs by 2.5-3.5x on these read paths.

## Remaining for PRD-002

- Proper (non-dry) runs of BulkInsert/BulkCopy suites per provider, with
  containers for SqlServer/Postgres/MySQL, to replace the README's
  per-provider "Nx faster" bulk table with measured numbers.
- Publish runs from a quiet CI box rather than a laptop.

## Update — same day, after PROD-117 (per-result-set row mapper)

Root cause of the gap found and fixed: the generated `ReadEntity` ran the
PRD-001 shape-safety validation (`GetName` + case-insensitive compare per
column) on **every row**. The generator now also emits `CreateRowMapper`,
a per-result-set factory that validates once and returns a zero-validation
closure; the dispatcher prefers it and a per-row `FieldCount` guard falls
back to the fully-validating `ReadEntity` if a stale delegate ever meets a
changed shape. All shape-safety regression tests pass unchanged.

Re-run of the same suite, same machine (Warm, SQLite):

| Rows | Jaunty before | Jaunty after | Dapper (same run) |
|---|---|---|---|
| 100 | 1.47x | 1.31x (177.3 us) | 1.07x |
| 1,000 | 1.99x | 1.33x (1,494 us, -35% absolute) | 1.03x |
| 10,000 | 1.80x | 1.46x (22,552 us) | 1.12x |

Multi-provider, 10,000 rows (Warm): Jaunty is now FASTER than Dapper on
MariaDB (6,230 vs 7,130 us) and tied on PostgreSQL (5,510 vs 5,483 us;
3,898 us with `WithExpectedRowCount` - well ahead). Remaining SQLite gap vs
Dapper is `GetFieldValue<T>` vs provider-specific typed getters plus list
growth - candidates for a future pass (PRD-005).

## Update 2 — typed getters (PROD-118)

The generated DbDataReader fast path used `GetFieldValue<T>` (generic
dispatch inside the provider) for every column. The generator now emits
direct typed getters (`GetInt32`, `GetString`, ...) for the 11 directly
supported types, keeping `GetFieldValue<T>` only for the GetValue-fallback
types (TimeSpan/DateTimeOffset/unknown).

Re-run, same machine, 3-way SQLite suite (Warm):

| Rows | original | after PROD-117 | after PROD-118 | Dapper (same run) |
|---|---|---|---|---|
| 100 | 1.47x | 1.31x | 1.17x (156.0 us) | 1.11x |
| 1,000 | 1.99x | 1.33x | 1.37x (1,567 us) | 1.37x (exact tie) |
| 10,000 | 1.80x | 1.46x | **1.03x (14,447 us)** | 1.00x |

At 10k rows Jaunty is now at hand-coded ADO.NET / Dapper parity on SQLite.
Multi-provider 10k (Warm): MariaDB Jaunty 4,479 us vs Dapper 6,797 us;
PostgreSQL with `WithExpectedRowCount` 3,216 us - faster than the
hand-coded ADO.NET baseline (3,623 us).

The PRD-005 "large SQLite read gap" flagged in the first section of this
report is closed.

## Update 3 — bulk-copy suites measured (PRD-002 remainder)

Full-config `BulkCopyBenchmarks` run (SQLite in-proc; local PostgreSQL and
MariaDB servers; SQL Server unreachable - container credentials mismatch, rows NA).
Warm results, 10,000 rows:

| Provider | ADO.NET loop (txn) | Jaunty BulkInsert | linq2db BulkCopy | Dapper loop |
|---|---|---|---|---|
| PostgreSQL | 368.7 ms | **53.6 ms (6.9x faster)** | 72.6 ms | 1,596 ms |
| PostgreSQL 1k | 48.6 ms | **6.4 ms (7.6x faster)** | 7.6 ms | 187.9 ms |
| SQLite | 20.6 ms | **334 ms — 16x SLOWER (defect)** | 16.2 ms | 97.3 ms |
| MariaDB | 480.1 ms | **NA — errored (defect/config)** | 64.4 ms | 20,231 ms |

Findings:

1. **PostgreSQL native bulk (NpgsqlBinaryImporter) verified**: 6.9-7.6x vs a
   transactional ADO.NET loop, and faster than linq2db's COPY path. This
   replaces the README's unverified "15-25x" with a measured number.
2. **SQLite bulk path is a real defect (PROD-120, open)**: 16x slower than a
   plain transactional loop. `SQLiteBulkCopyProvider` code looks correct
   (single txn, prepared, reused params) but has 0% coverage and
   `SQLiteDialect.CreateBulkCopyProvider()` in core returns null - suspicion:
   the "native" path never engages and something upstream (dialect
   resolution, per-row binder, or the WAL pragma churn per call) burns
   ~33us/row. Needs a profiling session.
3. **MariaDB native bulk errors (NA)** while linq2db succeeds on the same
   server - likely `MySqlBulkLoader`'s local-infile requirement
   (`AllowLoadLocalInfile=true` absent from the connection string); the
   provider should surface a clear error either way. Part of PROD-120.
4. SQL Server: pending correct container credentials (JAUNTY_TEST_SQLSERVER).

## Update 4 — PROD-120 fixes verified

- **MySQL/MariaDB provider rewritten** (chunked multi-row INSERT, no
  LOAD DATA LOCAL INFILE / local_infile dependency). Measured @10k rows:
  48.4 ms vs 781 ms transactional loop = **16.1x faster**, ahead of
  linq2db (65.9 ms). @1k: 7.6 ms = 12.9x. Live-tested against MariaDB
  (chunk boundaries, NULLs, external txn rollback, async).
- **SQLite routing fixed**: the real root cause was the multi-row exclusion
  checking `dialect is not SQLiteDialect`, which missed the
  Extensions.Reflection wrapper dialect - SQLite fell into multi-row VALUES
  (quadratic parameter binding in Microsoft.Data.Sqlite) or the now-deleted
  provider, both ~16x slower. BulkInsert now routes SQLite to the prepared
  loop: measured @10k **21.4 ms vs 21.3 ms hand-coded = 1.005x parity**
  (was 334 ms).
- SQL Server local instance reachable this round for baselines; Jaunty's
  SqlServer native path (SqlBulkCopy) still returns NA in the harness -
  next investigation item (PROD-121).

## Update 5 — PROD-121 resolved: SqlServer native bulk measured

Root cause of the NA: `SqlServerBulkCopyProvider` had never successfully
constructed a `SqlBulkCopy`. `MapBulkCopyOptions` returned a boxed `Int32`,
which `Activator.CreateInstance` refuses to bind to the `SqlBulkCopyOptions`
ctor parameter -> `MissingMethodException` on every call. Second latent
defect fixed in the same pass: no `ColumnMappings` were set, so SqlBulkCopy's
default ordinal mapping targeted the destination IDENTITY column. Live
tests added (`SqlServerBulkCopyProviderTests`: identity destination, NULL
round-trip, async, external txn rollback).

BulkCopyBenchmarks, SqlServer (local instance), full config, Warm job:

| Rows | ADO.NET loop | Jaunty native | linq2db BulkCopy | Jaunty vs loop |
|------|-------------:|--------------:|-----------------:|---------------:|
| 100  | 7.3 ms | 2.1 ms | 2.9 ms | **3.5x** |
| 1k   | 69.9 ms | 4.0 ms | 6.0 ms | **17.6x** |
| 10k  | 822.6 ms | 22.5 ms | 21.1 ms | **36.6x** |

Jaunty allocates 6.6x less than the loop baseline @10k (1.19 MB vs 7.8 MB)
and matches linq2db's SqlBulkCopy within noise. Dapper loop: 1,926 ms @10k.

Harness note: the EF Core competitor benchmark itself fails on SqlServer
("Cannot insert explicit value for identity column") - EfProduct's key is
not marked ValueGeneratedOnAdd for this provider. Competitor-benchmark
defect only; does not affect Jaunty results.
