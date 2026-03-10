# Jaunty ORM: Feature Gap Analysis & Roadmap

**Last Updated:** 2026-03-11
**Status:** Draft - For Discussion

---

## Part 1: Completed Features (Production-Ready)

### 1.1 Core Query Operations

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| `Query<T>()` - Strict mapping | `src/Jaunty/Read/Query.cs` | Complete | All properties must have matching columns |
| `QueryPartial<T>()` - Partial mapping | `src/Jaunty/Read/QueryPartial.cs` | Complete | Maps only existing columns |
| `QueryFirst<T>()` / `QueryFirstOrDefault<T>()` | `src/Jaunty/Read/QueryFirst.cs` | Complete | Returns single or default |
| `QuerySingle<T>()` / `QuerySingleOrDefault<T>()` | `src/Jaunty/Read/QuerySingle.cs` | Complete | Validates exactly one result |
| `QueryScalar<T>()` | `src/Jaunty/Read/QueryScalar.cs` | Complete | First column of first row |
| `QueryStream<T>()` | `src/Jaunty/Streaming/QueryStream.cs` | Complete | Unbuffered streaming |
| `QueryAsync<T>()` | `src/Jaunty/Read/QueryAsync.cs` | Complete | Async all operations |
| `QueryMultiple<T>()` | `src/Jaunty/Multiple/QueryMultiple.cs` | Complete | Multiple result sets via GridReader |

### 1.2 Write Operations (CRUD)

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| `Insert<T>()` | `src/Jaunty/Write/Insert.cs` | Complete | Returns identity value (long) |
| `Update<T>()` | `src/Jaunty/Write/Update.cs` | Complete | Updates by primary key |
| `Delete<T>()` | `src/Jaunty/Write/Delete.cs` | Complete | Deletes by primary key or ID |
| `Upsert<T>()` | `src/Jaunty/Write/Upsert.cs` | Complete | Dialect-specific upsert |
| `BulkInsert<T>()` | `src/Jaunty/Write/BulkInsert.cs` | Complete | Multi-row or native bulk copy |
| `BulkUpdate<T>()` | `src/Jaunty/Write/BulkUpdate.cs` | Complete | Batched updates |
| `BulkDelete<T>()` | `src/Jaunty/Write/BulkDelete.cs` | Complete | Batched deletes |

### 1.3 Parameter Handling

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| Named parameters | `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | Complete | Anonymous types, dictionaries |
| Positional parameters | `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | Complete | Scalar value binding |
| Collection/IN expansion | `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | Complete | `WHERE id IN @Ids` → `(@p0,@p1,@p2)` |
| Empty collection handling | `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | Complete | Converts to `WHERE 1=0` |
| Parameter validation | `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | Complete | Count mismatch detection |
| SQL parsing (comments/strings) | `src/Jaunty/Internals/Parameters/SqlParameterParser.cs` | Complete | Skips literals and comments |
| Stored procedure parameters | `src/Jaunty/StoredProcedure/SpParameters.cs` | Complete | Input, output, return value |

### 1.4 Caching (Internal)

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| SQL template caching | `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | Complete | `CommandTemplate` per (SQL, Type) |
| Parameter metadata caching | `src/Jaunty/Internals/Parameters/ParameterCache.cs` | Complete | Compiled property getters |
| Entity metadata caching | `src/Jaunty/Internals/Read/MappedCache.cs` | Complete | Column mappings per type |
| CRUD SQL caching | `src/Jaunty/Internals/Write/CrudSqlCache.cs` | Complete | Generated INSERT/UPDATE/DELETE |
| Parameter parser caching | `src/Jaunty/Internals/Parameters/SqlParameterParserCache.cs` | Complete | Parsed parameter names |

### 1.5 Database Dialect Support

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| SQL Server dialect | `src/Jaunty/Dialects/SqlServerDialect.cs` | Complete | MERGE, OFFSET/FETCH, SCOPE_IDENTITY |
| PostgreSQL dialect | `src/Jaunty/Dialects/PostgreSqlDialect.cs` | Complete | RETURNING, COPY |
| MySQL/MariaDB dialect | `src/Jaunty/Dialects/MySqlDialect.cs` | Complete | ON DUPLICATE KEY, LAST_INSERT_ID |
| SQLite dialect | `src/Jaunty/Dialects/SQLiteDialect.cs` | Complete | INSERT OR REPLACE, last_insert_rowid |
| Identifier escaping | `src/Jaunty/Dialects/*.cs` | Complete | Keyword-based escaping |
| Case-sensitive LIKE | `src/Jaunty/Dialects/*.cs` | Complete | COLLATE-based for SQL Server/Postgres |
| Paging SQL generation | `src/Jaunty/Dialects/*.cs` | Complete | OFFSET/FETCH or LIMIT |
| Upsert SQL generation | `src/Jaunty/Dialects/*.cs` | Complete | Dialect-specific syntax |

### 1.6 Attributes & Mapping

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| `[Table]` attribute | `src/Jaunty/Attributes/TableAttribute.cs` | Complete | Table name and schema |
| `[Column]` attribute | `src/Jaunty/Attributes/ColumnAttribute.cs` | Complete | Column name override |
| `[Key]` attribute | `src/Jaunty/Attributes/KeyAttribute.cs` | Complete | Primary key designation |
| `[DatabaseGenerated]` | `src/Jaunty/Attributes/DatabaseGeneratedAttribute.cs` | Complete | Identity, Computed options |
| `[Ignore]` attribute | `src/Jaunty/Attributes/IgnoreAttribute.cs` | Complete | Exclude from mapping |
| Source generator | `src/Jaunty.SourceGenerator/` | Complete | Compile-time metadata |

### 1.7 Configuration

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| Global config | `src/Jaunty/Configuration/JauntyConfig.cs` | Complete | Static configuration class |
| Naming resolvers | `src/Jaunty/Configuration/JauntyConfig.cs` | Complete | Table/column name resolvers |
| Logger hook | `src/Jaunty/Configuration/JauntyConfig.cs` | Complete | `Action<string, object>` |
| Capacity settings | `src/Jaunty/Configuration/JauntyConfig.cs` | Complete | Buffer sizing options |
| Bulk copy config | `src/Jaunty/Configuration/BulkCopyConfiguration.cs` | Complete | Batch size, timeout |

### 1.8 Command Options

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| Transaction support | `src/Jaunty/Core/CommandOptions.cs` | Complete | `CommandOptions.WithTransaction()` |
| Timeout support | `src/Jaunty/Core/CommandOptions.cs` | Complete | `CommandOptions.WithTimeout()` |
| Custom mapper | `src/Jaunty/Core/CommandOptions.cs` | Complete | `CommandOptions.WithMapper()` |
| Command type | `src/Jaunty/Core/CommandOptions.cs` | Complete | Text, StoredProcedure |
| Expected row count | `src/Jaunty/Core/CommandOptions.cs` | Complete | List pre-sizing hint |

### 1.9 Multi-Entity Mapping

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| `QueryMultiEntity<T1,T2>()` | `src/Jaunty/Read/QueryMultiEntity.cs` | Complete | JOIN result mapping to tuple |
| `QueryMultiEntityAsync<T1,T2>()` | `src/Jaunty/Read/QueryMultiEntityAsync.cs` | Complete | Async tuple mapping |
| `MultiEntityMapper<T1,T2>` | `src/Jaunty/Internals/Read/MultiEntityMapper.cs` | Complete | Column range mapping |

### 1.10 CSV Import

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| CSV reading | `src/Jaunty/Import/CsvImport.cs` | Complete | RFC 4180 compliant |
| CSV options | `src/Jaunty/Import/CsvImportOptions.cs` | Complete | Delimiter, headers, encoding |
| NULL value mapping | `src/Jaunty/Import/CsvImportOptions.cs` | Complete | Custom NULL string |

### 1.11 Logging and Diagnostics

| Feature | Location | Status | Notes |
|---------|----------|--------|-------|
| `LoggingInterceptor` | `src/Jaunty/Interceptors/LoggingInterceptor.cs` | Complete | ILogger integration with slow query detection |
| `AuditInterceptor` | `src/Jaunty/Diagnostics/AuditInterceptor.cs` | Complete | In-memory audit trail |
| `ICommandInterceptor` | `src/Jaunty/Interceptors/ICommandInterceptor.cs` | Complete | Interceptor interface |
| `InterceptorPipeline` | `src/Jaunty/Interceptors/InterceptorPipeline.cs` | Complete | Multi-interceptor orchestration |
| `DiagnosticSource` events | `src/Jaunty/Diagnostics/JauntyDiagnosticListener.cs` | Complete | OpenTelemetry/App Insights integration |
| `LoggingConfiguration` | `src/Jaunty/Configuration/LoggingConfiguration.cs` | Complete | Log levels, sensitivity, thresholds |
| DI registration | `src/Jaunty/JauntyLoggingExtensions.cs` | Complete | `IServiceCollection` extensions |

### 1.12 Extensions (Separate Packages)

| Extension | Package | Status | Notes |
|-----------|---------|--------|-------|
| Fluent Query API | `Jaunty.Fluent` | Complete | WHERE, JOIN, ORDER BY, CTEs, window functions |
| Reflection Mapping | `Jaunty.Extensions.Reflection` | Complete | Runtime mapping fallback |
| Native Bulk Copy | `Jaunty.Extensions.Reflection` | Complete | SqlBulkCopy, NpgsqlBinaryImporter |
| Scaffolding | `Jaunty.Scaffolding` | Complete | Reverse engineer entities |
| Flat Files | `Jaunty.FlatFiles` | Complete | CSV/TSV file operations |
| DuckDB Integration | `Jaunty.FlatFiles.DuckDB` | Complete | DuckDB dialect with bulk copy |

---

## Part 2: Missing Features - Discussion Required

### Priority P0: Critical for Production Adoption

#### 2.1.1 Dependency Injection Integration

**Status:** Partially Complete - `AddJauntyLogging()` and `ApplyJauntyInterceptors()` extensions available

**Problem:** Limited DI integration. Manual interceptor registration required.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: Simple Registration** | `services.AddJaunty(connectionString)` | Simple, minimal | Limited flexibility |
| **B: Factory-Based** | `IDbConnectionFactory` registration | Testable, flexible | More abstraction |
| **C: Named Connections** | Multiple named connections | Multi-database support | More complex config |

**Decision Required:**
- [ ] Support for multiple named connections?
- [ ] Connection factory abstraction or direct registration?
- [ ] Options pattern (`IOptions<JauntyOptions>`) or fluent config?
- [ ] Should connection pooling be managed?

**Proposed API:**
```csharp
// Simple
services.AddJaunty<SqlConnection>("connection-string");

// Named connections
services.AddJaunty(options => {
    options.AddConnection<SqlConnection>("default", "conn1");
    options.AddConnection<SqlConnection>("analytics", "conn2");
});

// Factory-based
services.AddScoped<IDbConnectionFactory, MyConnectionFactory>();
```

---

#### 2.1.2 Query Result Caching

**Problem:** No second-level cache. Every query hits the database.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: MemoryCache** | Built-in `IMemoryCache` integration | Simple, no dependencies | Single-node only |
| **B: Distributed Cache** | `IDistributedCache` (Redis) | Multi-node support | External dependency |
| **C: Provider Abstraction** | `ICacheProvider` interface | Pluggable, flexible | More abstraction |
| **D: No Built-in Cache** | Extension point only | Keeps core minimal | Users must implement |

**Decision Required:**
- [ ] Should caching be in core or extension?
- [ ] Cache invalidation strategy? (Time-based, write-through, manual)
- [ ] Per-query cache configuration?
- [ ] Cache key generation strategy?

**Proposed API:**
```csharp
// Per-query caching
var products = connection.Query<Product>(
    sql,
    cache: CacheOptions.For("products:all", TimeSpan.FromMinutes(5)));

// Global configuration
services.AddJaunty(config => {
    config.UseCacheProvider(new RedisCacheProvider(...));
    config.DefaultCacheDuration = TimeSpan.FromMinutes(1);
});
```

---

### Priority P1: High-Value Additions

#### 2.2.1 Resilience & Retry Policies

**Problem:** No built-in retry for transient failures.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: Polly Integration** | Built-in Polly support | Mature, feature-rich | External dependency |
| **B: Simple Retry** | Built-in retry with configurable attempts | No dependencies | Limited features |
| **C: Abstraction Layer** | `IResilienceProvider` interface | Pluggable | More work |

**Decision Required:**
- [ ] Use Polly or build simple retry?
- [ ] Which exceptions trigger retry? (Timeout, connection errors, deadlocks)
- [ ] Exponential backoff configuration?
- [ ] Circuit breaker pattern needed?

**Proposed API:**
```csharp
// Simple retry
services.AddJaunty(config => {
    config.UseRetryPolicy(retry => {
        retry.MaxAttempts(3);
        retry.OnException<SqlException>();
        retry.ExponentialBackoff(TimeSpan.FromSeconds(1));
    });
});

// Polly integration
services.AddJaunty(config => {
    config.UsePollyPolicy(myResiliencePipeline);
});
```

---

#### 2.2.2 Optimistic Concurrency

**Problem:** No automatic concurrency token handling.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: RowVersion Attribute** | `[ConcurrencyCheck]` or `[RowVersion]` | Explicit, clear | Requires schema support |
| **B: Timestamp Columns** | Automatic `UpdatedAt` handling | Common pattern | Application-level only |
| **C: Comparison-Based** | WHERE clause with original values | Works everywhere | Manual setup |

**Decision Required:**
- [ ] Which concurrency strategy to support?
- [ ] Should `Update()` throw on concurrency violation?
- [ ] Return affected rows or throw exception?
- [ ] Support composite concurrency tokens?

**Proposed API:**
```csharp
public class Product
{
    [Key]
    public int Id { get; set; }

    [RowVersion]  // SQL Server rowversion/timestamp
    public byte[] RowVersion { get; set; }

    // OR

    [ConcurrencyCheck]  // Compare original value
    public decimal Price { get; set; }
}

// Usage
try
{
    connection.Update(product);  // Throws DbConcurrencyException
}
catch (DbConcurrencyException ex)
{
    // Handle conflict
}
```

---

#### 2.2.3 Soft Delete Support

**Problem:** No automatic soft delete filtering.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: Attribute-Based** | `[SoftDelete]` attribute | Explicit, per-entity | Requires convention |
| **B: Global Filter** | Configurable global filter | Flexible | More complex |
| **C: Extension Method** | `.WithSoftDelete<T>()` | Opt-in, explicit | Manual per-query |

**Decision Required:**
- [ ] Automatic filtering or opt-in?
- [ ] Support for `Delete<T>()` conversion to soft delete?
- [ ] Include deleted entities option?
- [ ] Multiple deletion timestamp strategies?

**Proposed API:**
```csharp
// Attribute approach
public class Product
{
    [SoftDelete(ColumnName = "DeletedAt")]
    public DateTime? DeletedAt { get; set; }
}

// Query automatically filters: WHERE DeletedAt IS NULL
connection.Query<Product>("SELECT * FROM Products");

// Include deleted
connection.Query<Product>("SELECT * FROM Products", includeSoftDeleted: true);

// Actual delete
connection.HardDelete(product);  // Actual DELETE
connection.Delete(product);      // Sets DeletedAt
```

---

### Priority P2: Developer Experience

#### 2.3.1 LINQ Provider

**Problem:** No LINQ support. Users must write raw SQL.

**Decision Required:**
- [ ] Is LINQ support aligned with Jaunty's philosophy?
- [ ] Full LINQ or limited subset?
- [ ] Translation to raw SQL or expression-based builder?

**Note:** This is a significant architectural decision. May warrant a separate ADR.

---

#### 2.3.2 Change Tracking / Unit of Work

**Problem:** No identity map or change tracking.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: Lightweight Tracker** | Simple identity map | Low overhead | Limited features |
| **B: Unit of Work** | `IUnitOfWork` abstraction | Familiar pattern | More abstraction |
| **C: No Built-in** | Extension point only | Keeps core minimal | Users implement |

**Decision Required:**
- [ ] Should Jaunty support change tracking at all?
- [ ] Scope of tracking (per-connection, per-unit-of-work)?
- [ ] Auto-detect changes or explicit marking?

---

#### 2.3.3 Migration Framework

**Problem:** No code-first migrations.

**Proposed Solutions:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A: SQL-Based Migrations** | Execute SQL scripts | Simple, database-agnostic | Manual script writing |
| **B: Code-First DSL** | C#-based migration definitions | Type-safe, refactorable | Complex implementation |
| **C: Integration Only** | Execute external migration tools | Leverage existing tools | External dependency |

**Decision Required:**
- [ ] Build migrations or integrate with existing tools?
- [ ] Support rollback/down migrations?
- [ ] Migration history tracking?

---

### Priority P3: Nice-to-Have Features

#### 2.4.1 Advanced Parameter Features

| Feature | Description | Priority |
|---------|-------------|----------|
| Table-Valued Parameters | SQL Server TVP support | Medium |
| JSON Parameter Binding | Auto-serialize to JSON columns | Low |
| Parameter Size/Precision | Specify DbType, size, precision | Medium |
| Bulk Parameter Binding | Optimized for large parameter lists | Low |

---

#### 2.4.2 Audit & Compliance

| Feature | Description | Priority |
|---------|-------------|----------|
| Automatic Timestamps | CreatedAt/ModifiedAt population | Medium |
| Entity History | Change history tracking | Low |
| Audit Logging | Who changed what and when | Medium |

---

#### 2.4.3 Special Type Support

| Feature | Description | Priority |
|---------|-------------|----------|
| JSON/JSONB Columns | Native JSON type mapping | Medium |
| Geometry/Geography | Spatial type support | Low |
| Arrays (Postgres) | Native array type support | Low |

---

## Part 3: Decision Log

Use this section to record decisions made during discussion.

| Feature | Decision | Date | Notes |
|---------|----------|------|-------|
| ILogger Integration | | | |
| DI Integration | | | |
| Query Caching | | | |
| Resilience/Retry | | | |
| Optimistic Concurrency | | | |
| Soft Delete | | | |
| LINQ Provider | | | |
| Change Tracking | | | |
| Migrations | | | |

---

## Part 4: Implementation Roadmap (Post-Decision)

### Phase 1: Foundation (P0 Items)
- [ ] ILogger Integration
- [ ] DI Integration
- [ ] Command Interception

### Phase 2: Reliability (P1 Items)
- [ ] Resilience/Retry Policies
- [ ] Query Result Caching
- [ ] Optimistic Concurrency

### Phase 3: Developer Experience (P2 Items)
- [ ] Soft Delete Support
- [ ] Migration Framework (decision pending)

### Phase 4: Advanced Features (P3 Items)
- [ ] Table-Valued Parameters
- [ ] JSON Type Mapping
- [ ] Audit Features

---

## Appendix A: Related Documentation

- [ARCHITECTURE-DECISIONS.md](../02-architecture/ARCHITECTURE-DECISIONS.md)
- [Design Philosophy](../02-architecture/design-philosophy.md)
- [Known Limitations](../03-development/limitations.md)
