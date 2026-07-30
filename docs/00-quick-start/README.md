# Quick Start Guide

Welcome to Jaunty! This guide gets you up and running quickly.

## What is Jaunty?

Jaunty is a lightweight, high-performance micro-ORM for .NET that:
- Executes your SQL and maps results to objects
- Uses strict mapping by default (catches bugs early)
- Has zero external dependencies
- Targets `netstandard2.0` and `net8.0`

```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });
```

## Prerequisites

- .NET SDK 8.0 or later
- Visual Studio 2022 or VS Code
- SQLite (for running tests)

## Build the Project

```bash
# Clone the repository
git clone https://github.com/beparey/jaunty.git
cd Jaunty

# Build all targets
dotnet build

# Build specific target
dotnet build -f net8.0
```

## Run Tests

```bash
# Run all tests
dotnet test

# Run integration tests only
dotnet test --filter "Category=Integration"

# Run specific test class
dotnet test --filter "FullyQualifiedName~QueryTests"
```

## Basic Usage

### 1. Add the Package

```bash
dotnet add package Beparey.Jaunty
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
| **Strict** | `Query<T>()` | All properties must have columns or throws |
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

// Positional parameters (parsed from SQL)
connection.Query<Product>(sql, 1, 100);  // @CategoryId=1, @MinPrice=100
```

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

### "Parameter count mismatch"

You passed more/fewer parameters than your SQL contains. Check:
- Parameter names match between SQL and object
- No typos in `@ParameterName`
- SQL comments don't contain false `@params`

## Getting Help

- **Documentation**: Browse this `docs/` directory
- **API Reference**: [`../01-api-reference/README.md`](../01-api-reference/README.md)

