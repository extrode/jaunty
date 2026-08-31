# Jaunty.Fluent Bulk Operations Spec

**Created**: 2026-03-09  
**Status**: Proposed  
**Related**: `work/archive/2026-03-10-fluent-improvement-tasklist.md` #1

---

## Summary

Design fluent API for bulk operations (BulkInsert, BulkUpdate, BulkDelete) matching core Jaunty functionality with fluent builder pattern.

---

## Core Jaunty Reference

```csharp
// Core Jaunty API
connection.BulkInsert(entities);
connection.BulkInsertAsync(entities, cancellationToken);
connection.BulkUpdate(entities);
connection.BulkUpdateAsync(entities, cancellationToken);
connection.BulkDelete<T>(entities);
connection.BulkDeleteAsync<T>(entities, cancellationToken);
```

**Options:**
- `CommandOptions<T>` with transaction, timeout
- `BulkCopyOptions` for dialect-specific settings

---

## Proposed Fluent API Design

### BulkInsert

```csharp
// Basic usage - infer all columns
await db.BulkInsert<Product>()
    .From(products)
    .ExecuteAsync();

// With column selection
await db.BulkInsert<Product>()
    .From(products)
    .Columns(p => p.ProductName, p => p.CategoryId, p => p.UnitPrice)
    .ExecuteAsync();

// With batch size
await db.BulkInsert<Product>()
    .From(products)
    .WithBatchSize(1000)
    .ExecuteAsync();

// With transaction
using var tx = connection.BeginTransaction();
await db.BulkInsert<Product>()
    .From(products)
    .WithTransaction(tx)
    .ExecuteAsync();

// With identity insert (SQL Server)
await db.BulkInsert<Product>()
    .From(products)
    .EnableIdentityInsert()
    .ExecuteAsync();

// Get inserted IDs (for identity columns)
var insertedIds = await db.BulkInsert<Product>()
    .From(products)
    .WithIdentityOutput()
    .ExecuteAsync();
// insertedIds: IEnumerable<int>
```

### BulkUpdate

```csharp
// Basic usage - update all columns
await db.BulkUpdate<Product>()
    .From(products)
    .ExecuteAsync();

// With column selection
await db.BulkUpdate<Product>()
    .From(products)
    .Columns(p => p.ProductName, p => p.UnitPrice)
    .ExecuteAsync();

// With key specification (if not using [Key] attribute)
await db.BulkUpdate<Product>()
    .From(products)
    .Key(p => p.ProductId)
    .Columns(p => p.ProductName, p => p.UnitPrice)
    .ExecuteAsync();

// With transaction
using var tx = connection.BeginTransaction();
await db.BulkUpdate<Product>()
    .From(products)
    .WithTransaction(tx)
    .ExecuteAsync();
```

### BulkDelete

```csharp
// Basic usage - delete by key
await db.BulkDelete<Product>()
    .From(productIds)  // IEnumerable<int>
    .ExecuteAsync();

// With key specification
await db.BulkDelete<Product>()
    .From(productIds)
    .Key(id => id)
    .ExecuteAsync();

// Delete entities (extracts keys)
await db.BulkDelete<Product>()
    .From(products)
    .ExecuteAsync();

// With transaction
using var tx = connection.BeginTransaction();
await db.BulkDelete<Product>()
    .From(productIds)
    .WithTransaction(tx)
    .ExecuteAsync();
```

---

## Interface Design

### IBulkInsertClause<T>

```csharp
public interface IBulkInsertClause<T> where T : new()
{
    IBulkInsertFromClause<T> From(IEnumerable<T> entities);
}

public interface IBulkInsertFromClause<T> where T : new()
{
    IBulkInsertOptionsClause<T> Columns(Expression<Func<T, object?>>[] columns);
    IBulkInsertOptionsClause<T> EnableIdentityInsert();
    IBulkInsertOptionsClause<T> WithIdentityOutput();
    IBulkInsertOptionsClause<T> WithBatchSize(int batchSize);
    IBulkInsertOptionsClause<T> WithTransaction(IDbTransaction transaction);
    IBulkInsertOptionsClause<T> WithTimeout(int timeout);
    
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
    int Execute();
}

public interface IBulkInsertOutputClause<T> where T : new()
{
    Task<IEnumerable<TIdentity>> ExecuteAsync<TIdentity>(CancellationToken cancellationToken = default);
    IEnumerable<TIdentity> Execute<TIdentity>();
}
```

### IBulkUpdateClause<T>

```csharp
public interface IBulkUpdateClause<T> where T : new()
{
    IBulkUpdateFromClause<T> From(IEnumerable<T> entities);
}

public interface IBulkUpdateFromClause<T> where T : new()
{
    IBulkUpdateOptionsClause<T> Key(Expression<Func<T, object?>> keySelector);
    IBulkUpdateOptionsClause<T> Columns(Expression<Func<T, object?>>[] columns);
    IBulkUpdateOptionsClause<T> WithTransaction(IDbTransaction transaction);
    IBulkUpdateOptionsClause<T> WithTimeout(int timeout);
    IBulkUpdateOptionsClause<T> WithBatchSize(int batchSize);
    
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
    int Execute();
}
```

### IBulkDeleteClause<T>

```csharp
public interface IBulkDeleteClause<T> where T : new()
{
    IBulkDeleteFromClause<T, TKey> From<TKey>(IEnumerable<TKey> keys);
    IBulkDeleteFromClause<T, int> From(IEnumerable<T> entities);
}

public interface IBulkDeleteFromClause<T, TKey> where T : new()
{
    IBulkDeleteOptionsClause<T, TKey> Key(Expression<Func<T, TKey>> keySelector);
    IBulkDeleteOptionsClause<T, TKey> WithTransaction(IDbTransaction transaction);
    IBulkDeleteOptionsClause<T, TKey> WithTimeout(int timeout);
    IBulkDeleteOptionsClause<T, TKey> WithBatchSize(int batchSize);
    
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
    int Execute();
}
```

---

## Implementation Structure

```
src/Jaunty.Fluent/Builders/Bulk/
├── BulkInsertBuilder.cs
├── BulkUpdateBuilder.cs
├── BulkDeleteBuilder.cs
└── BulkCopyOptions.cs
```

### BulkInsertBuilder<T> (Partial)

```csharp
internal sealed partial class BulkInsertBuilder<T> : IBulkInsertClause<T>, IBulkInsertFromClause<T>
    where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private IEnumerable<T> _entities = Enumerable.Empty<T>();
    private IReadOnlyList<ColumnMetadata>? _columns;
    private int _batchSize = 1000;
    private IDbTransaction? _transaction;
    private int? _timeout;
    private bool _enableIdentityInsert;
    private bool _withIdentityOutput;

    internal BulkInsertBuilder(IDbConnection connection)
    {
        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _metadata = FluentMetadataCache.GetMetadata<T>();
    }

    public IBulkInsertFromClause<T> From(IEnumerable<T> entities)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        return this;
    }

    public IBulkInsertOptionsClause<T> Columns(params Expression<Func<T, object?>>[] columnSelectors)
    {
        var propertyNames = PropertyExtractor.ExtractPropertyNames(columnSelectors);
        _columns = _metadata.Columns
            .Where(c => propertyNames.Contains(c.Property.Name))
            .ToList();
        return this;
    }

    public IBulkInsertOptionsClause<T> EnableIdentityInsert()
    {
        _enableIdentityInsert = true;
        return this;
    }

    public IBulkInsertOptionsClause<T> WithIdentityOutput()
    {
        _withIdentityOutput = true;
        return this;
    }

    public IBulkInsertOptionsClause<T> WithBatchSize(int batchSize)
    {
        _batchSize = batchSize > 0 ? batchSize : throw new ArgumentOutOfRangeException(nameof(batchSize));
        return this;
    }

    public IBulkInsertOptionsClause<T> WithTransaction(IDbTransaction transaction)
    {
        _transaction = transaction;
        return this;
    }

    public IBulkInsertOptionsClause<T> WithTimeout(int timeout)
    {
        _timeout = timeout;
        return this;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var entityList = _entities.ToList();
        if (entityList.Count == 0) return 0;

        var columnsToInsert = _columns ?? _metadata.NonIdentityColumns;
        
        // Use native bulk copy if available (SQL Server)
        if (_dialect.SupportsNativeBulkCopy && _connection is DbConnection dbConnection)
        {
            return await ExecuteNativeBulkCopyAsync(dbConnection, entityList, columnsToInsert, cancellationToken);
        }

        // Fallback to multi-row INSERT
        return await ExecuteMultiRowInsertAsync(entityList, columnsToInsert, cancellationToken);
    }

    private async Task<int> ExecuteNativeBulkCopyAsync(
        DbConnection connection,
        List<T> entities,
        IReadOnlyList<ColumnMetadata> columns,
        CancellationToken cancellationToken)
    {
        // SQL Server: Use SqlBulkCopy
        // PostgreSQL: Use COPY
        // MySQL: Use LOAD DATA INFILE
        // SQLite: Use INSERT in transaction
        throw new NotImplementedException("Dialect-specific bulk copy not implemented");
    }

    private async Task<int> ExecuteMultiRowInsertAsync(
        List<T> entities,
        IReadOnlyList<ColumnMetadata> columns,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(512);
        sb.Append("INSERT INTO ");
        sb.Append(_dialect.EscapeTableName(_metadata.SchemaName, _metadata.TableName));
        sb.Append(" (");
        sb.AppendColumns(columns, _dialect);
        sb.Append(") VALUES ");

        var parameters = new ParameterCollection();
        var valueRows = new List<string>();

        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var valuePlaceholders = new string[columns.Count];

            for (int j = 0; j < columns.Count; j++)
            {
                var col = columns[j];
                var paramName = $"{_dialect.ParameterPrefix}{col.Property.Name}_{i}_{j}";
                valuePlaceholders[j] = paramName;
                parameters.Add(paramName, col.Property.GetValue(entity));
            }

            valueRows.Add($"({string.Join(", ", valuePlaceholders)})");
        }

        sb.Append(string.Join(", ", valueRows));

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) await _connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sb.ToString();
            command.BindParameters(parameters);
            command.Transaction = _transaction;
            if (_timeout.HasValue) command.CommandTimeout = _timeout.Value;

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }
    }
}
```

---

## Dialect-Specific Considerations

### SQL Server

```csharp
// Use SqlBulkCopy for maximum performance
// Supports: SET IDENTITY_INSERT, OUTPUT clause
if (_dialect is SqlServerDialect && _connection is SqlConnection sqlConn)
{
    using var bulkCopy = new SqlBulkCopy(sqlConn, SqlBulkCopyOptions.Default, _transaction);
    bulkCopy.DestinationTableName = _metadata.TableName;
    bulkCopy.BatchSize = _batchSize;
    bulkCopy.BulkCopyTimeout = _timeout ?? 30;
    
    // Map columns
    foreach (var col in columns)
    {
        bulkCopy.ColumnMappings.Add(col.Property.Name, col.ColumnName);
    }
    
    var dataTable = EntitiesToDataTable(entities, columns);
    await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
}
```

### PostgreSQL

```csharp
// Use COPY command for bulk insert
// Supports: Binary format, high performance
if (_dialect is PostgreSqlDialect)
{
    // Npgsql supports binary COPY
    await using var writer = await conn.BeginBinaryImportAsync(
        $"COPY {_metadata.TableName} ({columnList}) FROM STDIN (FORMAT BINARY)", 
        cancellationToken);
    
    foreach (var entity in entities)
    {
        await writer.StartRowAsync(cancellationToken);
        foreach (var col in columns)
        {
            await writer.WriteAsync(col.Property.GetValue(entity), cancellationToken);
        }
    }
    
    return await writer.CompleteAsync(cancellationToken);
}
```

### MySQL

```csharp
// Use LOAD DATA INFILE or multi-row INSERT
// SQLite doesn't have native bulk copy, use transactional INSERT
if (_dialect is MySqlDialect)
{
    // Option 1: Multi-row INSERT (works everywhere)
    // Option 2: LOAD DATA INFILE (requires file access)
}
```

### SQLite

```csharp
// Use transactional INSERT with PRAGMA optimizations
if (_dialect is SqliteDialect)
{
    // Optimize for bulk insert
    await ExecuteNonQueryAsync("PRAGMA journal_mode = MEMORY", transaction);
    await ExecuteNonQueryAsync("PRAGMA synchronous = OFF", transaction);
    
    // Then use multi-row INSERT
    return await ExecuteMultiRowInsertAsync(entities, columns, cancellationToken);
}
```

---

## Test Plan

### Integration Tests

```csharp
public class FluentBulkInsertTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    [Fact]
    public void BulkInsert_EmptyCollection_ReturnsZero()
    {
        var count = _fixture.Connection.BulkInsert<Product>()
            .From(Enumerable.Empty<Product>())
            .Execute();

        Assert.Equal(0, count);
    }

    [Fact]
    public void BulkInsert_SingleEntity_ReturnsOne()
    {
        var products = new[] { new Product { ProductName = "Test", CategoryId = 1 } };
        
        var count = _fixture.Connection.BulkInsert<Product>()
            .From(products)
            .Execute();

        Assert.Equal(1, count);
    }

    [Fact]
    public void BulkInsert_MultipleEntities_ReturnsCount()
    {
        var products = new[]
        {
            new Product { ProductName = "Test1", CategoryId = 1 },
            new Product { ProductName = "Test2", CategoryId = 1 },
            new Product { ProductName = "Test3", CategoryId = 1 }
        };

        var count = _fixture.Connection.BulkInsert<Product>()
            .From(products)
            .Execute();

        Assert.Equal(3, count);
    }

    [Fact]
    public void BulkInsert_WithColumnSelection_OnlyInsertsSelectedColumns()
    {
        var products = new[]
        {
            new Product { ProductName = "Test", CategoryId = 1, UnitPrice = 10.00m }
        };

        var count = _fixture.Connection.BulkInsert<Product>()
            .From(products)
            .Columns(p => p.ProductName, p => p.CategoryId)
            .Execute();

        Assert.Equal(1, count);
    }

    [Fact]
    public void BulkInsert_WithTransaction_CommitPersistsData()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var products = new[] { new Product { ProductName = "Test", CategoryId = 1 } };

        var count = _fixture.Connection.BulkInsert<Product>()
            .From(products)
            .WithTransaction(tx)
            .Execute();

        tx.Commit();

        Assert.Equal(1, count);
        // Verify data persisted
    }

    [Fact]
    public void BulkInsert_WithTransaction_RollbackDiscardsData()
    {
        using var tx = _fixture.Connection.BeginTransaction();
        var products = new[] { new Product { ProductName = "Test", CategoryId = 1 } };

        _fixture.Connection.BulkInsert<Product>()
            .From(products)
            .WithTransaction(tx)
            .Execute();

        tx.Rollback();

        // Verify no data inserted
    }

    [Fact]
    public async Task BulkInsertAsync_WithCancellationToken_CancelsOperation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var products = new[] { new Product { ProductName = "Test", CategoryId = 1 } };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _fixture.Connection.BulkInsert<Product>()
                .From(products)
                .ExecuteAsync(cts.Token));
    }
}
```

### Unit Tests

```csharp
public class BulkInsertBuilderTests
{
    [Fact]
    public void From_NullEntities_ThrowsArgumentNullException()
    {
        var builder = new BulkInsertBuilder<Product>(new MockConnection());
        
        Assert.Throws<ArgumentNullException>(() => builder.From(null!));
    }

    [Fact]
    public void WithBatchSize_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = new BulkInsertBuilder<Product>(new MockConnection());
        
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            builder.From(new[] { new Product() }).WithBatchSize(0));
    }

    [Fact]
    public void Columns_ExtractsCorrectColumnNames()
    {
        // Test column extraction logic
    }
}
```

---

## Performance Considerations

1. **Batch Size**: Default 1000, configurable via `WithBatchSize()`
2. **Native vs Fallback**: Use native bulk copy when available (SqlBulkCopy, COPY)
3. **Memory**: Stream entities for large datasets, don't materialize full list
4. **Transaction**: Always use transaction for bulk operations
5. **Column Selection**: Only include necessary columns to reduce data transfer

---

## Implementation Checklist

- [ ] **SPEC**: This spec reviewed and approved
- [ ] **TEST**: Create `FluentBulkInsertTests.cs`, `FluentBulkUpdateTests.cs`, `FluentBulkDeleteTests.cs`
- [ ] **IMPL**: `BulkInsertBuilder<T>` with multi-row INSERT fallback
- [ ] **IMPL**: `BulkUpdateBuilder<T>` with dialect-specific UPDATE
- [ ] **IMPL**: `BulkDeleteBuilder<T>` with WHERE IN clause
- [ ] **IMPL**: SQL Server native bulk copy (SqlBulkCopy)
- [ ] **IMPL**: PostgreSQL native bulk copy (COPY)
- [ ] **TEST**: Integration tests for all builders
- [ ] **TEST**: Dialect-specific tests (SQL Server, PostgreSQL, MySQL, SQLite)
- [ ] **BENCHMARK**: Compare native vs fallback performance
- [ ] **DOC**: XML documentation for all public APIs
- [ ] **VERIFY**: Code coverage > 80%

---

## Estimated Effort

- **Spec Review**: 0.5 days
- **Implementation**: 3 days
- **Testing**: 1.5 days
- **Documentation**: 0.5 days
- **Total**: 5.5 days
