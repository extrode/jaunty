# Jaunty API Reference

## Introduction

Jaunty is a high-performance micro-ORM for .NET that executes SQL and maps results to objects with strict mapping by default. This documentation provides comprehensive reference for all public APIs.

## Quick Start

```csharp
using var connection = new SqlConnection(connectionString);

// Query
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 });

// Insert
var newId = connection.Insert(new Product { ProductName = "Widget Pro", CategoryId = 1, Price = 24.99m });

// Fluent, LINQ-like query building
var cheapWidgets = connection.From<Product>()
    .Where(p => p.CategoryId == 1)
    .WhereBetween(p => p.Price, 0m, 25m)
    .OrderBy(p => p.ProductName)
    .Select();
```

## Core Concepts

### Extension Methods
All Jaunty methods are extension methods on `IDbConnection`, providing a fluent API for database operations.

### Mapping Modes
- **Strict Mode** (default): All entity properties must have matching columns in the result set
- **Partial Mode**: Only maps properties with matching columns, ignores missing ones

### Command Options
Common options for controlling command execution:
- Transaction
- Command timeout
- Custom mapper
- Command type (Text or StoredProcedure)

## API Documentation

### Query Operations
- [Query Methods](query-methods.md) - Basic query operations with strict and partial mapping
- [Scalar Methods](scalar-methods.md) - Scalar value operations
- [Single Result Methods](single-result-methods.md) - Operations returning single results
- [Streaming Methods](streaming-methods.md) - Memory-efficient streaming operations

### Multiple Result Sets
- [Multiple Result Sets](multiple-result-sets.md) - Operations for handling multiple result sets with GridReader

### Multi-Entity Mapping
- [Multi-Entity Mapping](multi-entity-mapping.md) - Mapping joined query results to two or more entity types (`Query<T1,...,T7>`), ordinal claiming, and custom per-position mappers

### Data Modification
- [CRUD Operations](crud-operations.md) - Insert, Update, Delete operations
- [Get and Execute Operations](get-and-execute-operations.md) - Key-based Get/GetAll retrieval, raw Execute, and ExecuteBatch
- [Stored Procedures](stored-procedures.md) - Stored procedure execution with input, output, and return parameters

### Query Building
- [Fluent API](fluent-api.md) - Type-safe query builder with LINQ-like syntax

### Configuration and Attributes
- [Configuration](configuration.md) - Global configuration options and CommandOptions
- [Attributes](attributes.md) - Mapping attributes for customizing entity mapping

### Complete Reference
- [API Summary](api-summary.md) - Complete overview of all public APIs

## Key Features

### Strict by Default
Jaunty uses strict mapping by default, ensuring all entity properties have matching columns in the result set. This prevents silent mapping bugs that can occur with loose mapping.

### Performance Optimized
- Metadata caching per-type for optimal performance
- Compiled expression trees for property mapping
- Minimal allocations during query execution

### Modern C# Support
- C# 13 extension syntax
- Full async/await support
- IAsyncEnumerable for streaming large result sets
- Records and primary constructors support

### Flexible Mapping
- Strict and partial mapping modes
- Attribute-based mapping configuration
- Global configuration via JauntyConfig
- Custom mapper support via CommandOptions