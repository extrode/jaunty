# Benchmark Results — 2026-09-02 (corrected baseline, read-path changes)

Re-run of the read-path benchmarks against `dev@136877a5` plus the harness fixes in this
branch, after the generated mapper changes of the same day. It supersedes
[`benchmarks-2026-07-29.md`](benchmarks-2026-07-29.md) for every number the README quotes.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 7840HS w/ Radeon 780M Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Cold   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Warm   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
Command: dotnet run -c Release -f net10.0 -- --filter "*.Benchmarks.QueryBenchmarks.*"
         from benchmarks/Jaunty.Benchmarks
Global total time: 00:38:31, 264 benchmark cases
```

**Headline: against a corrected baseline Jaunty is the fastest of the five libraries on SQL Server, and with `WithExpectedRowCount` on PostgreSQL and MariaDB it is within noise of the hand-coded loop. On SQLite the hand-coded loop is now the floor, 1.33x below Jaunty, and no library reaches it. Every "faster than ADO.NET" from the July reports was the baseline's `GetDecimal` round-trip.**

## What changed since the July report

Three things in Jaunty's read path and three in the harness. The read-path changes are in the
[CHANGELOG](../../../CHANGELOG.md) under Unreleased and told in full in
[How Jaunty got fast](../../08-learn/how-jaunty-got-fast.md):

1. A `decimal` property on a column that reports `double` is read through `GetDouble` and cast,
   decided once per result set. On SQLite that is every `REAL` column.
2. The per-column `IsDBNull` pre-check ahead of non-nullable value types is gone; the named
   NULL error is produced from a `catch` instead ([decision 010](../../decisions/2026-09-02-010-null-guard-by-catch-not-precheck.md)).
3. A custom mapper is handed to the row loop as given, without a wrapping delegate.

The harness fixes answer the validity findings the July report and a 2026-09-02 review raised:

| Finding | Fix |
|---|---|
| The hand-coded baseline called `GetDecimal` on SQLite's `REAL` price column, a text round-trip that cost it 3 ms per 10k rows and made Dapper and RepoDb look faster than ADO.NET | The baseline reads the price as the type the column reports, decided once per reader. The list-capacity hint stays: a hand-written loop that knows its row count would size the list, and Jaunty's `WithExpectedRowCount` case is the like-for-like comparison |
| RepoDb's SQLite bool workaround (`TypeMapper.Add(typeof(bool), DbType.Int64)` plus a property handler) was registered for all four providers, so on SQL Server, PostgreSQL and MariaDB RepoDb round-tripped a native bit or boolean through Int64 on every row | Registered only when the run's provider is SQLite |
| The warm job's 5 iterations gave error bars of 5-10% of the mean, wider than the 1-2% margins the July report quoted | 15 iterations |
| EF Core was said to track entities through `FromSqlRaw` | It did not: `BenchmarkDbContext` has set `QueryTrackingBehavior.NoTracking` since July. The context is still constructed inside the measured region, as a request-scoped context would be in an application; linq2db's `DataConnection` likewise |

## Scope and caveats

- Read path only (`Query<T>` materialization). Bulk-copy suites are unchanged since July.
- Same laptop as the July runs, so ratios are comparable and absolute times are not. Nothing
  else was running on the machine during the run.
- Row counts 1 / 100 / 10,000. Warm-job rows only; cold rows are in `docs/benchmark-artifacts/`.
- SQL Server is a local instance; PostgreSQL and MariaDB are the `docker-compose.yml` containers
  on ports 5433 and 3307, in their own `jauntybench` databases.
- The custom-mapper cases with `GetDouble` are only meaningful on SQLite; on the other providers
  `unit_price` is a real decimal and Setup substitutes the `GetDecimal` mapper, so those rows
  duplicate the plain custom-mapper rows there and are omitted from the provider tables below.

## Regression check vs 2026-07-29

Jaunty `Query<T>` mean as a ratio against hand-coded ADO.NET on the same run. Lower is better.
The July column was measured against the uncorrected baseline, so on SQLite the two ratios are
against different loops; the absolute Jaunty times are the like-for-like comparison there.

| Provider | Rows | 2026-07-29 | 2026-09-02 | Jaunty absolute, July → now |
|---|---|---|---|---|
| SQLite | 100 | 1.14x | **1.28x** | 78.3 us → 57.1 us |
| SQL Server | 100 | 1.11x | **1.01x** | 155.8 us → 146.0 us |
| PostgreSQL | 100 | 1.00x faster | **1.15x** | 430.6 us → 165.2 us |
| MariaDB | 100 | 1.04x | **1.16x** | 470.1 us → 201.8 us |
| SQLite | 10,000 | 1.12x | **1.47x** | 8,499 us → 7,050 us |
| SQL Server | 10,000 | 1.17x | **1.15x** | 4,333 us → 4,604 us |
| PostgreSQL | 10,000 | 1.77x | **1.60x** | 6,695 us → 8,219 us |
| MariaDB | 10,000 | 1.36x | **1.37x** | 5,541 us → 4,746 us |

## Warm job, 100 rows

| Method | SQLite | SQL Server | PostgreSQL | MariaDB |
|---|---|---|---|---|
| ADO.NET (hand-coded) | 44.6 us — baseline | 145.7 us — baseline | 145.1 us — baseline | 175.2 us — baseline |
| **Jaunty `Query<T>`** | 57.1 us — 1.28x | 146.0 us — 1.01x | 165.2 us — 1.15x | 201.8 us — 1.16x |
| **Jaunty (`WithExpectedRowCount`)** | 54.2 us — 1.22x | 152.4 us — 1.05x | 178.0 us — 1.24x | 180.2 us — 1.03x |
| Jaunty (custom mapper) | 67.6 us — 1.52x | 162.6 us — 1.12x | 141.6 us — 1.03x faster | 176.8 us — 1.01x |
| Jaunty (custom mapper, `WithExpectedRowCount`) | 84.0 us — 1.89x | 153.2 us — 1.06x | 154.3 us — 1.07x | 172.7 us — 1.02x faster |
| Dapper | 70.9 us — 1.59x | 212.5 us — 1.46x | 166.0 us — 1.15x | 146.6 us — 1.22x faster |
| RepoDb | 50.9 us — 1.14x | 173.0 us — 1.19x | 153.7 us — 1.07x | 174.6 us — 1.01x faster |
| linq2db | 64.5 us — 1.45x | 191.3 us — 1.32x | 162.5 us — 1.13x | 170.2 us — 1.04x faster |
| EF Core | 170.1 us — 3.82x | 380.1 us — 2.62x | 326.1 us — 2.27x | 9,094 us — 52.07x |

## Warm job, 10,000 rows

| Method | SQLite | SQL Server | PostgreSQL | MariaDB |
|---|---|---|---|---|
| ADO.NET (hand-coded) | 4,876 us — baseline | 4,003 us — baseline | 5,171 us — baseline | 3,478 us — baseline |
| **Jaunty `Query<T>`** | 7,050 us — 1.47x | 4,604 us — 1.15x | 8,219 us — 1.60x | 4,746 us — 1.37x |
| **Jaunty (`WithExpectedRowCount`)** | 6,329 us — 1.32x | 4,548 us — 1.14x | 4,528 us — 1.14x faster | 3,311 us — 1.05x faster |
| Jaunty (custom mapper) | 9,860 us — 2.06x | 4,724 us — 1.18x | 6,719 us — 1.31x | 4,296 us — 1.24x |
| Jaunty (custom mapper, `WithExpectedRowCount`) | 8,142 us — 1.70x | 4,468 us — 1.12x | 4,152 us — 1.25x faster | 3,465 us — 1.01x faster |
| Dapper | 9,577 us — 2.00x | 5,686 us — 1.42x | 6,594 us — 1.28x | 5,690 us — 1.64x |
| RepoDb | 6,404 us — 1.34x | 4,880 us — 1.22x | 5,425 us — 1.06x | 4,572 us — 1.32x |
| linq2db | 7,849 us — 1.64x | 5,006 us — 1.25x | 6,880 us — 1.34x | 5,642 us — 1.62x |
| EF Core | 12,374 us — 2.58x | 10,348 us — 2.59x | 9,425 us — 1.83x | 14,846 us — 4.27x |

**Read the SQLite column of this table with care.** The run took 38 minutes on a laptop, the
SQLite cases had standard deviations of 6-14% of the mean against 2-8% on the server providers,
and within the column the generated mapper with a hint measured 2 ms slower than a hand mapper
making the same reader calls, which three shorter runs the same day contradict. Sustained load
on this machine drifts its clock down over ten minutes or more, and cases run at different
points on that curve. The SQLite numbers the README quotes are from the separate run below.

Elsewhere in the table: `WithExpectedRowCount` beating the baseline on PostgreSQL and MariaDB is
inside the run's noise and should be read as parity.

**Why the unhinted `Query<T>` trails the hinted case by 3.7 ms on PostgreSQL and 1.4 ms on
MariaDB.** The GC columns of the summary say it: every unhinted case has Gen2 collections
(62.5 per 1,000 operations on both providers) and every hinted case has none. A `List<T>` that
starts at the default 64 doubles to 16,384 slots for 10,000 rows, and that last array is
131,072 bytes on 64-bit, over the 85,000-byte large-object threshold; `new List<T>(10_000)` is
80,000 bytes and never crosses it. Dapper, RepoDb and linq2db carry the same Gen2 signature.
Confirmed the same evening by setting the default to 10,000 in the harness and re-running
PostgreSQL at 10,000 rows: unhinted `Query<T>` 4,228 us, no Gen2, the hinted case's allocation.
The default stays 64; the reasoning is in
[decision 012](../../decisions/2026-09-02-012-result-list-default-capacity-stays-64.md). EF Core's 100-row MariaDB case at 9 ms is
`ServerVersion.AutoDetect` making a round trip inside the measured region, a harness cost, not a
library one.

## SQLite, 10,000 rows, measured alone

The same harness, the same day, SQLite and 10,000 rows only, after the machine had cooled:
3m37s for 22 cases. The 10,000-row SQLite rows in the README come from this run.

| Method | Mean | vs ADO.NET | Allocated |
|---|---|---|---|
| ADO.NET (hand-coded) | 4,082 us | baseline | 1.07 MB |
| **Jaunty `Query<T>`** | 5,417 us | 1.33x | 1.24 MB |
| **Jaunty (`WithExpectedRowCount`)** | 5,179 us | 1.28x | 1.07 MB |
| Jaunty (custom mapper) | 8,285 us | 2.04x | 1.55 MB |
| Jaunty (custom mapper, `WithExpectedRowCount`) | 7,370 us | 1.81x | 1.37 MB |
| Jaunty (custom mapper, `GetDouble`) | 4,891 us | 1.20x | 1.24 MB |
| Jaunty (custom mapper, `GetDouble`, `WithExpectedRowCount`) | 4,042 us | 1.01x faster | 1.07 MB |
| Dapper | 7,453 us | 1.84x | 2.16 MB |
| RepoDb | 5,771 us | 1.42x | 1.55 MB |
| linq2db | 7,867 us | 1.94x | 1.70 MB |
| EF Core | 12,415 us | 3.06x | 3.44 MB |

Two of the remaining 1.3 ms between the generated mapper and the hand-coded loop are known. The
generated row mapper checks `reader.FieldCount` on every row as a guard against a stale
delegate meeting a changed shape, and on Microsoft.Data.Sqlite that is a native call, about
0.4 ms per 10,000 rows; and it calls `IsDBNull` on the nullable `product_name` column, which
the hand loop does not, about the same again. Both are candidates for the next pass.

## Allocation, 10,000 rows on SQL Server

| Method | Allocated | vs ADO.NET |
|---|---|---|
| ADO.NET (hand-coded) | 1,096 KB | baseline |
| Jaunty (custom mapper, `WithExpectedRowCount`) | 1,096 KB | 1.00x |
| **Jaunty (`WithExpectedRowCount`)** | 1,097 KB | 1.00x |
| Jaunty (custom mapper) | 1,274 KB | 1.16x |
| **Jaunty `Query<T>`** | 1,274 KB | 1.16x |
| RepoDb | 1,274 KB | 1.16x |
| linq2db | 1,277 KB | 1.17x |
| Dapper | 2,056 KB | 1.88x |
| EF Core | 3,241 KB | 2.96x |

## Custom mapper, side by side, SQLite 10,000 rows

All four variants read the same five columns with the same typed getters; they differ only in
the price getter and the list hint.

| Variant | Price read | List hint | Mean | vs ADO.NET | Allocated |
|---|---|---|---|---|---|
| ADO.NET (hand-coded) | `(decimal)GetDouble(2)` | 10,000 | 4,876 us | baseline | 1,095 KB |
| Jaunty `Query<T>` | generated, `GetDouble` | none | 7,050 us | 1.47x | 1,273 KB |
| Jaunty (`WithExpectedRowCount`) | generated, `GetDouble` | 10,000 | 6,329 us | 1.32x | 1,096 KB |
| custom mapper | `GetDecimal(2)` | none, grows from 64 | 9,860 us | 2.06x | 1,585 KB |
| custom mapper, `WithExpectedRowCount` | `GetDecimal(2)` | 10,000 | 8,142 us | 1.70x | 1,407 KB |
| custom mapper, `GetDouble` | `(decimal)GetDouble(2)` | none | 5,594 us | 1.17x | 1,272 KB |
| custom mapper, `GetDouble`, `WithExpectedRowCount` | `(decimal)GetDouble(2)` | 10,000 | 4,319 us | 1.13x faster | 1,095 KB |

## Method

BenchmarkDotNet spawns one process per (method, parameters, job), so each provider's RepoDb
registration and Jaunty's static caches are fresh per case. The Cold job runs one invocation
with no warmup and records first-call cost including cache population; the Warm job runs two
warmup iterations and then 15 measured iterations. `MemoryDiagnoser` reports managed
allocation per operation.

Earlier reports: [benchmarks-2026-07-29.md](benchmarks-2026-07-29.md),
[BENCHMARKS-2026-07-04.md](BENCHMARKS-2026-07-04.md).
