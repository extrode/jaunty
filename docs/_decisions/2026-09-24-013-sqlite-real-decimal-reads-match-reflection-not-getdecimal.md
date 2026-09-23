# 013 — A decimal read from SQLite REAL matches the reflection mapper, not GetDecimal

**Date:** 2026-09-24
**Status:** Accepted

## Decision

**The generated mapper keeps reading a decimal property on a double-typed column as
`(decimal)reader.GetDouble(i)`.** The value it must agree with is the reflection mapper's, whose
terminal conversion is `Convert.ChangeType(boxedDouble, typeof(decimal), InvariantCulture)`.
Both round to 15 significant digits. Microsoft.Data.Sqlite's `GetDecimal` is not the reference,
and a future dependency bump that moves it must not reopen this.

## Why

The 2026-09-02 fast path (see `docs/08-learn/how-jaunty-got-fast.md`, step 4) assumed SQLite
formatted REAL with 15 significant digits, so the cast and `GetDecimal` agreed. SQLitePCLRaw
3.0.5 bundles SQLite 3.53.4, which formats with up to 17. On it, the stored double
1234567890.123456 reads as 1234567890.123456 through `GetDecimal` and 1234567890.12346 through the
cast, and 1234568138.793456 reads as 1234568138.7934561 through `GetDecimal`: the 17th digit is
binary-representation noise, not stored precision.

Measured 2026-09-24 on SQLite 3.53.4, net10.0, 10,000 rows, REAL column, best of 7:

| Read | us per 10k rows | Agrees with `GetDecimal` |
|---|---|---|
| No getter | 1,187-1,419 | n/a |
| `(decimal)GetDouble` | 2,038-2,247 | no, past 15 digits |
| `GetDouble` + stackalloc `TryFormat("R")` + `decimal.Parse` | 2,933 | no, 882 of 10,000 rows |
| `GetDouble` + G15, else G17 | 4,304 | no, 77 of 10,000 rows |
| `GetDecimal` | 3,653-3,990 | yes |

No read was both exact against `GetDecimal` and faster than calling it.

## The options that were rejected

- **Always `GetDecimal`**: parity with the provider, about 1.7x slower on the column, and it
  would make the generated mapper the only Jaunty read path (reflection, fluent, grid, scalar all
  go through `Convert.ChangeType`) that returns 17-digit values. Referencing the generator package
  should not change what a query returns; JAUNTYGEN005 guards the same promise for which
  properties get mapped.
- **Emulate SQLite's formatting**: slower than `GetDecimal` and still wrong on some rows, and it
  would track whatever the bundled SQLite ships next.

## What this depends on

Decimals stored as REAL. A path that stores decimals as TEXT and relies on reading them back
exactly would need `GetDecimal`; there is none today, and such a column reports `string`, not
`double`, so the fast path does not apply to it.

Hand-written mappers (`WithMapper`, `IMapped<T>`) that call `GetDecimal` on REAL will differ from
Jaunty's own reads past the 15th digit on SQLite 3.53+.

## Where the code is

`src/Extrode.Jaunty.SourceGenerator/JauntyGenerator.cs`, `RealColumnsLocal`, whose remark points
here. Test: `GeneratedDecimalReadTests.ASqliteRealColumn_MapsTheSameValueTheReflectionMapperWould`.
