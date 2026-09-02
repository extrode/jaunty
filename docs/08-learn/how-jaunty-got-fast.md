# How Jaunty got fast

The read path went from 1.80x slower than a hand-coded ADO.NET loop to 1.42x faster than it,
in five steps over two months, and then the loop itself turned out to be wrong and was fixed,
which put the finish line back where it belongs: a careful hand-written loop is still the
floor, and Jaunty is the closest of the five libraries to it. This is the record of each step:
what was slow, how it was found, what the code looked like before and after, and what it
measured. Every number here came from a run you can repeat; the commands are at the end.

Two things run through the whole story. **Correctness came first every time**, and twice a
correctness fix was the thing that made Jaunty slow. And **nothing was fixed from reasoning
alone**: each step started with a benchmark that disagreed with expectations and a probe that
isolated one call.

## Where it started: 1.80x slower

`Query<T>` hands each row to a mapper the source generator wrote for the entity. On 2026-07-03
the generator gained shape-safety validation: before reading a row it checked that every
column the entity expects is present, by name, case-insensitively. That closed a real bug,
a NULL that had been mapped as 0, and it made the first benchmark run look like this on SQLite:

| Rows | Jaunty vs hand-coded ADO.NET | Dapper |
|---|---|---|
| 100 | 1.47x slower | 1.07x |
| 1,000 | 1.99x slower | 1.03x |
| 10,000 | 1.80x slower | 1.12x |

The validation was correct. It also ran on every row.

## Step 1: validate the shape once per result set

`ReadEntity(reader)` was the only mapper, and it validated then read. The fix added a second
entry point, `CreateRowMapper(reader)`, a factory that validates once and returns a closure
that only reads:

```csharp
// before: every row
public static Product ReadEntity(IDataReader reader)
{
    var ord = OrdinalMap.Resolve(reader);        // GetName + compare, per column, per row
    ...
}

// after: once per result set
public static Func<IDataReader, Product> CreateRowMapper(IDataReader reader)
{
    var ord = OrdinalMap.Resolve(reader);        // validated here, once
    return r => { /* reads only, using ord */ };
}
```

The dispatcher prefers the factory, and a per-row `FieldCount` guard falls back to the
validating `ReadEntity` if a stale delegate ever meets a changed shape. 10,000 rows on SQLite
went from 1.80x to 1.46x, and at 1,000 rows the absolute time dropped 35%.

## Step 2: typed getters instead of `GetFieldValue<T>`

The generated read used `reader.GetFieldValue<int>(ord[0])` for every column. Inside a
provider that is a generic dispatch: a type check, sometimes a box, then the typed getter.
The generator now emits the typed getter directly for the eleven types every provider
implements, keeping `GetFieldValue<T>` only for the fallback types:

```csharp
// before
entity.ProductId = rr.GetFieldValue<int>(ord[0]);
entity.ProductName = rr.GetFieldValue<string>(ord[1]);

// after
entity.ProductId = rr.GetInt32(ord[0]);
entity.ProductName = rr.GetString(ord[1]);
```

Same day, same machine:

| Rows | Start | After step 1 | After step 2 | Dapper |
|---|---|---|---|---|
| 100 | 1.47x | 1.31x | 1.17x | 1.11x |
| 1,000 | 1.99x | 1.33x | 1.37x | 1.37x |
| 10,000 | 1.80x | 1.46x | **1.03x** | 1.00x |

At 10,000 rows Jaunty was at parity with the hand-coded loop and with Dapper on SQLite, and
ahead of Dapper on MariaDB (4,479 us against 6,797 us).

## Step 3: tell the list how big it will be

The pre-July change that mattered most was the smallest. `Query<T>` collects rows into a
`List<T>`, and a list that starts at the default capacity doubles nine times on the way to
10,000 rows. The last three arrays are over 85 KB and land on the large object heap.
`CommandOptions<T>.WithExpectedRowCount(n)` sizes the list once:

```csharp
int capacity = options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity;   // 64 unless hinted
var results = new List<T>(capacity);
```

On 2026-07-29, at 10,000 rows, the hint was the difference between 1.77x and 1.05x on
PostgreSQL, and it made Jaunty the fastest micro-ORM measured on SQL Server, PostgreSQL and
MariaDB. A hand-coded loop that knows its row count would do the same thing, and the benchmark
baseline does.

## The number that did not fit

The 2026-07-29 regression check showed no regression and one oddity. On SQLite at 10,000 rows
RepoDb was 1.37x **faster than the hand-coded ADO.NET loop**, and Dapper was 1.02x faster. A
library that adds work cannot beat a loop that does none, unless the loop is doing something
the library is not. The report said so and moved on. On 2026-09-02 it was looked at.

Three reviewers were given the benchmark source and asked why. Two said the baseline's
`GetDecimal` was the cost. One said `GetDecimal` on Microsoft.Data.Sqlite is `GetDouble` plus
a cast and the baseline was fair. A probe settled it in a minute. The `unit_price` column is
SQLite `REAL`, a double, and Microsoft.Data.Sqlite implements `GetDecimal` on it as
`sqlite3_column_text` followed by `decimal.Parse`:

| Reader pattern, 10,000 rows, 5 columns, warm | us per run |
|---|---|
| The loop with no getters at all | 1,210 |
| `GetDouble` on the price column alone | 1,762 |
| `GetDecimal` on the price column alone | 4,763 |
| Boxed `GetValue` then `Convert.ToDecimal`, Dapper's path | 7,232 |
| The benchmark baseline as written | 11,805 |
| The same loop with `(decimal)GetDouble` | 4,564 |

So RepoDb was not faster than ADO.NET. It was faster than a loop that formatted every price to
text and parsed it back. Jaunty's generated mapper made the same call, because a `decimal`
property gets `GetDecimal`, and it paid the same 3 ms.

The same probe, with `IsDBNull` before each getter and without:

| 5 typed getters, 10,000 rows | us per run |
|---|---|
| With `IsDBNull` before each non-nullable column | 6,215 |
| Without | 4,448 |

Every reader call on Microsoft.Data.Sqlite is a native call. The generated mapper made nine per
row on a five-column entity: five reads and four null checks.

## Step 4: read a double as a double, and stop pre-checking for NULL

The generator decides once per result set, when it resolves the ordinals, whether a decimal
property's column reports `double` from `GetFieldType`. SQLite `REAL` does; a `decimal` column
on SQL Server, PostgreSQL or MariaDB does not, and keeps `GetDecimal`. The flag is a `bool[]`
next to the ordinals, and the read becomes a ternary on it.

The NULL check moved from before each read to after a failure. Since 2026-08-03 the mapper has
thrown `Cannot assign NULL to non-nullable property 'X'.` for a NULL in a non-nullable value
type, matching the reflection mapper. It did that with an `IsDBNull` ahead of every such
column. Every supported provider's typed getter already throws on NULL, so the reads now sit
in one `try` per row, and the `catch` walks the non-nullable ordinals to name the column. A
`try` region costs nothing until something throws. The guarantee is unchanged, the message is
unchanged, and the provider's exception rides along as `InnerException`. Decision 010 in
`docs/decisions/` records why this must not be reverted.

The emitted mapper for the benchmark entity, before and after:

```csharp
// before
var ord = OrdinalMap.Resolve(reader);
return r =>
{
    var rr = (DbDataReader)r;
    var entity = new JauntyProduct();
    if (rr.IsDBNull(ord[0])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'ProductId'.");
    entity.ProductId = rr.GetInt32(ord[0]);
    if (!rr.IsDBNull(ord[1])) entity.ProductName = rr.GetString(ord[1]);
    if (rr.IsDBNull(ord[2])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'UnitPrice'.");
    entity.UnitPrice = rr.GetDecimal(ord[2]);
    if (rr.IsDBNull(ord[3])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'UnitsInStock'.");
    entity.UnitsInStock = rr.GetInt32(ord[3]);
    if (rr.IsDBNull(ord[4])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'Discontinued'.");
    entity.Discontinued = rr.GetBoolean(ord[4]);
    return entity;
};
```

```csharp
// after
var entry = OrdinalMap.Resolve(reader);
var ord = entry.Ordinals;
var __real = entry.DecimalAsDouble;          // reader.GetFieldType(ord[i]) == typeof(double), once
return r =>
{
    var rr = (DbDataReader)r;
    var entity = new JauntyProduct();
    try
    {
        entity.ProductId = rr.GetInt32(ord[0]);
        if (!rr.IsDBNull(ord[1])) entity.ProductName = rr.GetString(ord[1]);
        entity.UnitPrice = __real[2] ? (decimal)rr.GetDouble(ord[2]) : rr.GetDecimal(ord[2]);
        entity.UnitsInStock = rr.GetInt32(ord[3]);
        entity.Discontinued = rr.GetBoolean(ord[4]);
    }
    catch (Exception ex)
    {
        ThrowIfNonNullableColumnIsNull(rr, ord, ex);
        throw;
    }
    return entity;
};

private static void ThrowIfNonNullableColumnIsNull(IDataReader reader, int[] ord, Exception inner)
{
    if (reader.IsDBNull(ord[0])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'ProductId'.", inner);
    if (reader.IsDBNull(ord[2])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'UnitPrice'.", inner);
    if (reader.IsDBNull(ord[3])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'UnitsInStock'.", inner);
    if (reader.IsDBNull(ord[4])) throw new InvalidOperationException("Cannot assign NULL to non-nullable property 'Discontinued'.", inner);
}
```

Nine reader calls per row became five. Is the cast exact? SQLite formats a `REAL` with 15
significant digits, and that is the rounding the `decimal(double)` constructor applies, so the
two paths agree on every value tried, including 0.1, 1/3, 2.675, 1e22 and 12345.678901234567.
Only the scale can differ: `0.0000001` where the text path gave `0.00000010`. A test compares
both paths against a live SQLite reader for each of those values.

## Step 5: hand a custom mapper back as it was given

`CommandOptions<T>.WithMapper(Func<IDataReader, T>)` lets you write the row mapping yourself.
`Func<in T, out TResult>` is contravariant in its argument, so that delegate already is a
`Func<DbDataReader, T>`. The dispatcher wrapped it in a lambda anyway:

```csharp
// before: a closure per query, two delegate calls per row
if (options.Mapper is not null)
    return dbReader => options.Mapper(dbReader);

// after
if (options.Mapper is not null)
    return options.Mapper;
```

Nanoseconds per row, not milliseconds. A test asserts the delegate that comes back is the same
instance that went in.

## Where it is now

SQLite, 10,000 rows, warm, quiet machine, 2026-09-02. In these two runs the baseline still
calls `GetDecimal`, so that the before and after of step 4 are against the same loop; the
corrected baseline follows below. Two runs are quoted because a single BenchmarkDotNet run on
a laptop is not a measurement.

| Method | Run 1 | Run 2 | vs baseline (run 2) |
|---|---|---|---|
| ADO.NET (hand-coded, `GetDecimal`) | 6.536 ms | 6.929 ms | baseline |
| **Jaunty `Query<T>`** | 4.924 ms | 4.887 ms | **1.42x faster** |
| Jaunty (`WithExpectedRowCount`) | 4.211 ms | 4.759 ms | 1.46x faster |
| RepoDb | 4.951 ms | 5.105 ms | 1.36x faster |
| Dapper | 6.712 ms | 6.879 ms | 1.01x faster |
| linq2db | 7.311 ms | | 1.06x slower |
| EF Core | 9.589 ms | | 1.38x slower |

Jaunty and RepoDb are within each other's error bars. Jaunty led in both runs, by margins that
a third run could reverse, so the claim is parity. Before this week the same case read
9.205 ms against RepoDb's 4.627 ms.

### The custom mapper, side by side

A custom mapper is your code, so the generated mapper's tricks do not reach into it. The
benchmark carries four variants to show what each one is worth. All read the same five columns
with the same typed getters; they differ only in the price getter and the list hint.

Same machine, same day, a third run with all four in it. That run put the baseline at
6.835 ms, Jaunty `Query<T>` at 4.910 ms and RepoDb at 5.095 ms.

| Variant | Price read | List hint | Time | vs baseline | Allocated |
|---|---|---|---|---|---|
| custom mapper | `GetDecimal(2)` | none, grows from 64 | 8.789 ms | 1.29x slower | 1.55 MB |
| custom mapper, `WithExpectedRowCount` | `GetDecimal(2)` | 10,000 | 6.539 ms | 1.05x faster | 1.37 MB |
| custom mapper, `GetDouble` | `(decimal)GetDouble(2)` | none | 4.972 ms | 1.38x faster | 1.24 MB |
| custom mapper, `GetDouble`, `WithExpectedRowCount` | `(decimal)GetDouble(2)` | 10,000 | 4.766 ms | 1.44x faster | 1.07 MB |

The first row had the widest error bar of the run (0.82 ms standard deviation against 0.04 to
0.43 for the others), so treat it as roughly 7 to 9 ms. Read down the rest: the getter is
worth about 2 ms, the hint about 0.2 ms and 300 KB, and with both a custom mapper lands where
the generated one does. The generated mapper gets the getter for free, because it picks it from
the column's reported type; the hint is yours to pass either way.

### With the baseline corrected

The runs above compare against the July baseline so that the before and after are the same
loop. The baseline was then fixed to read the price as the type the column reports, RepoDb's
SQLite-only bool workaround was confined to SQLite, and the warm job went from 5 to 15
iterations. The full four-provider run on that harness is
[benchmarks-2026-09-02.md](../05-quality/reports/benchmarks-2026-09-02.md), and it is what the
README quotes. On that harness, SQLite at 10,000 rows measured alone: the hand-coded loop 4.08 ms, Jaunty
`Query<T>` 5.42 ms, RepoDb 5.77 ms, Dapper 7.45 ms. So the honest sentence is this: Jaunty is
the fastest of the five libraries measured on SQLite and SQL Server, level with RepoDb on the
others, and a hand-written loop that reads each column as its reported type is still 1.3x
faster than any of them on SQLite. The next 1 ms is known: a per-row `FieldCount` guard and an
`IsDBNull` on the nullable string column, each one native call per row.

## What the story says about measuring

- **A library faster than the baseline is a defect in the baseline.** The July report noticed
  and deferred. It was cheap to check and should have been checked then.
- **Reason from the probe, not from the source.** Three reviewers reading the same provider
  source split 2-1 on what `GetDecimal` does. Ten lines of C# and 10,000 rows answered it.
- **Count the native calls.** On an in-process provider the reader call is the unit of cost.
  Nine per row to five per row was the whole of step 4.
- **Correctness fixes carry a cost, and the cost is not the fix.** Shape validation and the
  NULL guard were both right. Both were paid per row when once per result set, or only on
  failure, bought the same guarantee.

## Repeating the numbers

```bash
cd benchmarks/Jaunty.Benchmarks
dotnet run -c Release -f net10.0 -- --filter "*.Benchmarks.QueryBenchmarks.*"
```

The full parameter set runs 1, 100 and 10,000 rows on SQLite, SQL Server, PostgreSQL and
MariaDB; the server providers need the `docker-compose.yml` containers and a local SQL Server.
Edit the two `[Params]` attributes in `QueryBenchmarks.cs` to narrow a run.

The earlier reports are [BENCHMARKS-2026-07-04.md](../05-quality/reports/BENCHMARKS-2026-07-04.md)
and [benchmarks-2026-07-29.md](../05-quality/reports/benchmarks-2026-07-29.md). The
performance rules the code is written to are in
[performance-spec.md](../02-architecture/performance-spec.md).
