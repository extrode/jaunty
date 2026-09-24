# Jaunty Test Implementation Guide

**Purpose**: Guide for developers writing tests for the Jaunty micro-ORM
**Created**: 2026-02-19
**Last Updated**: 2026-02-19

> **CRITICAL RULES** - Before writing any tests:
> - **Never modify existing tests** without explicit user permission
> - **Never delete tests or code** to make tests pass
> - **Never change production code** unless fixing a documented bug
> - **Always read existing test files** before adding new tests
> - **Never overwrite test files** - append to existing files

This document provides patterns, examples, and best practices for writing tests to achieve 100% code coverage of the Jaunty micro-ORM.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Test Frameworks](#test-frameworks)
3. [Test Entities](#test-entities)
4. [Helper Classes](#helper-classes)
5. [Integration Test Patterns](#integration-test-patterns)
6. [Unit Test Patterns](#unit-test-patterns)
7. [Write Operation Patterns](#write-operation-patterns)
8. [Streaming Test Patterns](#streaming-test-patterns)
9. [Multiple Result Set Patterns](#multiple-result-set-patterns)
10. [Stored Procedure Patterns](#stored-procedure-patterns)
11. [SQLite-Specific Considerations](#sqlite-specific-considerations)
12. [Common Patterns](#common-patterns)
13. [Troubleshooting](#troubleshooting)

---

## Project Structure

```
tests/
├── Jaunty.Tests/                          # Main test suite (~62 files, ~600+ tests)
│   ├── Entities/                          # Test models
│   │   ├── Product.cs                     # Full Northwind product (IEntity<int>, IMapped<Product>)
│   │   ├── ProductSummary.cs              # Partial mapping (3 properties)
│   │   ├── Category.cs                    # Northwind category with [Table], [Key], [Column]
│   │   ├── CategorySummary.cs             # Partial category mapping
│   │   ├── Customer.cs                    # Northwind customer
│   │   ├── CustomerSummary.cs             # Partial customer mapping
│   │   ├── Order.cs                       # Northwind order
│   │   ├── OrderSummary.cs                # Partial order mapping
│   │   ├── BulkTestEntity.cs              # Write test entity ([Table], [Key], [DatabaseGenerated])
│   │   └── ProductWithAttributes.cs       # Attribute testing
│   │
│   ├── Helpers/
│   │   ├── Database.cs                    # Northwind SQLite connection helper
│   │   ├── SQLiteTestBase.cs              # Base classes for SQLite tests
│   │   └── SkipSQLiteAsyncAttribute.cs    # Skip attributes for async limitations
│   │
│   ├── Integration/Sqlite/
│   │   ├── Configuration/                 # ConfigurationTests, ConfigResolverTests
│   │   ├── Infrastructure/                # QueryDatabaseConnectionTests
│   │   ├── Multiple/                      # QueryMultipleTests, GridReaderTests (sync + async)
│   │   ├── Read/                          # ~29 test files covering all Query* methods
│   │   ├── Streaming/                     # QueryStream*, QueryPartialStream* (sync + async)
│   │   └── Write/                         # BulkOperationsTests (sync + async), UpsertTests
│   │
│   └── Unit/Read/                         # CommandOptionsTests, SqlParameterParserTests, ParameterBinderTests
│
├── Jaunty.Fluent.Tests/                   # Fluent API tests (~18 files)
│   └── Integration/                       # FluentSelect, FluentWhere, FluentJoin, etc.
│
└── Jaunty.Scaffolding.Tests/              # Scaffolding tests (~8 files)
    ├── Integration/                       # ScaffolderIntegrationTests, SQLiteSchemaReaderTests
    └── Unit/                              # EntityCodeGeneratorTests, NamingHelperTests, TypeMapperTests
```

---

## Test Frameworks

### Jaunty.Tests

```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
<PackageReference Include="System.Data.SQLite" Version="1.0.119" />
```

### Jaunty.Fluent.Tests

```xml
<!-- Same as Jaunty.Tests plus FluentAssertions -->
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

### Assertion Styles

- **Jaunty.Tests**: xUnit assertions (`Assert.Equal`, `Assert.NotNull`, `Assert.Throws`)
- **Jaunty.Fluent.Tests**: FluentAssertions (`Should().Be()`, `Should().NotBeEmpty()`)
- **Jaunty.Scaffolding.Tests**: Mix of both

**Match the assertion style of the test project you're adding to.**

---

## Test Entities

### Product (Full Strict Mapping)

```csharp
// tests/Jaunty.Tests/Entities/Product.cs
public class Product : IEntity<int>, IMapped<Product>
{
    [Ignore]
    public int Id { get => ProductId; set => ProductId = value; }

    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;

    [Column("supplier_id")]  public int? SupplierId { get; set; }
    [Column("category_id")]  public short? CategoryId { get; set; }
    [Column("quantity_per_unit")] public string? QuantityPerUnit { get; set; }
    [Column("unit_price")]   public decimal? UnitPrice { get; set; }
    [Column("units_in_stock")] public short? UnitsInStock { get; set; }
    [Column("units_on_order")] public short? UnitsOnOrder { get; set; }
    [Column("reorder_level")] public short? ReorderLevel { get; set; }
    [Column("discontinued")] public bool Discontinued { get; set; }

    // IMapped<Product> custom mapper
    public static Product ReadEntity(IDataReader reader) { /* ... */ }
}
```

### ProductSummary (Partial Mapping)

```csharp
// tests/Jaunty.Tests/Entities/ProductSummary.cs
public class ProductSummary
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
}
```

### BulkTestEntity (Write Operations)

```csharp
// tests/Jaunty.Tests/Entities/BulkTestEntity.cs
[Table("bulk_test")]
public class BulkTestEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("value")]
    public int Value { get; set; }
}
```

### Category (CRUD Entity)

```csharp
// tests/Jaunty.Tests/Entities/Category.cs
[Table("categories")]
public class Category
{
    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("category_name")]
    public string CategoryName { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }
}
```

### When to Use Each Entity

| Entity | Use For | Mapping Mode |
|--------|---------|-------------|
| `Product` | Strict query tests, attribute tests, IMapped tests | Strict |
| `ProductSummary` | Partial query tests (fewer columns than table) | Partial |
| `Category` | CRUD tests, simpler strict queries | Strict |
| `CategorySummary` | Partial category queries | Partial |
| `BulkTestEntity` | Write operations (in-memory SQLite) | N/A |
| `Customer` / `Order` | Multi-entity queries, joins | Strict |

---

## Helper Classes

### Database.cs - Northwind Connection

```csharp
// tests/Jaunty.Tests/Helpers/Database.cs
public class Database : IDisposable
{
    private readonly SQLiteConnection _connection;
    public IDbConnection Connection => _connection;

    public Database()
    {
        _connection = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
    }

    public void Dispose() => _connection?.Dispose();
}
```

**Usage**: All read-only integration tests use this. Connection opens lazily on first query.

### SQLiteTestBase - Base Class

```csharp
// tests/Jaunty.Tests/Helpers/SQLiteTestBase.cs
public abstract class SQLiteTestBase : IDisposable
{
    protected internal Database _db;
    protected SQLiteTestBase() => _db = new Database();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }
}
```

### SkipSQLiteAsyncAttribute

```csharp
// For tests that hit SQLite async DataReader limitations
[SkipSQLiteAsyncFact]
public async Task SomeAsyncTest_ThatHitsLimitation() { }

[SkipSQLiteAsyncTheory]
[InlineData("test")]
public async Task SomeAsyncTheory_ThatHitsLimitation(string input) { }
```

---

## Integration Test Patterns

### Read Test (Northwind Database)

This is the most common pattern. Uses the shared Northwind SQLite database for read-only queries.

```csharp
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryPartialFirstTests : IDisposable
{
    private readonly Database _db;

    private const string PartialColumns = @"
        product_id AS ProductId,
        product_name AS ProductName,
        category_id AS CategoryId";

    public QueryPartialFirstTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryPartialFirst_WithResults_ReturnsFirst()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            $"SELECT {PartialColumns} FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public void QueryPartialFirst_WithParameters_FiltersCorrectly()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            $"SELECT {PartialColumns} FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.Equal(1, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirst_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryPartialFirst<ProductSummary>(
                $"SELECT {PartialColumns} FROM products WHERE product_id = -1"));
    }

    [Fact]
    public void QueryPartialFirst_WithCommandOptions_Works()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            $"SELECT {PartialColumns} FROM products WHERE product_id = @Id",
            new { Id = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.Equal(1, product.ProductId);
    }
}
```

### Async Read Test

```csharp
namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryPartialFirstAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialFirstAsyncTests() => _db = new Database();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstAsync_WithResults_ReturnsFirst()
    {
        var product = await _db.Connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstAsync_NoResults_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _db.Connection.QueryPartialFirstAsync<ProductSummary>(
                "SELECT product_id AS ProductId FROM products WHERE product_id = -1"));
    }
}
```

### SQL Column Constants

For strict mapping tests, you must select ALL columns that the entity expects. Use a constant:

```csharp
private const string FullProductColumns = @"
    product_id AS ProductId,
    product_name AS ProductName,
    supplier_id, category_id, quantity_per_unit, unit_price,
    units_in_stock, units_on_order, reorder_level, discontinued";
```

For partial mapping tests, select only a subset:

```csharp
private const string PartialColumns = @"
    product_id AS ProductId,
    product_name AS ProductName";
```

---

## Unit Test Patterns

### Testing Internal Classes

Unit tests go in `tests/Jaunty.Tests/Unit/{Category}/` and test internals without a database.

```csharp
namespace Jaunty.Tests.Unit.Read;

public class CommandOptionsTests
{
    [Fact]
    public void Default_HasNullValues()
    {
        var options = default(CommandOptions);

        Assert.Null(options.Transaction);
        Assert.Null(options.Timeout);
    }

    [Fact]
    public void WithTimeout_SetsTimeoutOnly()
    {
        var options = CommandOptions.WithTimeout(30);

        Assert.Equal(30, options.Timeout);
        Assert.Null(options.Transaction);
    }

    [Fact]
    public void IsReadonlyStruct()
    {
        var type = typeof(CommandOptions);

        Assert.True(type.IsValueType);
    }
}
```

### Testing SqlParameterParser

```csharp
namespace Jaunty.Tests.Unit.Read;

public class SqlParameterParserTests
{
    [Fact]
    public void ExtractParameterNames_SingleParameter_ReturnsName()
    {
        var names = SqlParameterParser.ExtractParameterNames(
            "SELECT * FROM products WHERE id = @Id");

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterInBlockComment_Ignored()
    {
        var names = SqlParameterParser.ExtractParameterNames(
            "SELECT * FROM products /* WHERE id = @Id */");

        Assert.Empty(names);
    }
}
```

---

## Write Operation Patterns

### In-Memory SQLite (for Write Tests)

Write tests must NOT modify the shared Northwind database. Use an in-memory SQLite connection.

```csharp
using System.Data;
using System.Data.SQLite;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.Sqlite.Write;

public class InsertTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public InsertTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        CreateTestTable();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _connection?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void CreateTestTable()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE bulk_test (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                value INTEGER NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    private void ClearTestTable()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bulk_test";
        cmd.ExecuteNonQuery();
    }

    private int GetRowCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bulk_test";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public void Insert_SingleEntity_ReturnsIdentity()
    {
        var entity = new BulkTestEntity { Name = "Test", Value = 42 };

        long id = _connection.Insert(entity);

        Assert.True(id > 0);
        Assert.Equal(1, GetRowCount());
    }

    [Fact]
    public void Insert_WithCommandOptions_RespectsTransaction()
    {
        using var tx = _connection.BeginTransaction();
        var entity = new BulkTestEntity { Name = "Test", Value = 42 };

        long id = _connection.Insert(entity, CommandOptions.WithTransaction(tx));

        Assert.True(id > 0);
        tx.Commit();
        Assert.Equal(1, GetRowCount());
    }
}
```

### Update Tests

```csharp
[Fact]
public void Update_ExistingEntity_ReturnsAffectedRows()
{
    var entity = new BulkTestEntity { Name = "Original", Value = 100 };
    long id = _connection.Insert(entity);
    entity.Id = id;

    entity.Name = "Updated";
    int rows = _connection.Update(entity);

    Assert.Equal(1, rows);
}
```

### Delete Tests

```csharp
[Fact]
public void Delete_ExistingEntity_ReturnsAffectedRows()
{
    var entity = new BulkTestEntity { Name = "ToDelete", Value = 999 };
    long id = _connection.Insert(entity);
    entity.Id = id;

    int rows = _connection.Delete(entity);

    Assert.Equal(1, rows);
    Assert.Equal(0, GetRowCount());
}

[Fact]
public void Delete_ById_ReturnsAffectedRows()
{
    var entity = new BulkTestEntity { Name = "ToDelete", Value = 999 };
    long id = _connection.Insert(entity);

    int rows = _connection.Delete<BulkTestEntity>(id);

    Assert.Equal(1, rows);
}
```

---

## Streaming Test Patterns

### QueryStream (Strict)

```csharp
namespace Jaunty.Tests.Integration.Sqlite.Streaming;

public class QueryPartialUnbufferedTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialUnbufferedTests() => _db = new Database();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryPartialUnbuffered_ReturnsLazyEnumerable()
    {
        var products = _db.Connection.QueryPartialUnbuffered<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(products);

        var list = products.ToList();
        Assert.NotEmpty(list);
        Assert.True(list[0].ProductId > 0);
    }

    [Fact]
    public void QueryPartialUnbuffered_WithParameters_FiltersCorrectly()
    {
        var products = _db.Connection.QueryPartialUnbuffered<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CatId",
            new { CatId = 1 });

        var list = products.ToList();
        Assert.NotEmpty(list);
    }

    [Fact]
    public void QueryPartialUnbuffered_NoResults_ReturnsEmptyEnumerable()
    {
        var products = _db.Connection.QueryPartialUnbuffered<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -1");

        Assert.Empty(products);
    }
}
```

---

## Multiple Result Set Patterns

### GridReader

```csharp
namespace Jaunty.Tests.Integration.Sqlite.Multiple;

public class GridReaderPartialTests : IDisposable
{
    private readonly Database _db;

    public GridReaderPartialTests() => _db = new Database();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void ReadPartialFirst_ReturnsFirstRow()
    {
        const string sql = @"
            SELECT product_id AS ProductId, product_name AS ProductName FROM products;
            SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories;";

        using var reader = _db.Connection.QueryMultiple(sql);

        var product = reader.ReadPartialFirst<ProductSummary>();
        Assert.True(product.ProductId > 0);

        var category = reader.ReadPartialFirst<CategorySummary>();
        Assert.True(category.CategoryId > 0);
    }
}
```

---

## Stored Procedure Patterns

SQLite does not support stored procedures. StoredProcedure tests require SQL Server or PostgreSQL.

### Skip Pattern (SQLite)

```csharp
namespace Jaunty.Tests.Integration.Sqlite.StoredProcedure;

public class StoredProcedureTests
{
    [Fact(Skip = "Requires SQL Server - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities() { }

    [Fact(Skip = "Requires SQL Server - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirst_ReturnsFirstEntity() { }

    [Fact(Skip = "Requires SQL Server - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue() { }

    [Fact(Skip = "Requires SQL Server - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureNonQuery_ReturnsAffectedRows() { }
}
```

### SQL Server Pattern (if available)

```csharp
// Only when SQL Server connection is available
public class StoredProcedureSqlServerTests : IDisposable
{
    private readonly IDbConnection _connection;

    public StoredProcedureSqlServerTests()
    {
        _connection = new SqlConnection("Server=...;Database=Northwind;...");
        _connection.Open();
    }

    [Fact]
    public void ExecuteStoredProcedure_CustOrderHist_ReturnsResults()
    {
        var results = _connection.ExecuteStoredProcedure<OrderHistory>(
            "CustOrderHist",
            new { CustomerID = "ALFKI" });

        Assert.NotEmpty(results);
    }
}
```

---

## SQLite-Specific Considerations

### Async Limitations

SQLite's `System.Data.SQLite` provider has known async `DataReader` limitations. The `GetName()` method fails in async contexts.

**When to use `[SkipSQLiteAsyncFact]`:**
- Any `QueryMultipleAsync` test that reads column metadata
- Tests that call async methods which internally access `DataReader.GetName()`

```csharp
[SkipSQLiteAsyncFact]
public async Task QueryFirstAsync_WithResults_ReturnsFirst()
{
    // This test is skipped on SQLite due to async DataReader limitation
    var product = await _db.Connection.QueryFirstAsync<Product>(
        $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
        new { Id = 1 });

    Assert.Equal(1, product.ProductId);
}
```

### Connection State

Jaunty automatically manages connection state (opens if closed, closes after if it was closed). This is tested in `QueryConnectionStateTests.cs`. You do NOT need to manually open connections for read tests using `Database.cs`.

### In-Memory vs File-Based SQLite

| Scenario | Connection String | Notes |
|----------|------------------|-------|
| Read-only queries | `Data Source=../../../../../data/sqlite/Northwind.db` | Shared Northwind database |
| Write operations | `Data Source=:memory:` | In-memory, isolated per test |
| Concurrent tests | `Data Source=:memory:` | Each test gets its own database |

---

## Common Patterns

### Test Naming Convention

All tests follow: `Method_Scenario_ExpectedResult`

```
QueryPartialFirst_WithResults_ReturnsFirst
QueryPartialFirst_NoResults_Throws
Insert_SingleEntity_ReturnsIdentity
Delete_ById_ReturnsAffectedRows
BulkInsert_EmptyCollection_ReturnsZero
```

### Comment Policy

- No `// Arrange`, `// Act`, `// Assert` comments
- The AAA structure should be obvious from whitespace
- Only comment unexpected or non-obvious behavior

### Test Class Structure

```csharp
public class SomeTests : IDisposable
{
    // Fields
    private readonly Database _db;

    // Constants for reusable SQL
    private const string PartialColumns = "...";

    // Constructor (setup)
    public SomeTests() => _db = new Database();

    // Dispose (teardown)
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    // Helper methods (private)
    private void CreateTestTable() { /* ... */ }

    // Tests (public, [Fact] or [Theory])
    [Fact]
    public void Method_Scenario_Expected() { /* ... */ }
}
```

### Testing Each Overload

Many Jaunty methods have 4 overloads: `(sql)`, `(sql, params)`, `(sql, options)`, `(sql, params, options)`. Test at least the first two. The options overload can be tested with a simple timeout or transaction:

```csharp
[Fact]
public void QueryPartialFirst_WithSqlOnly_Works()
{
    var result = _db.Connection.QueryPartialFirst<ProductSummary>(
        "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

    Assert.NotNull(result);
}

[Fact]
public void QueryPartialFirst_WithParameters_Works()
{
    var result = _db.Connection.QueryPartialFirst<ProductSummary>(
        "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
        new { Id = 1 });

    Assert.Equal(1, result.ProductId);
}

[Fact]
public void QueryPartialFirst_WithCommandOptions_Works()
{
    var result = _db.Connection.QueryPartialFirst<ProductSummary>(
        "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
        new { Id = 1 },
        CommandOptions<ProductSummary>.WithTimeout(30));

    Assert.Equal(1, result.ProductId);
}
```

### Testing Exception Scenarios

```csharp
[Fact]
public void QueryFirst_NoResults_ThrowsInvalidOperation()
{
    var ex = Assert.Throws<InvalidOperationException>(() =>
        _db.Connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = -999"));

    Assert.Contains("Sequence contains no elements", ex.Message);
}

[Fact]
public void QuerySingle_MultipleResults_ThrowsInvalidOperation()
{
    Assert.Throws<InvalidOperationException>(() =>
        _db.Connection.QuerySingle<Product>(
            $"SELECT {FullProductColumns} FROM products"));
}
```

### Testing OrDefault Variants

```csharp
[Fact]
public void QueryFirstOrDefault_NoResults_ReturnsNull()
{
    var result = _db.Connection.QueryFirstOrDefault<Product>(
        $"SELECT {FullProductColumns} FROM products WHERE product_id = -999");

    Assert.Null(result);
}

[Fact]
public void QueryFirstOrDefault_WithResults_ReturnsEntity()
{
    var result = _db.Connection.QueryFirstOrDefault<Product>(
        $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
        new { Id = 1 });

    Assert.NotNull(result);
    Assert.Equal(1, result.ProductId);
}
```

### Theory Tests for Reusable Scenarios

```csharp
[Theory]
[InlineData(1)]
[InlineData(2)]
[InlineData(3)]
public void QueryPartialFirst_ByCategory_ReturnsCorrectCategory(int categoryId)
{
    var product = _db.Connection.QueryPartialFirst<ProductSummary>(
        "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CatId",
        new { CatId = categoryId });

    Assert.Equal(categoryId, product.CategoryId);
}
```

---

## Troubleshooting

### Common Issues

**1. Strict mapping failure**
```
Error: Strict mapping failed: entity property X has no matching column
```
Solution: Use `QueryPartial<T>()` instead of `Query<T>()` if you're selecting a subset of columns, OR select ALL columns that the entity requires.

**2. SQLite async DataReader limitation**
```
Error: GetName() not supported in async contexts
```
Solution: Use `[SkipSQLiteAsyncFact]` attribute. This is a known SQLite provider limitation.

**3. Stored procedure not supported**
```
Error: SQLite does not support stored procedures
```
Solution: Use `[Fact(Skip = "Requires SQL Server")]` for stored procedure tests.

**4. In-memory database loses data**
```
Error: Table not found / no such table
```
Solution: In-memory SQLite databases only persist while the connection is open. Create tables in the test constructor and keep the connection open for the test's lifetime.

**5. Column name mismatch**
```
Error: Strict mapping failed for column 'product_name'
```
Solution: Use SQL aliases (`product_name AS ProductName`) or `[Column("product_name")]` attributes on the entity.

### Running Specific Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Jaunty.Tests

# Run specific test class
dotnet test --filter "ClassName=QueryPartialFirstTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName=Jaunty.Tests.Integration.Sqlite.Read.QueryPartialFirstTests.QueryPartialFirst_WithResults_ReturnsFirst"

# Run by pattern
dotnet test --filter "FullyQualifiedName~QueryPartialFirst"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# With code coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Checklist for New Tests

Before submitting tests, verify:

- [ ] Test follows AAA pattern (whitespace-separated, no comments)
- [ ] Test has descriptive name: `Method_Scenario_ExpectedResult`
- [ ] Test uses correct entity (strict vs partial)
- [ ] Test doesn't depend on other tests
- [ ] Read tests use `Database.cs` (Northwind)
- [ ] Write tests use in-memory SQLite (`Data Source=:memory:`)
- [ ] Async tests use `[SkipSQLiteAsyncFact]` if needed
- [ ] Dispose calls `GC.SuppressFinalize(this)` before `_db.Dispose()`
- [ ] Follows same assertion style as existing tests in the project
- [ ] No duplicate tests (read existing file first)
- [ ] SQL aliases match entity property names for strict mapping

---

## When Existing Tests Fail

**DO NOT modify or delete existing tests.** Instead:

1. Document the failure:
   ```
   Test: QueryFirstTests.QueryFirst_NoResults_Throws
   Error: Assert.Contains() Failure - Expected: "Sequence contains no elements"
   ```

2. Analyze the cause:
   - Is the test wrong? (unlikely if it was passing before)
   - Is the code broken? (more likely)
   - Did a recent change break it?

3. Report to user and wait for permission:
   ```
   "Test X is failing because Y. May I [fix the test / fix the code]?"
   ```

---

## Next Steps

1. Review [coverage-checklist.md](./coverage-checklist.md) for uncovered methods
2. Pick items marked (uncovered) - start with **High Priority** items
3. Read existing test files in the same area to match patterns
4. Write tests following patterns in this guide
5. Run tests: `dotnet test`
6. Update checklist to when complete
7. Commit: `git commit -m "test: add tests for [ClassName]"`
