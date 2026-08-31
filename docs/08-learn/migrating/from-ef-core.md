# Migrating from EF Core

**Start here: most teams should not do this, and the ones who should usually should not do all of
it.** EF Core does things Jaunty deliberately does not, and the recommended architecture in 2026 -
EF Core for the domain, a micro-ORM for hot reads - is recommended because it is right.

This guide is about moving the *read* half, and about knowing which half that is.

## What you lose, in full

None of these have Jaunty equivalents, because Jaunty is not that kind of library:

| EF Core feature | Jaunty |
|---|---|
| Change tracking | None. You write the UPDATE |
| Unit of work / `SaveChanges` | None. You control the transaction |
| Lazy loading and navigation fixup | None. You write the join |
| Migrations | None. (JauntyQ has migration intelligence; that is a separate product) |
| LINQ translated to SQL | None, and this is the design. `Extrode.Jaunty.Fluent` builds SQL from typed expressions, but it does not translate arbitrary LINQ |
| Identity map | None. Two queries for the same row give you two objects |
| Model configuration API (`OnModelCreating`) | Attributes only: `[Table]`, `[Key]`, `[Column]`, `[Ignore]`, `[DatabaseGenerated]` |

**If any row in that table is doing real work in your codebase, keep EF Core for that part.** Nothing
here is a gap to be filled later; they are the things a micro-ORM is defined by not having.

## What you gain, and where

The move is worth it in three places specifically:

1. **Read-heavy paths where materialisation cost shows up in a profile.** No tracking, no identity
   map, no query pipeline
2. **NativeAOT or trimming.** EF Core's compiled-model story exists, but Jaunty is AOT-verified end
   to end with a source generator in the box and no runtime reflection in core
3. **Queries you already wrote in raw SQL** - `FromSqlRaw`, `ExecuteSqlRaw`, or a stored procedure.
   These are not benefiting from EF Core at all, and they are the natural first thing to move

**That third category is the easy win.** Find every `FromSqlRaw` and `ExecuteSqlRaw` in the codebase;
those calls are already outside EF Core's model and they port almost verbatim.

## They coexist without ceremony

Jaunty is extension methods on `IDbConnection`. EF Core hands you one:

```csharp
var connection = dbContext.Database.GetDbConnection();
var rows = connection.Query<OrderSummary>(
    "SELECT id, customer_id, total FROM order_summaries WHERE total > @Min",
    new { Min = 1000 });
```

There is no second connection, no second pool, and no registration. If you are inside an EF Core
transaction, pass it explicitly rather than assuming it is picked up:
`CommandOptions<T>.WithTransaction(tx)`.

**Consequence worth stating:** rows read through Jaunty are not tracked. They are plain objects. If
you read an entity with Jaunty, change it and call `SaveChanges`, nothing happens - EF Core never saw
it. Read with Jaunty, write with the same tool you read with, or write explicit SQL.

## Your entities mostly work as they are

`[NotMapped]`, `[Table]`, `[Key]` and `[Column]` from `System.ComponentModel.DataAnnotations.Schema`
are honoured, so entity classes annotated for EF Core usually need no changes.

**Navigation properties are the exception, and they are the thing that will throw.** A writable
`List<Order> Orders { get; set; }` has no matching column, so strict `Query<T>` rejects it. Mark
navigation properties `[Ignore]`, or - better for a read path - query a purpose-built DTO instead of
the tracked entity. See [strict mapping](strict-mapping.md).

## A suggested order

1. Port `FromSqlRaw` / `ExecuteSqlRaw` / stored procedure calls. Near-verbatim, and they gain the
   most
2. Port read-only endpoints that return DTOs - list views, reports, search. Write a DTO per query and
   use strict `Query<T>`
3. **Stop.** Keep EF Core for anything that writes through the change tracker, and for migrations

Step 3 is not a staging post. For most codebases it is the end state, and it is a good one.
