# 012 — The result list's default capacity stays 64; the hint is the answer for big reads

**Date:** 2026-09-02
**Status:** Accepted

## Decision

**`JauntyConfig.QueryResultCapacity` stays at 64, and Jaunty does not try to keep an unhinted
result list under the large-object threshold.** A caller reading thousands of rows sizes the
list with `CommandOptions<T>.WithExpectedRowCount(n)`. That is documented next to the benchmark
table in the README and in the article, and a future performance pass must not raise the default
or add growth logic to reach the hinted number without a hint.

## Why

The 2026-09-02 four-provider run showed the unhinted `Query<T>` at 10,000 rows well behind the
hinted case on the two network providers, and the log's GC columns said why:

| 10,000 rows, warm | Unhinted | Hinted | Gen2 per 1,000 ops, unhinted / hinted |
|---|---|---|---|
| SQL Server | 4,604 us | 4,548 us | 54.7 / 0 |
| PostgreSQL | 8,219 us | 4,528 us | 62.5 / 0 |
| MariaDB | 4,746 us | 3,311 us | 62.5 / 0 |

A `List<T>` that starts at 64 doubles to 16,384 slots on the way to 10,000 rows. On 64-bit
that last array is 131,072 bytes, over the 85,000-byte large-object threshold, so it lands on
the large-object heap and is collected only by a Gen2. `new List<T>(10_000)` is 80,000 bytes and
never crosses the line. Dapper, RepoDb and linq2db show the same Gen2 signature, because none of
them pre-size either.

Confirmed the same evening by setting the default to 10,000 in the benchmark's setup and
re-running PostgreSQL at 10,000 rows, three cases only:

| PostgreSQL, 10,000 rows, warm | Full run, default 64 | Probe, default 10,000 |
|---|---|---|
| ADO.NET (hand-coded) | 5,171 us | 4,767 us |
| Jaunty `Query<T>` | 8,219 us | **4,228 us**, no Gen2, 2.06 MB |
| Jaunty (`WithExpectedRowCount`) | 4,528 us | 5,131 us (SD 1,049 us) |

With the list pre-sized the unhinted case matches the hinted one, so the growth is the whole
penalty. Why the same Gen2 costs 56 us on SQL Server and 3.7 ms on PostgreSQL was not isolated;
the network drivers and their buffers are the difference, and it does not change the decision.

## The options that were rejected

- **Raise the default** to 1,024 or similar: every one-row query pays a bigger first array, and
  a 10,000-row read still doubles past the line. It taxes the common case and fixes nothing.
- **Grow to just under the threshold** (10,624 slots) when the next doubling would cross it:
  Jaunty would own its list growth instead of `List<T>`, it helps only reads between 8,192 and
  10,624 rows, and it would be tuned to the benchmark's row count.
- **Set the default to 10,000**: the probe number, and the wrong default for a library whose most
  common query returns one row.

## What this depends on

Nothing in code. The hint already exists, costs the caller one argument, and every library read
path honours it: `QueryCore`, `GridReader.ReadCore`/`ReadAsyncCore`, `GetAllCore`.

## Where the code is

`src/Jaunty/Configuration/JauntyConfig.cs`, `QueryResultCapacity`, whose doc comment points here.
The full-run log is `tmp/bench-full-2026-09-02.log` and the probe log
`tmp/bench-pg-capacity-probe.log` in the private tree; the numbers above are copied from them.
