# 010 — The generated mapper names a NULL column by catching, not by pre-checking

**Date:** 2026-09-02
**Status:** Accepted
**Revises:** the mechanism behind audit finding AUD-R35-069 (2026-08-03). The guarantee that
finding introduced is unchanged.

## Decision

**The generated row mapper reads every non-nullable value-type column with an unguarded typed
getter, inside one `try` per row.** When any read throws, the `catch` walks the non-nullable
ordinals with `IsDBNull`, and the first NULL it finds throws
`InvalidOperationException("Cannot assign NULL to non-nullable property 'X'.")` with the
provider's exception as `InnerException`. If no column is NULL, the original exception is
rethrown untouched.

The per-column `if (reader.IsDBNull(ord[i])) throw ...` that AUD-R35-069 emitted ahead of each
read is gone, and **a future audit must not put it back**. The finding's registry entry, the
generator comment at the emission site, and this file all say so.

## Why

Both shapes produce the same exception, same message, same type. They differ in what a clean row
costs. On Microsoft.Data.Sqlite every reader call is a native call:

| 10,000 rows, 5 columns, warm | Time |
|---|---|
| Typed getters with `IsDBNull` before each non-nullable one | 6.2 ms |
| Typed getters alone | 4.4 ms |

The pre-check was 1.8 ms of a 6.2 ms read, paid on every row, to guard against a condition that
the typed getter already detects. A `try` region costs nothing until something throws.

On SqlClient, Npgsql and MySqlConnector `IsDBNull` is a bitmap lookup and the saving is small,
but the generated code is the same for every provider and the SQLite number decided it.

## What the reversal depends on

The typed getter must throw on NULL. Every provider Jaunty is built or tested against does,
measured 2026-09-02 with `SELECT CAST(NULL AS int)` and `GetInt32`, `GetDecimal`, `GetBoolean`
on each, at the package versions the test projects pin:

| Provider | `GetInt32` on NULL throws |
|---|---|
| Microsoft.Data.Sqlite 9 | `InvalidOperationException` |
| System.Data.SQLite 1.0.119 | `InvalidCastException` |
| Microsoft.Data.SqlClient 6.0.1 | `SqlNullValueException` |
| Npgsql 10.0.1 | `InvalidCastException` |
| MySql.Data 9.6.0 | `SqlNullValueException` (`GetBoolean`: `InvalidCastException`) |
| MySqlConnector 2.4.0 | `InvalidCastException` |
| DuckDB.NET 1.3.0 | `InvalidCastException` |

A provider whose getter returned `default` for NULL would silently assign 0 where the pre-check
threw. None of these does. If one is ever added that does, the fix is a per-provider pre-check
on that provider, not a return to pre-checking everywhere.

## What did not change

- The message, the exception type, and which property is named: the first non-nullable NULL in
  ordinal order, the same as before.
- Nullable value types and reference types keep `if (!IsDBNull) set`, the skip semantics from
  AUD-R33-009.
- The reflection mapper, which was already uniform and was the model the finding matched the
  generator to.

## Pinned by

- `tests/Jaunty.SourceGenerator.Tests/GeneratedNonNullableNullTests.cs`: the guarantee
  AUD-R35-069 introduced, unchanged.
- `tests/Jaunty.SourceGenerator.Tests/GeneratedNullDiagnosisTests.cs`: no `IsDBNull` on a
  non-nullable column of a clean row; a non-NULL getter failure propagates unchanged; the named
  error carries the provider exception as its cause. Restoring the pre-check turns 3 of these red.

## Where the code is

`src/Jaunty.SourceGenerator/JauntyGenerator.cs`: `AppendNullDiagnosisCatch`, the
`ThrowIfNonNullableColumnIsNull` emission, and the non-nullable arm of `AppendPropertyRead`.
The emitted shape and the numbers behind it are in
[How Jaunty got fast](../08-learn/how-jaunty-got-fast.md).
