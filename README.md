# Jaunty

**The micro-ORM that respects your SQL and your time.**

Jaunty is a high-performance data access library for .NET that does one thing exceptionally well: execute your SQL and map results to objects. No query builders. No LINQ translation. No magic. Just fast, predictable, type-safe data access.

```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });
```

---

## Why Jaunty Exists

Most micro-ORMs let you write SQL and get objects back. Jaunty does this too—but with deliberate design choices that catch bugs earlier and run faster.

**The core philosophy:**
- **Performant** — Compiled expression trees, not runtime reflection
- **Efficient** — Minimal allocations, cached metadata, zero dependencies
- **Elegant** — Clean API that reads like intent, not ceremony

### Strict by Default

When you call `Query<T>`, Jaunty requires that **every property** on your entity has a matching column in the result set. This is intentional.

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// This throws immediately - missing 'Price' column
var products = connection.Query<Product>(
    "SELECT id, name FROM products");
// InvalidOperationException: "Strict mapping failed: property 'Price' has no matching column"
```

**Why?** Because silent partial mapping is a bug waiting to happen. If you wanted all three properties, you should know immediately that your query is wrong. If you intentionally want partial data, say so explicitly with `QueryPartial<T>`.

This catches mismatches at development time, not when a customer reports weird behavior in production.

---

## Installation

```bash
dotnet add package Beparey.Jaunty
```

Targets `netstandard2.0` and `net8.0`. Works with any ADO.NET provider.

---

## Quick Start

```csharp
using Jaunty;

// Strict mapping - all properties must have matching columns
var products = connection.Query<Product>(
    "SELECT id AS Id, name AS Name, price AS Price FROM products");

// Partial mapping - only map what exists
var summaries = connection.QueryPartial<ProductSummary>(
    "SELECT id AS Id, name AS Name FROM products");

// Scalar values
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

// Parameters - named or positional
var filtered = connection.Query<Product>(sql, new { CategoryId = 1 });
var filtered = connection.Query<Product>(sql, 1);  // positional
var filtered = connection.Query<Product>(sql, 1, "active", 50.00m);  // multiple

// Async with cancellation
var products = await connection.QueryAsync<Product>(sql, cancellationToken);
```

---

## The Two Mapping Modes

### `Query<T>` — Strict Mode

Every entity property must have a corresponding column. Missing columns throw immediately.

```csharp
public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
}

// All 3 columns required
var orders = connection.Query<Order>(
    "SELECT order_id AS OrderId, order_date AS OrderDate, total AS Total FROM orders");

// Missing 'Total' - throws InvalidOperationException
var orders = connection.Query<Order>(
    "SELECT order_id AS OrderId, order_date AS OrderDate FROM orders");
```

**Use strict mode when:** You expect complete entities. This is the default because it's the safer choice.

### `QueryPartial<T>` — Partial Mode

Map only the columns that exist. Unmatched properties keep their default values.

```csharp
public class OrderSummary
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
}

// Only selecting what we need
var summaries = connection.QueryPartial<OrderSummary>(
    "SELECT order_id AS OrderId, order_date AS OrderDate FROM orders");

// Extra columns in result? Ignored silently.
var summaries = connection.QueryPartial<OrderSummary>(
    "SELECT order_id AS OrderId, order_date AS OrderDate, total, customer_id FROM orders");
```

**Use partial mode when:** You're intentionally selecting a subset of columns, using DTOs, or working with projections.

---

## Parameter Binding

### Named Parameters

```csharp
var orders = connection.Query<Order>(
    "SELECT * FROM orders WHERE customer_id = @CustomerId AND status = @Status",
    new { CustomerId = "ALFKI", Status = "shipped" });
```

### Positional Parameters

Jaunty parses your SQL to extract parameter names, then binds values in order.

```csharp
// Single value
var order = connection.Query<Order>(
    "SELECT * FROM orders WHERE order_id = @Id",
    42);

// Multiple values
var orders = connection.Query<Order>(
    "SELECT * FROM orders WHERE customer_id = @Customer AND total > @MinTotal",
    "ALFKI", 100.00m);

// Array
var orders = connection.Query<Order>(sql, new object[] { "ALFKI", 100.00m });
```

### Duplicate Parameters

Same parameter used multiple times? Provide one value.

```csharp
// @Id appears twice, but we only pass it once
var results = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @Id OR supplier_id = @Id",
    7);
```

### Parameter Count Validation

Jaunty validates immediately. No waiting for the database to complain.

```csharp
// SQL has 2 unique parameters, but we passed 3 values
connection.Query<Product>(sql, 1, 2, 3);
// ArgumentException: "Parameter count mismatch: SQL contains 2 unique parameter(s), but 3 value(s) provided."
```

---

## Transactions and Timeouts

Use `CommandOptions` to pass transaction or timeout settings. No overload confusion.

```csharp
using var transaction = connection.BeginTransaction();

// With transaction
var orders = connection.Query<Order>(sql, parameters,
    CommandOptions.WithTransaction(transaction));

// With timeout
var orders = connection.Query<Order>(sql, parameters,
    CommandOptions.WithTimeout(30));

// Both
var orders = connection.Query<Order>(sql, parameters,
    CommandOptions.With(transaction, timeoutSeconds: 30));

transaction.Commit();
```

---

## Attribute Mapping

Override table and column names per-entity.

```csharp
using Jaunty.Attributes;

[Table("order_items")]
public class OrderItem
{
    [Column("item_id")]
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; }

    [Column("unit_price")]
    public decimal Price { get; set; }

    [Ignore]  // Not mapped from database
    public decimal CalculatedDiscount { get; set; }
}

// SQL uses database column names
var items = connection.QueryPartial<OrderItem>(
    "SELECT item_id, product_name, unit_price FROM order_items");
```

**Priority order:** `[Column]` attribute > `JauntyConfig.ColumnNameResolver` > Property name

---

## Global Configuration

Configure naming conventions once at application startup. Resolution is cached per-type for performance.

```csharp
using Jaunty.Configuration;

// In your startup/initialization code:
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;
JauntyConfig.TableNameResolver = NamingConvention.SnakeCasePluralTable;
```

**Important:** Configure resolvers before executing any queries. Metadata is cached when a type is first used and won't pick up resolver changes afterward. This is intentional—it's faster, and configuration belongs at startup.

### Built-in Conventions

```csharp
NamingConvention.ToSnakeCase("ProductName")     // "product_name"
NamingConvention.ToLowerCase("ProductName")     // "productname"
NamingConvention.Pluralize("Category")          // "Categories"
NamingConvention.Pluralize("Company")           // "Companies"
NamingConvention.SnakeCasePluralTable           // Type -> "snake_case_plurals"
NamingConvention.SnakeCaseColumn                // Property -> "snake_case"
```

### Custom Resolvers

```csharp
JauntyConfig.TableNameResolver = type => $"tbl_{type.Name.ToLower()}";
JauntyConfig.ColumnNameResolver = prop => $"col_{prop.ToLower()}";
```

---

## Async Support

Every method has an async counterpart. CancellationToken is optional.

```csharp
// Without cancellation token
var products = await connection.QueryAsync<Product>(sql);
var count = await connection.QueryScalarAsync<long>(sql);

// With cancellation token
var products = await connection.QueryAsync<Product>(sql, cancellationToken);
var products = await connection.QueryAsync<Product>(sql, parameters, cancellationToken);

// Partial mapping async
var summaries = await connection.QueryPartialAsync<ProductSummary>(sql, cancellationToken);
```

---

## How It Works

### Connection Management

Jaunty respects your connection state:
- If the connection was closed, Jaunty opens it, executes, and closes it
- If the connection was already open, Jaunty leaves it open

No surprises. No leaked connections.

### Performance Architecture

1. **Compiled Setters** — Property setters are compiled via expression trees when a type is first used. No reflection during query execution.

2. **Metadata Caching** — Column mappings are cached in static generic classes (`MetadataCache<T>`). Zero allocation per query for metadata lookup.

3. **Parameter Caching** — Named parameter property getters are compiled and cached per anonymous type.

4. **SQL Parsing** — Parameter extraction skips string literals, comments, and quoted identifiers. Cheap compared to network roundtrip.

### NULL Handling

- **Nullable types** (`int?`, `string`, etc.): NULL becomes `default`
- **Non-nullable value types**: NULL throws `InvalidOperationException`

```csharp
public class Product
{
    public int Id { get; set; }           // NULL throws
    public int? CategoryId { get; set; }  // NULL becomes null
    public string Name { get; set; }      // NULL becomes null
}
```

---

## API Reference

### Query Methods

| Method | Mapping | Description |
|--------|---------|-------------|
| `Query<T>` | Strict | All properties must have columns |
| `QueryPartial<T>` | Partial | Map only matching columns |
| `QueryScalar<T>` | — | First column of first row |
| `QueryAsync<T>` | Strict | Async strict mapping |
| `QueryPartialAsync<T>` | Partial | Async partial mapping |
| `QueryScalarAsync<T>` | — | Async scalar |

### Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[Table("name")]` | Class | Override table name |
| `[Column("name")]` | Property | Override column name |
| `[Ignore]` | Property | Exclude from mapping |

### Configuration

| Member | Purpose |
|--------|---------|
| `JauntyConfig.TableNameResolver` | `Func<Type, string>` for table names |
| `JauntyConfig.ColumnNameResolver` | `Func<string, string>` for column names |
| `JauntyConfig.Reset()` | Clear configuration |
| `NamingConvention.*` | Built-in naming helpers |

---

## Design Decisions

### Why strict mapping by default?

Silent partial mapping causes bugs that surface far from their origin. Strict mode fails fast with a clear message. If you want partial data, `QueryPartial<T>` makes that intent explicit.

### Why parse SQL for positional parameters?

The cost of scanning a SQL string for `@param` tokens is negligible compared to network latency and query execution. The benefit—natural syntax like `Query(sql, 1, 2, 3)`—is worth it.

### Why `CommandOptions` instead of overloads?

Overloads for every combination of transaction/timeout/parameters create ambiguity. `Query(sql, 1, 30)` could mean "parameter 1, timeout 30" or "parameters 1 and 30". `CommandOptions` eliminates confusion.

### Why cache metadata in static constructors?

Performance. Static generic classes initialize once per type and live for the application lifetime. Configuration should happen at startup before queries run—this is a feature, not a limitation.

---

## Comparison

| Feature | Jaunty | Dapper | EF Core |
|---------|--------|--------|---------|
| Raw SQL execution | Yes | Yes | Yes |
| Strict mapping mode | Yes | No | No |
| Partial mapping mode | Yes | Yes | Yes |
| Positional parameters | Yes | No | No |
| Zero dependencies | Yes | Yes | No |
| Connection state management | Yes | Yes | Yes |
| LINQ translation | No | No | Yes |
| Change tracking | No | No | Yes |

---

## License

MIT

---

Built by [Syed Beparey](https://github.com/beparey)
