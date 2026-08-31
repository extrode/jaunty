# Migrating from Dapper

Dapper is a good library and this guide does not pretend otherwise. If your data layer is not causing
you problems, the honest advice is to stay. The reasons people move are specific: strict mapping,
NativeAOT without a separate package, zero runtime dependencies, and bulk copy in the box.

**Read [strict mapping](strict-mapping.md) before you start.** It is the only behavioural difference
that matters and everything else on this page is mechanical.

## The shape is the same

Jaunty is extension methods on `IDbConnection`, like Dapper. There is no context, no registration,
and no ownership of the connection. **You can run both in the same method against the same
connection**, which is what makes an incremental port possible.

One caveat on mixing them: both libraries define `Query<T>` as an extension on `IDbConnection`, so a
file with `using Dapper;` and `using Jaunty;` together gets ambiguous-call errors. **Port file by
file, not line by line** - swap the `using` and convert that whole file. Different files in the same
project, and different methods on the same connection, are fine.

## Method mapping

**The important row is the first one.** Dapper's `Query<T>` is lenient, so its direct behavioural
equivalent is `QueryPartial<T>`, not `Query<T>`.

| Dapper | Jaunty (same behaviour) | Jaunty (stricter, usually what you want) |
|---|---|---|
| `Query<T>(sql, param)` | `QueryPartial<T>(sql, param)` | `Query<T>(sql, param)` |
| `QueryFirst<T>` | `QueryPartialFirst<T>` | `QueryFirst<T>` |
| `QueryFirstOrDefault<T>` | `QueryPartialFirstOrDefault<T>` | `QueryFirstOrDefault<T>` |
| `QuerySingle<T>` | `QueryPartialSingle<T>` | `QuerySingle<T>` |
| `QuerySingleOrDefault<T>` | `QueryPartialSingleOrDefault<T>` | `QuerySingleOrDefault<T>` |
| `Execute(sql, param)` | `Execute(sql, param)` | - |
| `ExecuteScalar<T>(sql, param)` | `ExecuteScalar<T>` or `QueryScalar<T>` | - |
| `QueryMultiple` | `QueryMultiple` | - |
| `Query<T>(..., buffered: false)` | `GetAllStream<T>` / `GetAllStreamAsync<T>` | - |

Every method has an `...Async` counterpart. Jaunty's async methods return `ValueTask<T>` rather than
`Task<T>`; `await` works the same, but if you are storing the result in a variable or passing it to
`Task.WhenAll`, call `.AsTask()`.

**Parameters are unchanged.** Anonymous objects work the same way:

```csharp
connection.Query<Product>("SELECT * FROM products WHERE price > @MinPrice", new { MinPrice = 100 });
```

## Coming from Dapper.Contrib

Contrib's CRUD helpers have direct equivalents built into Jaunty core - no extra package.

| Dapper.Contrib | Jaunty |
|---|---|
| `Get<T>(id)` | `Get<T>(id)`, or `GetRequired<T>(id)` to throw instead of returning null |
| `GetAll<T>()` | `GetAll<T>()` |
| `Insert(entity)` | `Insert(entity)` - returns `long` |
| `Update(entity)` | `Update(entity)` |
| `Delete(entity)` | `Delete(entity)` |

Jaunty adds `Upsert`, and `BulkInsert` / `BulkUpdate` / `BulkDelete`.

**Attributes.** `[Table]`, `[Key]`, `[Column]`, `[Ignore]` and `[DatabaseGenerated]` live in
`Jaunty.Attributes`. `[NotMapped]` from `System.ComponentModel.DataAnnotations.Schema` is also
honoured, so entities annotated for EF Core mostly work as they are.

## Coming from Dapper Plus

`BulkInsert`, `BulkUpdate` and `BulkDelete` are in Jaunty core across SQL Server, PostgreSQL and
MySQL, and cost nothing. There is also an `...IgnoreConstraints` variant of each.

The APIs are not identical - Dapper Plus has a large fluent configuration surface that Jaunty does
not attempt to match. Check [bulk copy methods](../../01-api-reference/bulk-copy-methods.md) for what
is actually supported before assuming a per-option equivalence.

## Multi-mapping and multiple result sets

Dapper's `splitOn` multi-mapping has an equivalent, with a different shape - see
[multi-entity mapping](../../01-api-reference/multi-entity-mapping.md). `QueryMultiple` and its grid
reader are covered in [multiple result sets](../../01-api-reference/multiple-result-sets.md).

**Port these last.** They are the least mechanical part of the move and the easiest to get subtly
wrong while you are still learning the strict/partial distinction.

## What you gain, concretely

- **A silent bug class becomes an exception.** [Strict mapping](strict-mapping.md)
- **NativeAOT works with no extra package and no runtime reflection in core.** Dapper needs
  Dapper.AOT for this; Jaunty's source generator is in the box
- **Zero runtime dependencies** on net8.0 and net10.0
- **Bulk copy, a scaffolding CLI, DuckDB and flat-file querying, and four dialects**, all included

## What you give up

- **Ecosystem.** Fifteen years of Stack Overflow answers, blog posts and third-party extensions are
  about Dapper. This is a real cost and it is the main reason not to move
- **Team familiarity.** Everyone you hire knows Dapper
- **An OSI-approved licence.** Jaunty is under ISL-R, which is not OSI-approved and not SPDX-listed.
  If your organisation has an approved-licence list, check it before you start the port, not after

## A suggested order

1. Pick the smallest repository or data-access class you have
2. Replace `using Dapper;` with `using Jaunty;` and switch every call to the `QueryPartial` family -
   behaviour is now identical
3. Run your tests. Anything failing here is a genuine port issue, not a mapping-strictness issue
4. Promote full-entity and per-query-DTO calls to the strict family, one file at a time
5. Fix what it finds. That step is the point of the exercise
6. Only then look at multi-mapping, `QueryMultiple`, and bulk operations
