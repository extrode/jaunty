# API Reference

Complete API documentation for Jaunty micro-ORM.

## Method Categories

### Query Methods (Read)

| Category | Methods | Documentation |
|----------|---------|---------------|
| **Strict Mapping** | `Query<T>`, `QueryFirst<T>`, `QueryFirstOrDefault<T>`, `QuerySingle<T>`, `QuerySingleOrDefault<T>` | [`query-methods.md`](query-methods.md) |
| **Partial Mapping** | `QueryPartial<T>`, `QueryPartialFirst<T>`, `QueryPartialFirstOrDefault<T>`, `QueryPartialSingle<T>`, `QueryPartialSingleOrDefault<T>` | [`query-partial-methods.md`](query-partial-methods.md) |
| **Scalar Values** | `QueryScalar<T>`, `ExecuteScalar<T>` | [`scalar-methods.md`](scalar-methods.md) |
| **Streaming** | `QueryStream<T>`, `QueryPartialStream<T>`, `QueryPartialUnbuffered<T>` | [`streaming-methods.md`](streaming-methods.md) |

### Write Methods

| Category | Methods | Documentation |
|----------|---------|---------------|
| **Individual** | `Insert<T>`, `Update<T>`, `Delete<T>` | [`write-methods.md`](write-methods.md) |
| **Bulk** | `BulkInsert<T>`, `BulkUpdate<T>`, `BulkDelete<T>` | [`write-methods.md`](write-methods.md) |
| **Bulk Copy** | Native bulk copy (auto-activated for 100+ rows) | [`bulk-copy-methods.md`](bulk-copy-methods.md) |
| **Upsert** | `Upsert<T>` | [`write-methods.md`](write-methods.md) |

### Advanced

| Category | Methods | Documentation |
|----------|---------|---------------|
| **Multiple Result Sets** | `QueryMultiple`, `GridReader.Read*` | [`multiple-result-sets.md`](multiple-result-sets.md) |
| **Stored Procedures** | `ExecuteStoredProcedure*` | [`stored-procedures.md`](stored-procedures.md) |
| **Special Types** | `Query<Dictionary>`, `Query<ExpandoObject>`, `Query<KeyValuePair>`, `Query<ValueTuple>` | [`special-types.md`](special-types.md) |

### Configuration

| Component | Purpose | Documentation |
|-----------|---------|---------------|
| **Attributes** | `[Table]`, `[Column]`, `[Ignore]`, `[Key]`, `[DatabaseGenerated]` | [`attributes.md`](attributes.md) |
| **JauntyConfig** | Global configuration, naming conventions | [`configuration.md`](configuration.md) |
| **CommandOptions** | Transaction, timeout, custom mapper | [`command-options.md`](command-options.md) |

### Fluent API (Optional)

| Component | Purpose | Documentation |
|-----------|---------|---------------|
| **Fluent Select** | `Query.Build().Select().From()` | [`fluent-api.md`](fluent-api.md) |
| **Fluent Where** | `.Where()`, `.WhereIn()`, `.WhereBetween()` | [`fluent-api.md`](fluent-api.md) |
| **Fluent Join** | `.Join()`, `.LeftJoin()` | [`fluent-api.md`](fluent-api.md) |
| **Fluent Group** | `.GroupBy()`, `.OrderBy()`, `.Take()`, `.Skip()` | [`fluent-api.md`](fluent-api.md) |

---

## Quick Reference

### Basic Query

```csharp
// Strict mapping
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });

// Partial mapping
var summaries = connection.QueryPartial<ProductSummary>(
    "SELECT id, name FROM products");

// Scalar
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");
```

### Async

```csharp
var products = await connection.QueryAsync<Product>(sql, cancellationToken);
var count = await connection.QueryScalarAsync<long>(sql, cancellationToken);
```

### With Options

```csharp
// Transaction
using var tx = connection.BeginTransaction();
var products = connection.Query<Product>(sql, 
    CommandOptions<Product>.WithTransaction(tx));

// Timeout
var products = connection.Query<Product>(sql, 
    CommandOptions<Product>.WithTimeout(30));

// Custom mapper
var products = connection.Query<Product>(sql, 
    CommandOptions<Product>.WithMapper(MapProduct));
```

### CRUD

```csharp
// Insert (returns identity)
long id = connection.Insert(product);

// Update (returns rows affected)
int rows = connection.Update(product);

// Delete by entity
int rows = connection.Delete(product);

// Delete by ID
int rows = connection.Delete<Product>(id);
```

Note: `Upsert`/`UpsertAsync` return provider-specific "rows affected" values; for MariaDB/MySQL, updates may report `2` instead of `1`. Prefer checking `result > 0` for cross-provider behavior.

### Multiple Result Sets

```csharp
const string sql = @"
    SELECT * FROM products WHERE category_id = @CategoryId;
    SELECT * FROM categories WHERE category_id = @CategoryId;
";

using var multi = connection.QueryMultiple(sql, new { CategoryId = 1 });
var products = multi.Read<Product>();
var category = multi.ReadFirst<Category>();
```

---

## Mapping Modes

| Mode | Method | Behavior |
|------|--------|----------|
| **Strict** | `Query<T>()` | All properties must have matching columns or throws `InvalidOperationException` |
| **Partial** | `QueryPartial<T>()` | Only maps existing columns, ignores unmatched properties |

## Parameter Binding

```csharp
// Named parameters
connection.Query<Product>(sql, new { CategoryId = 1, MinPrice = 100 });

// Positional parameters (parsed from SQL)
connection.Query<Product>(sql, 1, 100);  // @CategoryId=1, @MinPrice=100

// Collection expansion
connection.Query<Product>(
    "SELECT * FROM products WHERE category_id IN @CategoryIds",
    new { CategoryIds = new[] { 1, 2, 3 } });
```

## Error Handling

| Error | Exception |
|-------|-----------|
| Missing column (strict mode) | `InvalidOperationException` |
| Parameter count mismatch | `ArgumentException` |
| No elements (First/Single) | `InvalidOperationException` |
| Multiple elements (Single) | `InvalidOperationException` |
| NULL to non-nullable value type | `InvalidOperationException` |

---

## See Also

- [`../../00-quick-start/README.md`](../../00-quick-start/README.md) - Quick start
- [`../../02-architecture/README.md`](../../02-architecture/README.md) - Architecture
