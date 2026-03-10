# Jaunty.Fluent

**Type-safe fluent SQL query builder for Jaunty micro-ORM**

Jaunty.Fluent provides a fluent, expression-based API for building SQL queries with compile-time type safety, while generating efficient parameterized SQL under the hood.

---

## Quick Start

```csharp
using Jaunty;
using Jaunty.Fluent;
using MyProject.Entities;

// Create connection
using var connection = new SqlConnection(connectionString);

// Simple query
var products = connection.From<Product>()
    .Where(p => p.CategoryId == 1)
    .OrderBy(p => p.ProductName)
    .Select();

// Join query
var productsWithCategories = connection.From<Product>()
    .InnerJoin<Category>()
    .On(p => p.CategoryId, c => c.CategoryId)
    .Where((p, c) => c.CategoryName == "Beverages")
    .Select();

// Aggregate query
var count = connection.From<Product>()
    .Where(p => p.UnitPrice > 10)
    .Count();

var avgPrice = connection.From<Product>()
    .Where(p => p.CategoryId == 1)
    .Avg(p => p.UnitPrice);
```

---

## Features

- **Type-safe** - Compile-time checking of entity properties
- **Expression-based** - LINQ-like syntax with expression trees
- **Parameterized** - Automatic SQL parameterization prevents injection
- **Dialect-aware** - Generates SQL for SQLite, SQL Server, PostgreSQL, MySQL
- **NativeAOT compatible** - No runtime reflection in core paths
- **Async support** - Full async/await support with cancellation tokens

---

## API Overview

### Basic Queries

```csharp
// Select all
var products = db.From<Product>().Select();

// Select first
var product = db.From<Product>()
    .Where(p => p.ProductId == 1)
    .SelectFirst();

// Select with projection
var names = db.From<Product>()
    .SelectPartial(p => p.ProductName);
```

### WHERE Clauses

```csharp
// Expression-based
db.From<Product>()
    .Where(p => p.CategoryId == 1)
    .And(p => p.UnitPrice > 10)
    .Select();

// String-based
db.From<Product>("p")
    .Where("p.category_id", 1)
    .AndRaw("p.unit_price > @MinPrice", new { MinPrice = 10 })
    .Select();

// Collection-based
var categoryIds = new[] { 1, 2, 3 };
db.From<Product>()
    .WhereIn(p => p.CategoryId, categoryIds)
    .Select();

// Range-based
db.From<Product>()
    .WhereBetween(p => p.UnitPrice, 10, 100)
    .Select();

// Subquery
db.From<Product>()
    .WhereInSubquery(
        p => p.CategoryId,
        c => c.CategoryId,
        db.From<Category>().Where(c => c.CategoryName == "Beverages"))
    .Select();
```

### JOINs

```csharp
// Two-table join
db.From<Product>()
    .InnerJoin<Category>()
    .On(p => p.CategoryId, c => c.CategoryId)
    .Select();

// Three-table join
db.From<Product>()
    .InnerJoin<Category>()
    .On(p => p.CategoryId, c => c.CategoryId)
    .InnerJoin<Supplier>()
    .On(p => p.SupplierId, s => s.SupplierId)
    .SelectAll(); // Returns List<(Product, Category, Supplier)>

// With aliases
db.From<Product>("p")
    .InnerJoin<Category>("c")
    .On("p.category_id", "c.category_id")
    .Select();
```

### ORDER BY

```csharp
// Single column
db.From<Product>()
    .OrderBy(p => p.ProductName)
    .Select();

// Multiple columns
db.From<Product>()
    .OrderBy(p => p.CategoryId)
    .ThenBy(p => p.ProductName)
    .ThenByDescending(p => p.UnitPrice)
    .Select();

// Descending
db.From<Product>()
    .OrderByDescending(p => p.CreatedDate)
    .Select();
```

### GROUP BY & Aggregates

```csharp
// Group by with aggregate
var results = db.From<Product>()
    .GroupBy(p => p.CategoryId)
    .Select(g => new { g.Key, Count = g.Count() });

// Multiple aggregates
var stats = db.From<Product>()
    .GroupBy(p => p.CategoryId)
    .Select(g => new
    {
        g.Key,
        Count = g.Count(),
        AvgPrice = g.Avg(p => p.UnitPrice),
        MinPrice = g.Min(p => p.UnitPrice),
        MaxPrice = g.Max(p => p.UnitPrice)
    });

// With HAVING
var results = db.From<Product>()
    .GroupBy(p => p.CategoryId)
    .Having(g => g.Count() > 5)
    .Select(g => new { g.Key, Count = g.Count() });
```

### Set Operations

```csharp
// UNION
db.From<Product>()
    .Where(p => p.CategoryId == 1)
    .Union(db.From<Product>().Where(p => p.CategoryId == 2))
    .Select();

// UNION ALL
db.From<Product>()
    .Where(p => p.CategoryId == 1)
    .UnionAll(db.From<Product>().Where(p => p.CategoryId == 2))
    .Select();

// EXCEPT
db.From<Product>()
    .Where(p => p.CategoryId == 1)
    .Except(db.From<Product>().Where(p => p.Discontinued))
    .Select();

// INTERSECT
db.From<Product>()
    .Where(p => p.CategoryId == 1)
    .Intersect(db.From<Product>().Where(p => p.UnitPrice > 100))
    .Select();
```

### CTE (Common Table Expressions)

```csharp
var cte = db.With<Product>()
    .Where(p => p.UnitPrice > 100)
    .AsCte("ExpensiveProducts");

var results = db.From<Product>()
    .With(cte)
    .WhereExists<ExpensiveProducts>((p, e) => p.ProductId == e.ProductId)
    .Select();
```

### Window Functions

```csharp
var results = db.From<Product>()
    .Select(p => new
    {
        p.ProductName,
        p.UnitPrice,
        RowNum = Sql.RowNumber()
            .PartitionBy(p => p.CategoryId)
            .OrderBy(p => p.UnitPrice)
    });
```

### UPDATE

```csharp
// Update with SET
db.From<Product>()
    .Set(p => p.UnitPrice, 15.00m)
    .Set(p => p.Discontinued, true)
    .Where(p => p.CategoryId == 1)
    .Update();

// Update all (no WHERE)
db.From<Product>()
    .Set(p => p.Discontinued, true)
    .UpdateAll();
```

### DELETE

```csharp
// Delete with WHERE
db.From<Product>()
    .Where(p => p.Discontinued)
    .Delete();

// Delete all (no WHERE - use with caution!)
db.From<Product>()
    .DeleteAll();
```

### Async Operations

```csharp
// Async select
var products = await db.From<Product>()
    .Where(p => p.CategoryId == 1)
    .SelectAsync();

// Async with cancellation
var cts = new CancellationTokenSource();
var product = await db.From<Product>()
    .Where(p => p.ProductId == 1)
    .SelectFirstAsync(cts.Token);

// Async aggregate
var count = await db.From<Product>()
    .CountAsync();
```

---

## Entity Configuration

Jaunty.Fluent uses attributes to map entities to database tables:

```csharp
using Jaunty.Attributes;

[Table("products")]
public class Product
{
    [Key]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }
}
```

**Attributes:**
- `[Table("table_name")]` - Maps class to database table
- `[Column("column_name")]` - Maps property to database column
- `[Key]` - Marks primary key property
- `[Ignore]` - Excludes property from mapping

---

## Dialect Support

Jaunty.Fluent automatically detects the database dialect and generates appropriate SQL:

| Database | Supported | Notes |
|----------|-----------|-------|
| SQLite | yes | Default dialect |
| SQL Server | yes | Supports `SqlBulkCopy` for bulk operations |
| PostgreSQL | yes | Supports `COPY` for bulk operations |
| MySQL | yes | Uses multi-row INSERT for bulk operations |

---

## Performance Tips

1. **Use `SelectPartial` for large result sets** - Only fetch columns you need
2. **Use `Take` and `Skip` for pagination** - Avoid loading entire tables
3. **Use indexes on WHERE columns** - Ensure query performance
4. **Use transactions for bulk operations** - Reduces round trips
5. **Use async for I/O-bound operations** - Improves scalability

```csharp
// Good: Only fetch needed columns
var names = db.From<Product>()
    .SelectPartial(p => p.ProductName);

// Good: Pagination
var page = db.From<Product>()
    .OrderBy(p => p.ProductId)
    .Skip(100)
    .Take(50)
    .Select();

// Good: Transaction for bulk insert
using var tx = connection.BeginTransaction();
await db.BulkInsert<Product>()
    .From(products)
    .WithTransaction(tx)
    .ExecuteAsync();
tx.Commit();
```

---

## Migration from Core Jaunty

| Core Jaunty | Jaunty.Fluent |
|-------------|---------------|
| `connection.Query<T>(sql, params)` | `connection.From<T>().Where(...).Select()` |
| `connection.QueryFirst<T>(sql, params)` | `connection.From<T>().Where(...).SelectFirst()` |
| `connection.QuerySingle<T>(sql, params)` | `connection.From<T>().Where(...).SelectSingle()` |
| `connection.Count<T>(sql, params)` | `connection.From<T>().Where(...).Count()` |

---

## Related Projects

- [Jaunty](../Jaunty/) - Core micro-ORM with raw SQL support
- [Jaunty.Scaffolding](../Jaunty.Scaffolding/) - Database scaffolding and code generation

---

## License

Same license as Jaunty. See [LICENSE](../../LICENSE.md) for details.
