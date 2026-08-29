# Write Methods

## Overview

Jaunty's write surface is four operations - insert, update, delete, upsert - each with a
single-entity form and a bulk form, each with a synchronous and an asynchronous variant, and each
with and without `CommandOptions`.

| Family | Single entity | Bulk | Constraint-bypassing bulk |
|---|---|---|---|
| Insert | `Insert<T>` | `BulkInsert<T>` | `BulkInsertIgnoreConstraints<T>` |
| Update | `Update<T>` | `BulkUpdate<T>` | `BulkUpdateIgnoreConstraints<T>` |
| Delete | `Delete<T>` | `BulkDelete<T>` | `BulkDeleteIgnoreConstraints<T>` |
| Upsert | `Upsert<T>` | (none) | (none) |

Every one of these has an `Async` twin returning `ValueTask<...>` and taking a trailing
`CancellationToken cancellationToken = default`.

Overload-by-overload documentation for the single-entity insert, update and delete methods is in
[`crud-operations.md`](crud-operations.md). This page is the family reference: return values,
bulk routing, transaction behaviour, and the parts of the surface that page does not cover.

---

## Return values

| Method | Returns | Meaning |
|---|---|---|
| `Insert<T>` | `long` | see below |
| `Update<T>` | `int` | rows affected |
| `Delete<T>` | `int` | rows affected |
| `Upsert<T>` | `int` | rows affected, provider-defined |
| `BulkInsert<T>` | `int` | rows inserted |
| `BulkUpdate<T>` | `int` | rows updated |
| `BulkDelete<T>` | `int` | rows deleted |

`Insert<T>` returns two different things depending on the entity:

| Entity | Return | Side effect |
|---|---|---|
| Has an identity key | the generated key | the key is written back onto the entity's id property |
| Has an identity key, but the provider returned no key | 0 | none; an id the caller already set is not clobbered |
| Has no identity key | rows affected, from `ExecuteNonQuery` | none |

A negative identity is ordinary and is returned and written back like any other. SQL Server's
standard wide-range seed is `IDENTITY(-2147483648, 1)`, so treating "not positive" as "no key" is
wrong (AUD-R35-133).

`Upsert` is the one to be careful with. MySQL and MariaDB report **2** for a row that was updated
rather than inserted, so a test for `== 1` fails against those providers on a path that worked.
Check `result > 0`.

---

## Single-entity methods

```csharp
public static long Insert<T>(this IDbConnection connection, T entity) where T : new();
public static long Insert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new();

public static int Update<T>(this IDbConnection connection, T entity) where T : new();
public static int Update<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new();

public static int Delete<T>(this IDbConnection connection, T entity) where T : new();
public static int Delete<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new();
public static int Delete<T>(this IDbConnection connection, object id) where T : new();
public static int Delete<T>(this IDbConnection connection, object id, CommandOptions options) where T : new();
public static int Delete<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new();
public static int Delete<T, TId>(this IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>, new();
```

`Delete` has three shapes. The `object id` overload boxes the key; the `Delete<T, TId>` overload
avoids the box but constrains `T` to `IEntity<TId>`. Prefer the two-parameter generic form where
the entity implements the interface.

Note that these take the **non-generic** `CommandOptions`, not `CommandOptions<T>`. A write has no
result set to map, so there is no `Mapper` and no `ExpectedRowCount` to carry. The implicit
conversion from `CommandOptions<T>` compiles and silently drops both;
[`command-options.md`](command-options.md#the-implicit-conversion-loses-two-fields) says more.

## Upsert

```csharp
public static int Upsert<T>(this IDbConnection connection, T entity) where T : new();
public static int Upsert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new();
public static ValueTask<int> UpsertAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : new();
public static ValueTask<int> UpsertAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : new();
```

The primary key on the entity decides: a row with that key is updated, otherwise one is inserted.
Jaunty emits the dialect's native form - `MERGE` on SQL Server, `INSERT ... ON CONFLICT` on
PostgreSQL and SQLite, `INSERT ... ON DUPLICATE KEY UPDATE` on MySQL.

`InvalidOperationException` is thrown when the entity has no primary key, when the dialect does
not support upsert, or when there are no upsertable columns.

```csharp
var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
int affected = connection.Upsert(product);   // inserts, then updates on a second call
```

There is no `BulkUpsert`. Loop over `Upsert`, or use `BulkInsertIgnoreConstraints` into a staging
table and merge.

---

## Bulk methods

```csharp
public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();
public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();
public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();
public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();
```

`BulkUpdate`, `BulkDelete` and their `IgnoreConstraints` and `Async` forms have the identical
shape. An empty collection returns 0 without opening the connection. A `null` element throws
`ArgumentException` naming the index.

### Transactions

A bulk method runs inside a transaction whether or not you supply one:

- **You supplied `options.Transaction`** - Jaunty uses it and does not commit. Commit or roll back
  yourself.
- **You did not** - Jaunty calls `BeginTransaction()`, commits on success, rolls back on any
  exception, and disposes the transaction it created.

The connection is treated the same way: opened if closed, and closed again afterwards only if
Jaunty was the one that opened it.

### How BulkInsert routes

`BulkInsert` picks one of three paths, and which one it picks changes what you get back:

| Path | Chosen when | Identity write-back |
|---|---|---|
| Native bulk copy | `BulkCopyConfiguration.EnableNativeBulkCopy` and the dialect supports it and `entities.Count >= BulkCopyConfiguration.MinimumRowsForNativeBulkCopy` (default 100) | no |
| Multi-row `VALUES` | the dialect supports multi-row insert, more than one entity, and the dialect is not SQLite | no |
| Prepared loop | everything else, including every SQLite bulk insert | **yes** |

Only the loop populates the identity property back onto each entity, because there is no
provider-agnostic way to map a single "last inserted id" onto individual rows of a batched
statement. If you need populated IDs, call single-row `Insert` in a loop, or read the rows back.

SQLite is deliberately excluded from the multi-row path: `Microsoft.Data.Sqlite` binds multi-row
`VALUES` parameters quadratically, measured about 16x slower than the prepared loop (PROD-120).

`BulkUpdate` and `BulkDelete` have no batched path at all. Each issues one round trip per entity
in a sequential loop inside the transaction, so for large collections the round-trip count is the
dominant cost.

### Constraints

**Plain `BulkInsert`, `BulkUpdate` and `BulkDelete` enforce CHECK and FOREIGN KEY constraints on
every route.** That has not always been true: on SQL Server above the native-bulk-copy threshold,
`BulkInsert` used to reach `SqlBulkCopy` without its `CheckConstraints` option, so the same call
validated at 50 rows and did not at 50,000 (AUD-R26). It now validates at both.

To bypass validation, say so:

- Per call - `BulkInsertIgnoreConstraints` and its two siblings.
- Globally - `BulkCopyConfiguration.DefaultCheckConstraints = false`.

The bypass mechanism differs by size, and the two are not guaranteed equivalent:

| Dataset | Mechanism | Failure mode |
|---|---|---|
| At or above the native threshold | the provider's bulk-copy `CheckConstraints` option | none |
| Below the native threshold | session-level foreign-key toggle SQL | `NotSupportedException` on providers with no session-level toggle, including SQL Server |

So `BulkInsertIgnoreConstraints` on SQL Server succeeds with 200 rows and throws with 50. That is
the documented behaviour, not a defect, but it makes the method size-sensitive in a way the plain
form is not.

### Configuration

`BulkCopyConfiguration` (static, in `Jaunty.Configuration`):

| Property | Default | Notes |
|---|---|---|
| `DefaultBatchSize` | 10000 | throws `ArgumentOutOfRangeException` when set to 0 or less |
| `DefaultTimeout` | 30 | seconds; 0 means no timeout; negative throws |
| `DefaultIdentityMode` | `BulkCopyIdentityMode.Default` | undefined enum values throw |
| `DefaultCheckConstraints` | `true` | `false` restores the pre-AUD-R26 bypass globally |
| `MinimumRowsForNativeBulkCopy` | 100 | 0 means always take the native path where one exists; negative throws |
| `EnableNativeBulkCopy` | `true` | `false` forces standard INSERT everywhere |

`BulkCopyConfiguration.Reset()` restores all six. These throw on bad input rather than clamping,
unlike `JauntyConfig`'s capacity hints: a batch size or a timeout is an instruction to the
database, and quietly substituting a different one is how a misconfiguration reaches production
looking as though it took effect.

Native bulk copy itself - `SqlBulkCopy`, PostgreSQL `COPY`, the MySQL chunked writer - is covered
in [`bulk-copy-methods.md`](bulk-copy-methods.md).

---

## Observability

A bulk operation reports **one** interceptor event, not one per row. A 100,000-row `BulkUpdate` is
one logical write; firing the pipeline per statement would swamp an auditor and cost more than the
bulk path saves. The event carries a `BulkOperationParameters` payload with the operation name,
entity type and row count.

The foreign-key toggle issued by the `IgnoreConstraints` methods goes to `JauntyConfig.Logger` and
deliberately not to the interceptor pipeline, for the same reason.

---

## See Also

- [`crud-operations.md`](crud-operations.md) - per-overload reference for `Insert`, `Update`, `Delete`
- [`bulk-copy-methods.md`](bulk-copy-methods.md) - the native bulk-copy providers
- [`get-and-execute-operations.md`](get-and-execute-operations.md) - `Get`, `GetAll`, `Execute`, `ExecuteBatch`
- [`command-options.md`](command-options.md) - transactions, timeouts, command type
- [`attributes.md`](attributes.md) - `[Key]`, `[DatabaseGenerated]`, `[Column]`, `[Ignore]`
