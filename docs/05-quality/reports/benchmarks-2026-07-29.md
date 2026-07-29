# Benchmark Results — 2026-07-29 (pre-GA regression check)

Re-run of the read-path benchmarks against `dev@b821a72`, to confirm nothing regressed in the
~800 commits and 26 audit rounds since [`BENCHMARKS-2026-07-04.md`](BENCHMARKS-2026-07-04.md).

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 7840HS w/ Radeon 780M, 1 CPU, 16 logical / 8 physical cores
.NET SDK 10.0.203, host .NET 8.0.26, X64 RyuJIT AVX-512
Command: dotnet run -c Release -f net8.0 -- --filter "*QueryBenchmarks*"
         from benchmarks/Jaunty.Benchmarks
Global total time: 16m41s, 186 benchmark cases
```

**Headline: no regression. Jaunty's read path got materially faster relative to hand-coded
ADO.NET on every provider measured in July.**

## Scope and caveats

- Read path only (`Query<T>` materialization). Bulk-copy suites are still unmeasured, as in
  July — the README's per-provider bulk-copy ranges remain unverified.
- Same machine, OS build and runtime as the July run, so the ratios are directly comparable.
  Absolute times still are not: this is a laptop, and the July numbers were taken under a
  different thermal and background load. **Treat ratios, not absolute times, as the signal.**
- Row counts are 1 / 100 / 10,000. July reported 100 / 1,000 / 10,000; the 1,000 case is not in
  the current parameter set, so it has no counterpart below.
- Warm-job (steady-state) rows only. Cold rows are in the raw artifacts under
  `benchmarks/results/`.
- SQL Server is a local instance; PostgreSQL and MariaDB are the `docker-compose.yml`
  containers on ports 5433 and 3307. All three use a dedicated `jauntybench` /
  `JauntyBench` database, so the Northwind test databases were not touched.

## Regression check vs 2026-07-04

Jaunty `Query<T>` mean, as a ratio against hand-coded ADO.NET on the same run. Lower is better.

| Provider | Rows | 2026-07-04 | 2026-07-29 | Change |
|---|---|---|---|---|
| SQLite | 100 | 1.47x slower | **1.14x slower** | improved |
| SQLite | 10,000 | 1.80x slower | **1.12x slower** | improved |
| MariaDB | 10,000 | 1.73x slower | **1.36x slower** | improved |
| MariaDB (`WithExpectedRowCount`) | 10,000 | 1.33x slower | **1.13x slower** | improved |

Nothing got worse. The `WithExpectedRowCount` overload remains the fastest Jaunty path at
scale, and now lands within 2–13% of hand-coded ADO.NET on every provider.

## Warm job, 100 rows

| Method | SQLite | SQL Server | PostgreSQL | MariaDB |
|---|---|---|---|---|
| ADO.NET (hand-coded) | 68.6 us — baseline | 140.4 us — baseline | 431.1 us — baseline | 452.2 us — baseline |
| **Jaunty `Query<T>`** | 78.3 us — 1.14x | 155.8 us — 1.11x | 430.6 us — **1.00x faster** | 470.1 us — 1.04x |
| **Jaunty (`WithExpectedRowCount`)** | 90.2 us — 1.31x | 141.2 us — 1.01x | 422.4 us — **1.02x faster** | 444.9 us — **1.02x faster** |
| Dapper | 62.8 us — 1.09x faster | 148.1 us — 1.05x | 435.7 us — 1.01x | 465.4 us — 1.03x |
| RepoDb | 48.5 us — 1.42x faster | 146.0 us — 1.04x | 437.2 us — 1.01x | 456.2 us — 1.01x |
| linq2db | 64.6 us — 1.06x faster | 158.1 us — 1.13x | 489.5 us — 1.14x | 466.3 us — 1.03x |
| EF Core | 168.9 us — 2.46x | 299.7 us — 2.13x | 613.9 us — 1.42x | see below |

At 100 rows over a network round trip the provider dominates, and every micro-ORM lands within
a few percent of ADO.NET. The in-proc SQLite column is where the mapper itself is visible.

## Warm job, 10,000 rows

| Method | SQLite | SQL Server | PostgreSQL | MariaDB |
|---|---|---|---|---|
| ADO.NET (hand-coded) | 7,574 us — baseline | 3,703 us — baseline | 3,791 us — baseline | 4,074 us — baseline |
| **Jaunty `Query<T>`** | 8,499 us — 1.12x | 4,333 us — 1.17x | 6,695 us — 1.77x | 5,541 us — 1.36x |
| **Jaunty (`WithExpectedRowCount`)** | 7,853 us — **1.04x** | 3,773 us — **1.02x** | 3,992 us — **1.05x** | 4,610 us — **1.13x** |
| Dapper | 7,444 us — 1.02x faster | 5,511 us — 1.49x | 6,071 us — 1.60x | 7,418 us — 1.82x |
| RepoDb | 5,547 us — 1.37x faster | 4,547 us — 1.23x | 5,644 us — 1.49x | 5,423 us — 1.33x |
| linq2db | 7,656 us — 1.01x | 4,560 us — 1.23x | 6,647 us — 1.75x | 5,965 us — 1.47x |
| EF Core | 11,140 us — 1.47x | 7,260 us — 1.96x | 9,804 us — 2.59x | see below |

**Jaunty is the fastest micro-ORM tested on SQL Server, PostgreSQL and MariaDB at 10,000 rows**,
provided `WithExpectedRowCount` is used. Without it, list growth reallocation costs 8–68%
depending on provider — worst on PostgreSQL (1.05x → 1.77x), which is the strongest argument
for documenting that overload prominently.

RepoDb wins on in-proc SQLite at scale (1.37x *faster* than hand-coded ADO.NET, which means its
baseline comparison is measuring something structurally different — worth a look, not a defect).

## Allocation

Jaunty allocates least of the mapping libraries at scale. At 10,000 rows on SQL Server:

| Method | Allocated | vs ADO.NET |
|---|---|---|
| ADO.NET (hand-coded) | 1,122 KB | baseline |
| **Jaunty (`WithExpectedRowCount`)** | **1,123 KB** | **1.00x** |
| Jaunty `Query<T>` | 1,305 KB | 1.16x |
| RepoDb | 1,625 KB | 1.45x |
| Dapper | 2,105 KB | 1.88x |
| EF Core | 3,323 KB | 2.96x |

`WithExpectedRowCount` reaches allocation parity with hand-written ADO.NET — the same result as
July, now confirmed across all four providers rather than two.

## Harness defect found and fixed

The EF Core MariaDB cases reported `NA` for every row count. Cause: `BenchmarkDbContext` handed
Pomelo `_connection.ConnectionString`, and MySqlConnector strips the password out of that once
the connection is open unless `PersistSecurityInfo=true`. Every iteration threw
`Access denied for user 'root' (using password: NO)`.

Fixed in `cd410cd` by adding `DatabaseSetup.GetConnectionString(provider)`, which returns the
string as configured. Verified over 279 cases with zero `Access denied`. Provisional EF Core
MariaDB figures from that short-job run — **not** comparable in precision to the tables above:

| Rows | ADO.NET | EF Core |
|---|---|---|
| 1 | 412 us | 3,801 us |
| 100 | 448 us | 3,923 us |
| 10,000 | 4,370 us | 11,990 us |

The full-config numbers in the tables above were measured **before** this fix, which is why the
MariaDB EF Core cells say "see below". Everything else in this report is unaffected — the bug
was confined to the EF Core MariaDB path.

## Follow-ups

- Re-run the full config now that the EF Core MariaDB fix has landed, to fill in that one cell.
- Bulk-copy suites are still unmeasured. The README's per-provider ranges remain unverified.
- Consider making `WithExpectedRowCount` more prominent in the docs: on PostgreSQL at 10,000
  rows it is the difference between 1.05x and 1.77x.
