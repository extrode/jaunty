# Quick Start Guide

Welcome to Jaunty! This guide gets you up and running quickly.

## What is Jaunty?

Jaunty is a lightweight, high-performance micro-ORM for .NET that:
- Executes your SQL and maps results to objects
- Uses strict mapping by default (catches bugs early)
- Has no package dependencies on `net8.0` and `net10.0`
- Targets `netstandard2.0`, `net8.0` and `net10.0`

```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });
```

## Prerequisites

- .NET SDK 10.0 (the solution targets `net10.0`; CI installs 8.0, 9.0 and 10.0)
- Visual Studio 2022 or VS Code
- Nothing else for the SQLite suites. The SQL Server, PostgreSQL, MySQL and MariaDB suites need a
  running server (`docker-compose.yml` starts all four) and skip when none is reachable

## Build the Project

```bash
# Clone the repository
git clone https://github.com/extrode/jaunty.git
cd jaunty

# Build all targets
dotnet build

# Build specific target
dotnet build -f net8.0
```

## Run Tests

```bash
# Every project, every framework
dotnet test --solution Jaunty.slnx

# One project, one framework
dotnet test --project tests/Jaunty.Tests -f net10.0

# One test class
dotnet test --project tests/Jaunty.Tests -f net10.0 --filter-class "*QueryTests*"
```

## Basic Usage

### 1. Add the Package

```bash
dotnet add package Extrode.Jaunty
```

### 2. Create an Entity

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

### 3. Query the Database

```csharp
using Jaunty;

// Strict mapping - all properties must have columns
var products = connection.Query<Product>(
    "SELECT id AS Id, name AS Name, price AS Price FROM products");

// Partial mapping - only map existing columns
var summaries = connection.QueryPartial<ProductSummary>(
    "SELECT id AS Id, name AS Name FROM products");

// With parameters
var filtered = connection.Query<Product>(
    "SELECT * FROM products WHERE price > @MinPrice",
    new { MinPrice = 100 });
```

## Key Concepts

### Strict vs Partial Mapping

| Mode | Method | Behavior |
|------|--------|----------|
| **Strict** | `Query<T>()` | Throws on a property with no column; a column with no property throws under the reflection mapper and is ignored by the generated one |
| **Partial** | `QueryPartial<T>()` | Only maps existing columns, ignores rest |

### Connection Management

Jaunty respects your connection state:
- If closed: Opens, executes, closes
- If open: Executes, leaves open

```csharp
// Connection closed - Jaunty opens and closes
var products = connection.Query<Product>(sql);

// Connection open - Jaunty leaves it open
connection.Open();
var products = connection.Query<Product>(sql);
// Connection still open
```

### Parameter Binding

```csharp
// Named parameters (anonymous object)
connection.Query<Product>(sql, new { CategoryId = 1, MinPrice = 100 });

// A single scalar, when the SQL names exactly one parameter
connection.Query<Product>("SELECT * FROM products WHERE category_id = @CategoryId", 1);
```

There is no positional binding: SQL that names two or more parameters takes an object or a
dictionary, and a lone scalar against such SQL throws before anything is sent.

## Next Steps

1. **API Reference**: See [`../01-api-reference/README.md`](../01-api-reference/README.md)
2. **Configuration**: See [`../01-api-reference/configuration.md`](../01-api-reference/configuration.md)
3. **Attributes**: See [`../01-api-reference/attributes.md`](../01-api-reference/attributes.md)
4. **Architecture**: See [`../02-architecture/README.md`](../02-architecture/README.md)

## Common Issues

### "Strict mapping failed: property 'X' has no matching column"

Your SELECT doesn't include a column for property X. Either:
- Add the column to your SELECT
- Use `QueryPartial<T>()` instead
- Add `[Ignore]` attribute to the property

On a source-generated entity the same cause surfaces as whatever the provider's `GetOrdinal` throws
for an unknown column name - `ArgumentOutOfRangeException` on SQLite, `IndexOutOfRangeException` on
SqlClient - rather than this message. The reverse case - a column with no property - throws only
under the reflection mapper; see
[which mapper enforces which direction](../01-api-reference/query-partial-methods.md#which-mapper-enforces-which-direction).

### "No property found on type 'X' matching SQL parameter '@Name'"

The SQL names a parameter your object has no property for. Check:
- Parameter names match between SQL and object
- No typos in `@ParameterName`
- SQL comments and string literals are skipped by the parser, so a stray `@word` in prose is not
  the cause; a real parameter with no value is

The reverse, a property the SQL never names, throws "Unused parameter properties on type 'X'".

## Getting Help

- **Documentation**: Browse this `docs/` directory
- **API Reference**: [`../01-api-reference/README.md`](../01-api-reference/README.md)
