# Jaunty Codebase Inconsistencies

**Generated:** 2026-02-19  
**Scope:** Full codebase audit of src/Jaunty

---

## Executive Summary

This document catalogs all inconsistencies found in the Jaunty micro-ORM codebase, organized by category and severity. A separate prioritized tasklist (`INCONSISTENCIES-TASKLIST.md`) provides actionable items for remediation.

**Total Issues Found:** 47  
**Critical:** 8  
**High:** 15  
**Medium:** 16  
**Low:** 8

---

## 1. API Design Inconsistencies

### 1.1 Return Type Inconsistencies

#### Issue: Insert returns `long`, Update/Delete return `int`
**Severity:** Medium  
**Files:** `Write/Insert.cs`, `Write/Update.cs`, `Write/Delete.cs`

**Description:**
- `Insert<T>()` returns `long` (identity value or rows affected)
- `Update<T>()` returns `int` (rows affected)
- `Delete<T>()` returns `int` (rows affected)

**Impact:** Inconsistent API surface; callers must handle different return types for similar operations.

**Recommendation:** Standardize on `int` for all operations, or use `long` consistently. Document the rationale for the current design.

---

#### Issue: Bulk operations return `int` but individual operations return `long` for Insert
**Severity:** Low  
**Files:** `Write/BulkInsert.cs`, `Write/BulkUpdate.cs`, `Write/BulkDelete.cs`

**Description:**
- `BulkInsert<T>()` returns `int` (total rows inserted)
- `Insert<T>()` returns `long` (identity value)

**Impact:** Confusing for users expecting similar behavior between single and bulk operations.

---

### 1.2 Parameter Order Inconsistencies

#### Issue: CommandOptions parameter position varies
**Severity:** Low  
**Files:** Multiple files across Read/, Write/, Streaming/

**Description:**
Most methods follow: `connection, sql, parameters, options, cancellationToken`
But some variations exist in internal core methods.

**Example:**
```csharp
// Public API (consistent)
Query<T>(connection, sql, parameters, options)

// Internal core (also consistent but different pattern)
QueryCore<T>(connection, sql, parameters, options, mappingMode)
```

**Impact:** Minor confusion for contributors reading internal code.

---

### 1.3 Method Naming Inconsistencies

#### Issue: "WithOutput" vs "WithParameters" terminology
**Severity:** Low  
**Files:** `StoredProcedure/ExecuteStoredProcedureWithOutput.cs`

**Description:**
Methods are named `ExecuteStoredProcedureWithOutput` but the class is `SpParameters`. The term "output" specifically refers to output parameters, but the naming could be clearer.

**Recommendation:** Consider renaming to `ExecuteStoredProcedureWithParameters` for clarity.

---

#### Issue: Mixed use of "Async" suffix placement
**Severity:** Low  
**Files:** Throughout codebase

**Description:**
Most async methods follow: `QueryAsync<T>()`
But some follow: `BulkInsertAsync<T>()` vs `InsertAsync<T>()`

The pattern is actually consistent, but the file organization sometimes groups sync/async together and sometimes separates them.

---

### 1.4 Generic Constraint Inconsistencies

#### Issue: Not all methods specify `where T : class, new()`
**Severity:** Medium  
**Files:** Some StoredProcedure methods

**Description:**
Most query methods specify: `where T : class, new()`
But some stored procedure methods only specify: `where T : new()`

**Example:**
```csharp
// Consistent
public static List<T> Query<T>(...) where T : class, new()

// Inconsistent (missing 'class')
public static List<T> ExecuteStoredProcedure<T>(...) where T : new()
```

**Impact:** Potential runtime issues if value types are passed.

---

## 2. Coding Style Inconsistencies

### 2.1 Null Check Patterns

#### Issue: Mixed null check styles across files
**Severity:** Medium  
**Files:** Throughout codebase

**Description:**
Three different patterns are used:

**Pattern 1 - NET8_0_OR_GREATER conditional:**
```csharp
#if NET8_0_OR_GREATER
ArgumentNullException.ThrowIfNull(connection);
ArgumentNullException.ThrowIfNull(entity);
#else
if (connection is null) throw new ArgumentNullException(nameof(connection));
if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
```

**Pattern 2 - Simple if check:**
```csharp
if (connection is null) throw new ArgumentNullException(nameof(connection));
```

**Pattern 3 - No explicit check (relies on downstream):**
```csharp
// No null check, relies on called methods to throw
```

**Impact:** Inconsistent error messages and stack traces; maintenance burden.

**Recommendation:** Standardize on Pattern 1 for all public APIs.

---

### 2.2 Using Statement Styles

#### Issue: Mixed `using` declaration styles
**Severity:** Low  
**Files:** Throughout codebase

**Description:**
Two patterns exist:

**Pattern 1 - Traditional:**
```csharp
using IDbCommand command = connection.CreateCommand();
```

**Pattern 2 - With explicit dispose in finally:**
```csharp
IDbCommand? command = null;
try
{
    command = connection.CreateCommand();
    // ...
}
finally
{
    command?.Dispose();
}
```

**Impact:** Code verbosity differs; Pattern 1 is preferred for modern C#.

---

### 2.3 String Interpolation vs Concatenation

#### Issue: Mixed string building approaches
**Severity:** Low  
**Files:** `Internals/Dialects/*.cs`

**Description:**
Some dialects use string interpolation:
```csharp
return $"{columnName} COLLATE Latin1_General_CS_AS LIKE {parameterName}";
```

Others use StringBuilder for complex SQL:
```csharp
var sb = new StringBuilder(512);
sb.Append("MERGE INTO ");
sb.Append(tableName);
```

**Impact:** Minor; both are acceptable. StringBuilder is more efficient for complex strings.

---

### 2.4 Comment Styles

#### Issue: Inconsistent XML comment completeness
**Severity:** Medium  
**Files:** Internal methods, helper classes

**Description:**
Public APIs have comprehensive XML documentation.
Internal methods have minimal or no documentation.

**Examples:**
```csharp
// Well documented
/// <summary>
/// Executes a SQL query and returns all results...
/// </summary>

// Minimally documented
// SQL Server doesn't have a simple session-level FK toggle.
```

**Recommendation:** Add XML comments to all internal methods that are part of the core logic.

---

### 2.5 Exception Message Formatting

#### Issue: Inconsistent exception message formats
**Severity:** Low  
**Files:** Throughout codebase

**Description:**
Three patterns exist:

**Pattern 1 - With type name:**
```csharp
throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");
```

**Pattern 2 - Without type name:**
```csharp
throw new InvalidOperationException("Sequence contains no elements");
```

**Pattern 3 - With provider name:**
```csharp
throw new NotSupportedException(
    $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling.");
```

**Impact:** Inconsistent debugging experience.

**Recommendation:** Standardize on Pattern 1 for entity-related errors, Pattern 3 for provider-specific errors.

---

## 3. Behavior Inconsistencies

### 3.1 Connection State Handling

#### Issue: Inconsistent connection open/close patterns
**Severity:** High  
**Files:** All core operation files

**Description:**
Most methods follow:
```csharp
bool wasClosed = connection.State == ConnectionState.Closed;
try
{
    if (wasClosed) connection.Open();
    // ... operation
}
finally
{
    if (wasClosed && connection.State != ConnectionState.Closed)
        connection.Close();
}
```

But some async methods have variations:
```csharp
// In finally block
if (wasClosed && connection.State != ConnectionState.Closed)
{
#if NET8_0_OR_GREATER
    if (connection is DbConnection dbConn)
        await dbConn.CloseAsync().ConfigureAwait(false);
    else
        connection.Close();
#else
    connection.Close();
#endif
}
```

**Impact:** Potential connection leaks in edge cases; inconsistent behavior between sync/async.

---

### 3.2 Transaction Type Casting

#### Issue: Unsafe transaction type casting in async methods
**Severity:** High  
**Files:** `Internals/Write/DeleteCore.cs`, `Internals/ExecuteQueryMultipleAsync.cs`

**Description:**
```csharp
// Unsafe cast - will throw if not DbTransaction
if (options.Transaction is not null)
    command.Transaction = (DbTransaction)options.Transaction;
```

Should be:
```csharp
if (options.Transaction is DbTransaction dbTransaction)
    command.Transaction = dbTransaction;
```

**Impact:** Runtime InvalidCastException if IDbTransaction implementation is not DbTransaction.

---

### 3.3 Parameter Binding Inconsistencies

#### Issue: Different parameter binding approaches
**Severity:** Medium  
**Files:** `Write/InsertCore.cs`, `Write/UpdateCore.cs`, `Write/DeleteCore.cs`

**Description:**
- Insert/Update use compiled delegates: `WriteParameterCache<T>.InsertBinder(command, entity)`
- Delete uses inline binding: `BindDeleteParameters(command, entity, cached.Metadata)`

**Impact:** Inconsistent performance characteristics; maintenance burden.

**Recommendation:** Standardize on compiled delegates for all operations.

---

### 3.4 Identity Column Detection

#### Issue: Inconsistent identity column handling
**Severity:** Medium  
**Files:** `Internals/Write/InsertCore.cs`

**Description:**
```csharp
// For identity columns, append the last insert ID SQL
if (cached.HasIdentityKey)
{
    command.CommandText = cached.InsertSql + "; " + cached.LastInsertIdSql;
}
else
{
    command.CommandText = cached.InsertSql;
}
```

PostgreSQL uses `RETURNING` clause which is appended differently.

**Impact:** Different SQL generation patterns per database; potential SQL injection if not careful.

---

### 3.5 Error Handling in Bulk Operations

#### Issue: Inconsistent error handling for FK constraint toggle
**Severity:** High  
**Files:** `Write/BulkInsert.cs`, `Write/BulkUpdate.cs`, `Write/BulkDelete.cs`

**Description:**
Some methods re-enable FK constraints in catch blocks:
```csharp
catch
{
    if (ignoreConstraints)
    {
        try
        {
            // Re-enable FK checks
        }
        catch { /* Best effort */ }
    }
    throw;
}
```

But the pattern isn't consistent across all bulk operations.

**Impact:** Database may be left in inconsistent state if FK constraints aren't re-enabled.

---

### 3.6 Cancellation Token Handling

#### Issue: Inconsistent CancellationToken propagation
**Severity:** Medium  
**Files:** Streaming methods, async methods

**Description:**
Some methods properly propagate cancellation tokens:
```csharp
while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
```

Others don't check cancellation in loops:
```csharp
foreach (var entity in entityList)
{
    // No cancellationToken.ThrowIfCancellationRequested()
    totalInserted += await command.ExecuteNonQueryAsync(cancellationToken);
}
```

**Impact:** Delayed cancellation response in bulk operations.

---

## 4. Naming Inconsistencies

### 4.1 Class Naming

#### Issue: Mixed naming conventions for internal classes
**Severity:** Low  
**Files:** `Internals/` directory

**Description:**
- `CachedCrudSql` (PascalCase with abbreviation)
- `CrudSqlCache` (different order)
- `MetadataCache` vs `MetadataBuilder`
- `WriteParameterCache` vs `ParameterCache`

**Impact:** Minor confusion for contributors.

---

### 4.2 Method Naming

#### Issue: Core method naming inconsistencies
**Severity:** Low  
**Files:** Internal core methods

**Description:**
- `QueryCore<T>()` vs `QueryFirstCore<T>()` vs `QueryFirstOrDefaultCore<T>()`
- `DeleteByEntityCore()` vs `DeleteByIdCore()` vs `DeleteByIdCoreAsync<T, TId>()`

The pattern is mostly consistent but some variations exist.

---

### 4.3 Variable Naming

#### Issue: Inconsistent variable names for same concepts
**Severity:** Low  
**Files:** Throughout codebase

**Description:**
- `wasClosed` vs `connectionWasClosed`
- `ownTransaction` vs `localTransaction`
- `entityList` vs `entities` vs `items`
- `cached` vs `sqlCache` vs `cachedSql`

**Recommendation:** Standardize on short, clear names: `wasClosed`, `ownTransaction`, `entities`, `cached`.

---

### 4.4 Namespace Organization

#### Issue: Some internals could be better organized
**Severity:** Low  
**Files:** `Internals/` directory structure

**Description:**
- `Internals/Write/` contains core logic
- `Internals/Entity/` contains metadata classes
- `Internals/Parameters/` contains parameter handling
- But `Internals/QueryCore.cs` is at root level

**Recommendation:** Move `QueryCore.cs` to `Internals/Read/` for consistency.

---

## 5. Documentation Inconsistencies

### 5.1 Exception Documentation

#### Issue: Not all exceptions are documented
**Severity:** Medium  
**Files:** Some internal methods, older files

**Description:**
Newly documented files have comprehensive `<exception>` tags.
Older files or internal methods may be missing exception documentation.

**Example:**
```csharp
// Well documented
/// <exception cref="InvalidOperationException">
/// Thrown when the entity has no primary key property.
/// </exception>

// Missing documentation
private static void SomeMethod() { } // No XML comments
```

---

### 5.2 SeeAlso References

#### Issue: Inconsistent `<seealso>` usage
**Severity:** Low  
**Files:** Throughout codebase

**Description:**
Some methods have comprehensive `<seealso>` references to related methods.
Others have minimal or no cross-references.

**Recommendation:** Add `<seealso>` to all public methods referencing:
- Sync/async counterparts
- Related operations (Insert/Update/Delete)
- Overloads with different parameters

---

### 5.3 Example Code Consistency

#### Issue: Example code uses different entity names
**Severity:** Low  
**Files:** Documentation examples throughout

**Description:**
Most examples use `Product` class.
Some use `Order`, `Category`, or other entities.

**Impact:** Minor; makes it harder to follow if reading multiple docs.

**Recommendation:** Standardize on `Product` for simple examples, `Order`/`Customer` for relationship examples.

---

## 6. Architecture Inconsistencies

### 6.1 Dialect Implementation

#### Issue: Not all dialects implement all methods
**Severity:** Medium  
**Files:** `Internals/Dialects/ISqlDialect.cs` and implementations

**Description:**
SQL Server returns `null` for FK toggle methods:
```csharp
public string? GetDisableForeignKeyChecksSql() => null;
public bool SupportsForeignKeyToggle => false;
```

But the interface doesn't enforce checking for support before calling.

**Impact:** Potential NullReferenceException if not checked.

---

### 6.2 Cache Implementation

#### Issue: Multiple cache implementations with different patterns
**Severity:** Low  
**Files:** `MappedCache.cs`, `ParameterCache.cs`, `CrudSqlCache.cs`, `WriteParameterCache.cs`

**Description:**
- `MappedCache<T>` uses static lambda
- `ParameterCache<T>` uses concurrent dictionary
- `CrudSqlCache` uses static method with dictionary
- `WriteParameterCache<T>` uses compiled delegates

**Impact:** Different performance characteristics; maintenance burden.

**Recommendation:** Consider unifying cache implementations or documenting the rationale for each.

---

### 6.3 Reader Implementation

#### Issue: EntityReader has conditional compilation for async
**Severity:** Low  
**Files:** `Readers/EntityReader.cs`

**Description:**
```csharp
#if ASYNC_ENUMERABLE_SUPPORT
public static async IAsyncEnumerable<T> ReadEntitiesAsync<T>(...)
#else
public static async Task<List<T>> ReadEntitiesAsync<T>(...)
#endif
```

This creates two different return types based on compilation flag.

**Impact:** API surface differs based on target framework.

---

## 7. Performance Inconsistencies

### 7.1 String Building

#### Issue: Inefficient string concatenation in loops
**Severity:** Low  
**Files:** Some dialect SQL generation

**Description:**
Some methods use string interpolation in loops instead of StringBuilder.

**Example:**
```csharp
// Less efficient
var result = "";
foreach (var item in items)
{
    result += item + ", ";
}

// More efficient
var sb = new StringBuilder();
foreach (var item in items)
{
    sb.Append(item).Append(", ");
}
```

---

### 7.2 LINQ Usage

#### Issue: Mixed LINQ vs manual iteration
**Severity:** Low  
**Files:** Throughout codebase

**Description:**
Some places use LINQ:
```csharp
var entityList = entities as IList<T> ?? entities.ToList();
```

Others use manual loops for similar operations.

**Impact:** Minor performance differences; consistency is more important.

---

## 8. Testing Gaps

### 8.1 Edge Case Coverage

#### Issue: Not all edge cases are tested
**Severity:** High  
**Files:** Test files (not audited in detail)

**Description:**
Based on code review, these scenarios may not be fully tested:
- Composite primary keys
- Null handling in all dialects
- FK constraint toggle failures
- Transaction rollback scenarios
- Cancellation during bulk operations

**Recommendation:** Review test coverage for these scenarios.

---

## Appendix A: Files Reviewed

### Read Operations
- Query.cs
- QueryFirst.cs
- QuerySingle.cs
- QueryFirstOrDefault.cs
- QuerySingleOrDefault.cs
- QueryPartial.cs
- QueryPartialFirst.cs
- QueryPartialSingle.cs
- QueryPartialFirstOrDefault.cs
- QueryPartialSingleOrDefault.cs
- QueryAsync.cs
- QueryFirstAsync.cs
- QuerySingleAsync.cs
- QueryFirstOrDefaultAsync.cs
- QuerySingleOrDefaultAsync.cs
- QueryPartialAsync.cs
- QueryPartialFirstAsync.cs
- QueryPartialSingleAsync.cs
- QueryPartialFirstOrDefaultAsync.cs
- QueryPartialSingleOrDefaultAsync.cs
- QueryMultiEntity.cs
- QueryMultiEntityAsync.cs
- QueryScalar.cs
- QueryScalarAsync.cs
- ExecuteScalar.cs
- ExecuteScalarAsync.cs

### Write Operations
- Insert.cs
- InsertAsync.cs
- Update.cs
- UpdateAsync.cs
- Delete.cs
- DeleteAsync.cs
- BulkInsert.cs
- BulkInsertAsync.cs
- BulkUpdate.cs
- BulkUpdateAsync.cs
- BulkDelete.cs
- BulkDeleteAsync.cs
- Upsert.cs
- UpsertAsync.cs

### Multiple Result Sets
- QueryMultiple.cs
- QueryMultipleAsync.cs

### Streaming
- QueryStream.cs
- QueryStreamAsync.cs
- QueryPartialStream.cs
- QueryPartialStreamAsync.cs
- QueryPartialUnbuffered.cs
- QueryPartialUnbufferedAsync.cs

### Stored Procedures
- StoredProcedure.cs
- StoredProcedureAsync.cs
- ExecuteStoredProcedureWithOutput.cs
- ExecuteStoredProcedureWithOutputAsync.cs
- SpParameters.cs

### Core/Configuration
- CommandOptions.cs
- GridReader.cs
- JauntyConfig.cs

### Attributes
- TableAttribute.cs
- ColumnAttribute.cs
- KeyAttribute.cs
- IgnoreAttribute.cs
- DatabaseGeneratedAttribute.cs
- DatabaseGeneratedOptions.cs
- AttributeHelper.cs

### Interfaces
- IEntity.cs
- IMapped.cs

### Internals
- All dialect files
- All core operation files
- All cache files
- All reader files

---

*End of Document*
