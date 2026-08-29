# CommandOptions

## Overview

`CommandOptions` carries the per-call settings that are not the SQL and not the parameters: a
transaction, a timeout, a command type, and - on the generic form - a custom mapper and a
row-count hint.

There are two of them, both `readonly struct`:

| Type | Carries | Taken by |
|---|---|---|
| `CommandOptions<T>` | `Mapper`, `Transaction`, `CommandTimeout`, `CommandType`, `ExpectedRowCount` | methods that map rows to `T` |
| `CommandOptions` | `Transaction`, `CommandTimeout`, `CommandType` | writes, scalars, non-queries, `QueryPartialList`, multi-entity overloads |

Both are structs with a default value that means "nothing set", so `default` is always a valid
argument and every option is opt-in.

---

## Fields

### `CommandOptions<T>`

```csharp
public readonly struct CommandOptions<T>(
    Func<IDataReader, T>? mapper = null,
    IDbTransaction? transaction = null,
    int? commandTimeout = null,
    CommandType commandType = CommandType.Text,
    int? expectedRowCount = null)
```

| Field | Type | Default | Notes |
|---|---|---|---|
| `Mapper` | `Func<IDataReader, T>?` | `null` | wins over every other mapper; see below |
| `Transaction` | `IDbTransaction?` | `null` | must be a `DbTransaction` when the connection is a `DbConnection` |
| `CommandTimeout` | `int?` | `null` | seconds; 0 means no timeout; **negative throws** |
| `CommandType` | `CommandType` | `Text` | `StoredProcedure` or `TableDirect` |
| `ExpectedRowCount` | `int?` | `null` | pre-sizing hint; **normalised, never throws** |

### `CommandOptions`

```csharp
public readonly struct CommandOptions(
    IDbTransaction? transaction = null,
    int? commandTimeout = null,
    CommandType commandType = CommandType.Text)
```

Same three fields, same semantics.

---

## Factories

```csharp
CommandOptions<T>.WithMapper(Func<IDataReader, T> mapper)
CommandOptions<T>.WithTransaction(IDbTransaction transaction)
CommandOptions<T>.WithTimeout(int seconds)
CommandOptions<T>.AsStoredProcedure()
CommandOptions<T>.WithExpectedRowCount(int rowCount)
CommandOptions<T>.With(Func<IDataReader, T> mapper, IDbTransaction transaction, int timeoutSeconds)

CommandOptions.WithTransaction(IDbTransaction transaction)
CommandOptions.WithTimeout(int seconds)
CommandOptions.AsStoredProcedure()
CommandOptions.With(IDbTransaction transaction, int timeoutSeconds)
```

Each factory sets one field and leaves the rest at their defaults. They do not compose - there is
no `WithTransaction(tx).WithTimeout(30)`. To set several, use the constructor with named
arguments:

```csharp
var options = new CommandOptions<Product>(
    transaction: tx,
    commandTimeout: 30,
    expectedRowCount: 5_000);
```

---

## Transactions

```csharp
using var connection = new SqlConnection(connectionString);
connection.Open();
using var tx = connection.BeginTransaction();

connection.Insert(product, CommandOptions.WithTransaction(tx));
connection.Update(other,  CommandOptions.WithTransaction(tx));

tx.Commit();
```

Jaunty attaches the transaction to the command and does nothing else with it: it never commits or
rolls back a transaction you supplied. The exception is the bulk write family, which begins and
commits its own transaction **only when you did not supply one** - see
[`write-methods.md`](write-methods.md#transactions).

`IDbCommand.Transaction` is an explicit interface implementation on `DbCommand` that casts to
`DbTransaction` internally, so an `IDbTransaction` that is not a `DbTransaction` would throw an
opaque `InvalidCastException` from inside ADO.NET. Jaunty validates first and throws an
`ArgumentException` naming the problem instead.

## Timeouts

```csharp
connection.Query<Product>(sql, CommandOptions<Product>.WithTimeout(30));   // 30 seconds
connection.Query<Product>(sql, CommandOptions<Product>.WithTimeout(0));    // no timeout
```

A negative timeout throws `ArgumentOutOfRangeException` at the point you write it, not later from
inside command execution. Before AUD-R35-149 nothing checked, and `WithTimeout(-1)` surfaced as a
provider-specific exception with nothing naming the option that caused it.

Leaving `CommandTimeout` null uses the provider default.

## Command type

```csharp
var rows = connection.Query<Product>(
    "GetProductsByCategory",
    new { CategoryId = 1 },
    CommandOptions<Product>.AsStoredProcedure());
```

The dedicated stored-procedure methods are usually clearer; see
[`stored-procedures.md`](stored-procedures.md).

The bulk write methods reject anything but `Text` and say which method rejected it: a bulk path
builds its own statement, so a `StoredProcedure` command type there would be silently ignored.

## Custom mappers

`Mapper` is the first thing `DrDispatcher` checks, ahead of the source-generated mapper, the
special-type resolver and the reflection extension. It is the zero-reflection escape hatch:

```csharp
var options = CommandOptions<Product>.WithMapper(reader => new Product
{
    Id    = reader.GetInt32(0),
    Name  = reader.GetString(1),
    Price = reader.GetDecimal(2),
});

var products = connection.Query<Product>("SELECT id, name, price FROM products", options);
```

It is also the only way to run a partial-shaped query in an AOT build with no reflection
extension loaded - see
[`query-partial-methods.md`](query-partial-methods.md#partial-mapping-does-not-use-the-source-generated-mapper).

The delegate is called once per row and receives the live reader. Do not capture the reader.

## Expected row count

```csharp
var products = connection.Query<Product>(sql, CommandOptions<Product>.WithExpectedRowCount(10_000));
```

A pre-sizing hint for the result list, nothing more. Unlike the timeout it is normalised rather
than validated, so a bad hint can never fail the query it was meant to speed up:

| Value | Effect |
|---|---|
| 0 or negative | treated as no hint; falls back to `JauntyConfig.QueryResultCapacity` |
| above 1,048,576 | capped at 1,048,576, so a mistyped value cannot allocate its way to `OutOfMemoryException` before the first row is read |

The value you read back from `ExpectedRowCount` is the normalised one, not the one you passed.

The asymmetry with `CommandTimeout` is deliberate: a capacity hint only costs a reallocation if it
is wrong, while a timeout is an instruction to the database, and quietly substituting a different
one is how a misconfiguration reaches production looking as though it took effect.

---

## The implicit conversion loses two fields

`CommandOptions<T>` converts implicitly to `CommandOptions`, and back. Going from generic to
non-generic, **`Mapper` and `ExpectedRowCount` are dropped**, and because the conversion is
implicit there is no cast at the call site to say so:

```csharp
var options = CommandOptions<Product>.WithMapper(MapProduct);

connection.Query<Product>(sql, options);   // mapper used
connection.Execute(sql, options);          // compiles; mapper silently dropped
```

Neither field can be carried: `CommandOptions` has no member for either, and `Mapper` is typed on
`T`, so a non-generic target could not hold it even if a field were added. The conversion is left
implicit rather than made explicit because tightening it would be a source-breaking change to a
shipped API (AUD-R26-054).

Nothing can currently be lost that the destination could have used - every public API taking the
non-generic form is a scalar read, a non-query, a dictionary projection, or a multi-entity
overload that takes its `map` delegate as an explicit parameter. That is a property of today's API
surface, not a guarantee. **An API that maps entities must take `CommandOptions<T>`, never the
non-generic form.**

---

## See Also

- [`configuration.md`](configuration.md) - global settings, including `QueryResultCapacity`
- [`query-methods.md`](query-methods.md) - the overloads that take `CommandOptions<T>`
- [`write-methods.md`](write-methods.md) - the overloads that take `CommandOptions`
- [`stored-procedures.md`](stored-procedures.md) - the dedicated stored-procedure surface
