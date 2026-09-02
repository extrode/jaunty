# Exercises

These six exercises build directly on the SQLite database from
[`README.md`](README.md) in this folder — the `products` table, plus whatever you insert as
you go. Do them in order; each one leans on something the previous one set up. Every starter
snippet compiles as-is (aside from the marked `TODO`s), so you can paste it straight into the
same `Program.cs` you used for the tutorial and fill in the blanks.

## Exercise 1: Map a New Table with Attributes

**Goal:** Create a `categories` table and map it with `[Table]`, `[Column]`, `[Key]`, and
`[DatabaseGenerated]`, then insert a row and read it back.

**Starter code:**

```csharp
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = """
        CREATE TABLE categories (
            category_id INTEGER PRIMARY KEY AUTOINCREMENT,
            category_name TEXT NOT NULL,
            description TEXT
        );
        """;
    cmd.ExecuteNonQuery();
}

using Jaunty.Attributes;

// TODO: add [Table("categories")] to this class
public partial class Category
{
    // TODO: mark this the primary key and identity-generated,
    // and point it at the "category_id" column
    public int Id { get; set; }

    // TODO: point this at the "category_name" column
    public string Name { get; set; }

    // TODO: point this at the "description" column
    public string Description { get; set; }
}

var category = new Category { Name = "Beverages", Description = "Soft drinks, coffees, teas" };

// TODO: insert `category` and capture the generated id
// var newId = ...

// TODO: read it back with QueryPartial<Category> using the real column names
// var loaded = ...
```

**Hints:**
- The `[Key]` and `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` attributes go
  together on the same property, exactly like `ProductEntity.Id` in Step 8 of the tutorial.
- `Insert<T>()` returns the generated identity value as a `long`.
- Keep the `partial` keyword. Without it the generator warns `JAUNTYGEN004` and writes no mapper,
  and `Insert` throws `No parameter binder found for type 'Category'`.

<details>
<summary>Solution</summary>

```csharp
using Jaunty.Attributes;

[Table("categories")]
public partial class Category
{
    [Key]
    [Column("category_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("category_name")]
    public string Name { get; set; }

    [Column("description")]
    public string Description { get; set; }
}

var category = new Category { Name = "Beverages", Description = "Soft drinks, coffees, teas" };

var newId = connection.Insert(category);
Console.WriteLine($"Inserted category with id {newId}");

var loaded = connection.QueryPartial<Category>(
    "SELECT category_id, category_name, description FROM categories WHERE category_id = @Id",
    (int)newId);

foreach (var c in loaded)
    Console.WriteLine($"{c.Id}: {c.Name} - {c.Description}");
```

</details>

## Exercise 2: Diagnose and Fix a Strict-Mode Failure

**Goal:** The code below throws. Figure out why without running it first, then fix it two
different ways.

**Starter code:**

```csharp
public class ProductDetails
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool Discontinued { get; set; }
}

// TODO: this throws an InvalidOperationException. Why?
var details = connection.Query<ProductDetails>(
    "SELECT product_id AS Id, product_name AS Name, unit_price AS Price FROM products");
```

**Hints:**
- Count the properties on `ProductDetails` and count the aliased columns in the `SELECT`.
- There are two independent fixes: change the SQL, or change which method you call.

<details>
<summary>Solution</summary>

The query selects three columns (`Id`, `Name`, `Price`) but `ProductDetails` has four
properties. `Query<T>` is strict mode: every property needs a matching column, so it throws:

```
System.InvalidOperationException: Strict mapping failed: property 'Discontinued' has no matching column
```

**Fix 1 — add the missing column:**

```csharp
var details = connection.Query<ProductDetails>(
    "SELECT product_id AS Id, product_name AS Name, unit_price AS Price, discontinued AS Discontinued FROM products");
```

**Fix 2 — if you genuinely only want three of the four properties, say so with `QueryPartial<T>`:**

```csharp
var details = connection.QueryPartial<ProductDetails>(
    "SELECT product_id AS Id, product_name AS Name, unit_price AS Price FROM products");
// details[i].Discontinued is left at its default value (false)
```

</details>

## Exercise 3: A Projection with `QueryPartial<T>`

**Goal:** Write a projection that returns only `Name` and `Price` for products under $20, and
explain in a comment why `Query<T>` would reject the same SQL.

**Starter code:**

```csharp
public class PriceListEntry
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// TODO: select product_name AS Name, unit_price AS Price for products where unit_price < 20
// TODO: use the mapping mode that matches "we only want two columns, on purpose"
// var cheapEntries = ...

// TODO: as a comment, explain why calling Query<PriceListEntry> with the same SQL
// would be fine here specifically, then explain what would break it
```

**Hints:**
- `PriceListEntry` only has two properties, and the SQL only selects two columns — so this is
  actually a case where `Query<T>` and `QueryPartial<T>` behave identically.
- The interesting question is: what happens the moment someone adds a third property to
  `PriceListEntry` (say, `Sku`) without also updating the SQL?

<details>
<summary>Solution</summary>

```csharp
public class PriceListEntry
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}

var cheapEntries = connection.QueryPartial<PriceListEntry>(
    "SELECT product_name AS Name, unit_price AS Price FROM products WHERE unit_price < @MaxPrice",
    20.00m);

foreach (var e in cheapEntries)
    Console.WriteLine($"{e.Name}: ${e.Price:F2}");

// Query<PriceListEntry> would also succeed today, because the SQL selects exactly the two
// columns PriceListEntry needs — strict mode is satisfied by coincidence. QueryPartial<T> is
// still the right choice: it documents the intent ("this is a deliberate projection") so that
// if someone later adds a Sku property to PriceListEntry without updating this SQL, nothing
// breaks. With Query<T> the same change would throw at runtime the next time this code path
// ran, because Sku would have no matching column.
```

</details>

## Exercise 4: A Custom Mapper and a Query-Counting Interceptor

**Goal:** Write a hand-rolled row mapper for `products`, then register a custom
`ICommandInterceptor` that counts how many commands Jaunty executes, and prove the count goes
up when you query.

**Starter code:**

```csharp
using System.Data;
using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Interceptors;

public class ProductRow
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// TODO: write a mapper that reads "product_id", "product_name", "unit_price" by ordinal
static ProductRow MapProductRow(IDataReader reader)
{
    throw new NotImplementedException();
}

// TODO: implement ICommandInterceptor with a public counter field/property
public class QueryCountingInterceptor : ICommandInterceptor
{
    // TODO: increment a counter in OnCommandExecutingAsync
    // TODO: leave OnCommandExecutedAsync and OnCommandFailedAsync as no-ops
}

var counter = new QueryCountingInterceptor();
// TODO: register `counter` with JauntyConfig.InterceptorPipeline

var options = CommandOptions<ProductRow>.WithMapper(MapProductRow);
var rows = connection.Query<ProductRow>(
    "SELECT product_id, product_name, unit_price FROM products", options: options);

// TODO: print counter's count — it should be at least 1
```

**Hints:**
- `ICommandInterceptor` has three methods: `OnCommandExecutingAsync`, `OnCommandExecutedAsync`,
  `OnCommandFailedAsync`, all returning `ValueTask` — see Step 7's `CommandOptions<T>.WithMapper`
  and the root README's "Custom Interceptors" section for the shape.
- Register interceptors with
  `JauntyConfig.InterceptorPipeline = new InterceptorPipeline(new[] { counter });` before running
  any queries you want counted.

<details>
<summary>Solution</summary>

```csharp
using System.Data;
using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Interceptors;

public class ProductRow
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

static ProductRow MapProductRow(IDataReader reader)
{
    return new ProductRow
    {
        Id = reader.GetInt32(reader.GetOrdinal("product_id")),
        Name = reader.GetString(reader.GetOrdinal("product_name")),
        Price = (decimal)reader.GetDouble(reader.GetOrdinal("unit_price"))
    };
}

public class QueryCountingInterceptor : ICommandInterceptor
{
    public int Count { get; private set; }

    public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken ct)
    {
        Count++;
        return new ValueTask();
    }

    public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken ct)
    {
        return new ValueTask();
    }

    public ValueTask OnCommandFailedAsync(CommandContext context, Exception ex, CancellationToken ct)
    {
        return new ValueTask();
    }
}

var counter = new QueryCountingInterceptor();
JauntyConfig.InterceptorPipeline = new InterceptorPipeline(new[] { counter });

var options = CommandOptions<ProductRow>.WithMapper(MapProductRow);
var rows = connection.Query<ProductRow>(
    "SELECT product_id, product_name, unit_price FROM products", options: options);

Console.WriteLine($"Queries executed so far: {counter.Count}");
// Run another query and confirm the count increases again
connection.QueryScalar<long>("SELECT COUNT(*) FROM products");
Console.WriteLine($"Queries executed after a second call: {counter.Count}");
```

</details>

## Exercise 5: Bulk Insert vs. a Loop, Timed

**Goal:** Insert 500 new products two ways — one `Insert` call per row inside a transaction, and
one `BulkInsert` call for the whole batch inside a transaction — and compare elapsed time.

**Starter code:**

```csharp
using System.Diagnostics;
using Jaunty.Attributes;

[Table("products")]
public partial class BenchProduct
{
    [Key]
    [Column("product_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; }

    [Column("unit_price")]
    public decimal Price { get; set; }

    [Column("discontinued")]
    public bool Discontinued { get; set; }
}

var loopBatch = Enumerable.Range(1, 500)
    .Select(i => new BenchProduct { Name = $"Loop Item {i}", Price = i * 1.5m, Discontinued = false })
    .ToList();

var bulkBatch = Enumerable.Range(1, 500)
    .Select(i => new BenchProduct { Name = $"Bulk Item {i}", Price = i * 1.5m, Discontinued = false })
    .ToList();

// TODO: time inserting `loopBatch` one row at a time with Insert(), inside a single transaction
// TODO: time inserting `bulkBatch` in one call with BulkInsert(), inside a single transaction
// TODO: print both elapsed times
```

**Hints:**
- Wrap each approach in its own `using var transaction = connection.BeginTransaction();` /
  `transaction.Commit();` pair — a transaction per row would defeat the point of the comparison.
- `Stopwatch.StartNew()` / `.Elapsed` is enough; you don't need a full benchmarking harness for
  this exercise.

<details>
<summary>Solution</summary>

```csharp
using System.Diagnostics;
using Jaunty.Attributes;

[Table("products")]
public partial class BenchProduct
{
    [Key]
    [Column("product_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; }

    [Column("unit_price")]
    public decimal Price { get; set; }

    [Column("discontinued")]
    public bool Discontinued { get; set; }
}

var loopBatch = Enumerable.Range(1, 500)
    .Select(i => new BenchProduct { Name = $"Loop Item {i}", Price = i * 1.5m, Discontinued = false })
    .ToList();

var bulkBatch = Enumerable.Range(1, 500)
    .Select(i => new BenchProduct { Name = $"Bulk Item {i}", Price = i * 1.5m, Discontinued = false })
    .ToList();

var loopStopwatch = Stopwatch.StartNew();
using (var transaction = connection.BeginTransaction())
{
    foreach (var product in loopBatch)
        connection.Insert(product, CommandOptions.WithTransaction(transaction));

    transaction.Commit();
}
loopStopwatch.Stop();

var bulkStopwatch = Stopwatch.StartNew();
using (var transaction = connection.BeginTransaction())
{
    connection.BulkInsert(bulkBatch, CommandOptions.WithTransaction(transaction));
    transaction.Commit();
}
bulkStopwatch.Stop();

Console.WriteLine($"Loop insert (500 rows, one transaction): {loopStopwatch.ElapsedMilliseconds} ms");
Console.WriteLine($"BulkInsert (500 rows, one transaction): {bulkStopwatch.ElapsedMilliseconds} ms");
```

On SQLite specifically, `BulkInsert` doesn't have a native bulk-copy API to fall back on (unlike
SQL Server's `SqlBulkCopy` or Postgres's binary `COPY`), so the gap you see here is smaller than
you'd get on those providers — but batching everything into a single transaction is still faster
than committing per row. Try the same comparison against SQL Server or PostgreSQL if you have one
available, and see the root README's "Native Bulk Copy Performance" table for measured numbers
per database.

</details>

## Exercise 6: Fix a Qualifier That an Alias Retired

**Goal:** Meet the one rule that catches people out when they mix a lambda `On` with a string
condition, and fix the same query two different ways.

This one needs the Fluent builder and the `categories` table you created in Exercise 1:

```bash
dotnet add package Extrode.Jaunty.Fluent
```

**Starter code:**

```csharp
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = """
        ALTER TABLE products ADD COLUMN category_id INTEGER;
        UPDATE products SET category_id = 1;
        """;
    cmd.ExecuteNonQuery();
}

// Add this property to the ProductEntity class from Step 8 of the tutorial:
//     [Column("category_id")]
//     public int CategoryId { get; set; }
//
// ProductEntity and Category both need the `partial` keyword here, as they have had since
// Step 8 and Exercise 1 — the Fluent builder reads the same generated metadata Insert does.

using Jaunty.Fluent;

// TODO: this throws. Predict the message before you run it.
var rows = connection.From<ProductEntity>()
    .InnerJoin<Category>()
    .On((p, c) => p.CategoryId == c.Id)
    .Where("products.unit_price > 15")
    .SelectBoth();
```

**Hints:**
- Replace `.SelectBoth()` with `.ToSql()` and print the result. Read the `FROM` clause first.
- Where did `p` and `c` in the generated SQL come from? You did write them somewhere.
- There are two fixes, and neither one is "stop using strings".

<details>
<summary>Solution</summary>

`ToSql()` shows what the database is being asked:

```sql
SELECT p.product_id, p.product_name, p.unit_price, p.discontinued, p.category_id
FROM products p
INNER JOIN categories c ON (p.category_id = c.category_id)
WHERE products.unit_price > 15
      ^^^^^^^^
```

Jaunty took the aliases from the lambda parameter names you wrote — `p` and `c`. In SQL, aliasing a
table retires its name as a qualifier for the rest of the statement, so `products` now refers to
nothing and SQLite says so:

```
Microsoft.Data.Sqlite.SqliteException
SQLite Error 1: 'no such column: products.unit_price'.
```

On SQL Server the same query fails as
`Msg 4104, The multi-part identifier "products.unit_price" could not be bound.`

**Fix 1 — use the alias the lambda established:**

```csharp
.Where("p.unit_price > 15")     // 4 rows
```

**Fix 2 — do not establish an alias at all.** The string form of `On` infers nothing, so both tables
keep their names and the filter you first wrote is correct as it stands:

```csharp
var rows = connection.From<ProductEntity>()
    .InnerJoin<Category>()
    .On("products.category_id", "categories.category_id")
    .Where("products.unit_price > 15")
    .SelectBoth();
```

**One thing that is not a mistake.** A subquery inside the string opens its own scope, so naming the
table *there* stays right even while the outer statement is aliased:

```csharp
.Where("p.product_id IN (SELECT product_id FROM products WHERE discontinued = 0)")
```

The rule to carry away: check `ToSql()` when a qualifier is in doubt. Aliases are inferred per join
and only from names Jaunty can use - a parameter named after a SQL keyword, after a table already
in the query, or after an alias an earlier join locked in, is declined and that join keeps the full
table names. The one exception is a self-join with neither side aliased, which takes `t1`/`t2`
rather than emit ambiguous SQL.

</details>
