# Fluent API

## Overview

The Jaunty Fluent API provides a type-safe, LINQ-like query builder that enables constructing SQL queries using method chaining. This API offers compile-time validation and IntelliSense support for building complex queries.

## Getting Started

### From&lt;T&gt;(IDbConnection connection, string? alias = null)

Entry point for the fluent query API. Creates a query builder for the specified entity type.

**Signature:**
```csharp
public static IFromClause<T> From<T>(this IDbConnection connection, string? alias = null) where T : new()
```

**Type Parameters:**
- `T`: The entity type to query (must have a parameterless constructor)

**Parameters:**
- `connection`: The database connection
- `alias`: Optional alias for the table (useful for joins with string-based conditions)

**Returns:**
- `IFromClause<T>`: A query builder for chaining WHERE, ORDER BY, JOIN, and SELECT operations

**Example:**
```csharp
// Basic query
var products = connection.From<Product>()
    .Select();

// With alias for joins
var products = connection.From<Product>("p")
    .InnerJoin<Category>("c")
    .On("p.category_id", "c.id")
    .Select();
```

## Query Building Methods

### WHERE Clauses

#### Where(string column, object? value)

Adds a WHERE condition comparing a column to a value using equality.

**Signature:**
```csharp
IWhereClause<T> Where(string column, object? value)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .Where("category_id", 1)
    .Select();
```

#### Where(Expression&lt;Func&lt;T, bool&gt;&gt; predicate)

Adds a WHERE condition using a strongly-typed expression predicate.

**Signature:**
```csharp
IWhereClause<T> Where(Expression<Func<T, bool>> predicate)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .Where(p => p.CategoryId == 1)
    .Select();
```

#### WhereRaw(string rawSql)

Adds a raw SQL WHERE condition without parameterization (use with caution).

**Signature:**
```csharp
IWhereClause<T> WhereRaw(string rawSql)
```

#### WhereRaw(string rawSql, object parameters)

Adds a raw SQL WHERE condition with parameters.

**Signature:**
```csharp
IWhereClause<T> WhereRaw(string rawSql, object parameters)
```

### AND/OR Conditions

#### And(string column, object? value)

Adds an AND condition to the existing WHERE clause.

**Signature:**
```csharp
IWhereClause<T> And(string column, object? value)
```

#### And(Expression&lt;Func&lt;T, bool&gt;&gt; predicate)

Adds an AND condition using a strongly-typed expression.

**Signature:**
```csharp
IWhereClause<T> And(Expression<Func<T, bool>> predicate)
```

#### Or(string column, object? value)

Adds an OR condition to the existing WHERE clause.

**Signature:**
```csharp
IWhereClause<T> Or(string column, object? value)
```

#### Or(Expression&lt;Func&lt;T, bool&gt;&gt; predicate)

Adds an OR condition using a strongly-typed expression.

**Signature:**
```csharp
IWhereClause<T> Or(Expression<Func<T, bool>> predicate)
```

### Collection-Based Filtering

#### WhereIn&lt;TValue&gt;(Expression&lt;Func&lt;T, TValue&gt;&gt; selector, IEnumerable&lt;TValue&gt; values)

Filters results where the selected property value is in the specified collection.

**Signature:**
```csharp
IWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .WhereIn(p => p.CategoryId, new[] { 1, 2, 3 })
    .Select();
```

#### WhereNotIn&lt;TValue&gt;(Expression&lt;Func&lt;T, TValue&gt;&gt; selector, IEnumerable&lt;TValue&gt; values)

Filters results where the selected property value is NOT in the specified collection.

**Signature:**
```csharp
IWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
```

#### AndIn&lt;TValue&gt;(Expression&lt;Func&lt;T, TValue&gt;&gt; selector, IEnumerable&lt;TValue&gt; values)

Adds an AND condition where the selected property value is in the specified collection.

**Signature:**
```csharp
IWhereClause<T> AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
```

#### OrIn&lt;TValue&gt;(Expression&lt;Func&lt;T, TValue&gt;&gt; selector, IEnumerable&lt;TValue&gt; values)

Adds an OR condition where the selected property value is in the specified collection.

**Signature:**
```csharp
IWhereClause<T> OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
```

### Range Filtering

#### WhereBetween&lt;TValue&gt;(Expression&lt;Func&lt;T, TValue&gt;&gt; selector, TValue from, TValue to)

Filters results where the selected property value is between the specified range (inclusive).

**Signature:**
```csharp
IWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .WhereBetween(p => p.Price, 10m, 100m)
    .Select();
```

#### WhereNotBetween&lt;TValue&gt;(Expression&lt;Func&lt;T, TValue&gt;&gt; selector, TValue from, TValue to)

Filters results where the selected property value is NOT between the specified range.

**Signature:**
```csharp
IWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
```

### ORDER BY Clauses

#### OrderBy(Expression&lt;Func&lt;T, object?&gt;&gt; keySelector)

Orders the results by the specified property in ascending order.

**Signature:**
```csharp
IOrderByClause<T> OrderBy(Expression<Func<T, object?>> keySelector)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .OrderBy(p => p.ProductName)
    .Select();
```

#### OrderByDescending(Expression&lt;Func&lt;T, object?&gt;&gt; keySelector)

Orders the results by the specified property in descending order.

**Signature:**
```csharp
IOrderByClause<T> OrderByDescending(Expression<Func<T, object?>> keySelector)
```

#### OrderBy(string column)

Orders the results by the specified column name in ascending order.

**Signature:**
```csharp
IOrderByClause<T> OrderBy(string column)
```

#### OrderByDescending(string column)

Orders the results by the specified column name in descending order.

**Signature:**
```csharp
IOrderByClause<T> OrderByDescending(string column)
```

### JOIN Operations

#### InnerJoin&lt;TJoin&gt;(string? alias = null)

Adds an INNER JOIN to another table.

**Signature:**
```csharp
IJoinClause<T, TJoin> InnerJoin<TJoin>(string? alias = null) where TJoin : new()
```

**Example:**
```csharp
var products = connection.From<Product>()
    .InnerJoin<Category>()
    .On((p, c) => p.CategoryId == c.Id)
    .Select();
```

#### LeftJoin&lt;TJoin&gt;(string? alias = null)

Adds a LEFT JOIN to another table.

**Signature:**
```csharp
IJoinClause<T, TJoin> LeftJoin<TJoin>(string? alias = null) where TJoin : new()
```

#### RightJoin&lt;TJoin&gt;(string? alias = null)

Adds a RIGHT JOIN to another table.

**Signature:**
```csharp
IJoinClause<T, TJoin> RightJoin<TJoin>(string? alias = null) where TJoin : new()
```

### JOIN Conditions

#### On&lt;TLeftKey, TRightKey&gt;(Expression&lt;Func&lt;T, TLeftKey&gt;&gt; leftKey, Expression&lt;Func&lt;TJoin, TRightKey&gt;&gt; rightKey)

Specifies the join condition using strongly-typed expressions for the keys.

**Signature:**
```csharp
IJoinedQuery<T, TJoin> On<TLeftKey, TRightKey>(Expression<Func<T, TLeftKey>> leftKey, Expression<Func<TJoin, TRightKey>> rightKey)
```

#### On(Expression&lt;Func&lt;T, TJoin, bool&gt;&gt; predicate)

Specifies the join condition using a strongly-typed expression predicate.

**Signature:**
```csharp
IJoinedQuery<T, TJoin> On(Expression<Func<T, TJoin, bool>> predicate)
```

#### OnColumns(string leftColumn, string rightColumn)

Specifies the join condition using column names.

**Signature:**
```csharp
IJoinedQuery<T, TJoin> OnColumns(string leftColumn, string rightColumn)
```

#### OnRaw(string condition)

Specifies the join condition using raw SQL.

**Signature:**
```csharp
IJoinedQuery<T, TJoin> OnRaw(string condition)
```

### DISTINCT

#### Distinct()

Applies DISTINCT to the query results.

**Signature:**
```csharp
IDistinctClause<T> Distinct()
```

**Example:**
```csharp
var categories = connection.From<Product>()
    .Distinct()
    .SelectPartial(p => p.CategoryId);
```

### Pagination

#### Take(int count)

Limits the number of results returned (equivalent to LIMIT/TOP).

**Signature:**
```csharp
IFromClause<T> Take(int count)
```

#### Skip(int count)

Skips the specified number of results (equivalent to OFFSET).

**Signature:**
```csharp
IFromClause<T> Skip(int count)
```

**Example:**
```csharp
// Get products 11-20 (pagination)
var products = connection.From<Product>()
    .Skip(10)
    .Take(10)
    .OrderBy(p => p.ProductId)
    .Select();
```

## Terminal Operations (Sync)

### Select Methods

#### Select()

Executes the query and returns all results as a list of entities using strict mapping mode.

**Signature:**
```csharp
List<T> Select()
```

#### SelectPartial(params string[] columns)

Executes the query and returns results with only the specified columns mapped using partial mapping mode.

**Signature:**
```csharp
List<T> SelectPartial(params string[] columns)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .SelectPartial("ProductId", "ProductName", "Price");
```

#### SelectPartial(params Expression&lt;Func&lt;T, object?&gt;&gt;[] columns)

Executes the query and returns results with only the specified properties mapped using partial mapping mode.

**Signature:**
```csharp
List<T> SelectPartial(params Expression<Func<T, object?>>[] columns)
```

**Example:**
```csharp
var products = connection.From<Product>()
    .SelectPartial(p => p.ProductId, p => p.ProductName, p => p.Price);
```

### Single Result Methods

#### SelectFirst()

Returns the first result from the query. Throws an exception if the result set is empty.

**Signature:**
```csharp
T SelectFirst()
```

#### SelectFirstOrDefault()

Returns the first result from the query or the default value if the result set is empty.

**Signature:**
```csharp
T? SelectFirstOrDefault()
```

#### SelectSingle()

Returns the single result from the query. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
T SelectSingle()
```

#### SelectSingleOrDefault()

Returns the single result from the query or the default value if the result set is empty. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
T? SelectSingleOrDefault()
```

### Aggregate Methods

#### Count()

Returns the count of records in the result set.

**Signature:**
```csharp
int Count()
```

#### LongCount()

Returns the count of records in the result set as a long value.

**Signature:**
```csharp
long LongCount()
```

#### Count&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector)

Returns the count of records for the specified property.

**Signature:**
```csharp
int Count<TResult>(Expression<Func<T, TResult>> selector)
```

#### Sum&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector)

Returns the sum of the specified property values.

**Signature:**
```csharp
TResult Sum<TResult>(Expression<Func<T, TResult>> selector)
```

#### Avg&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector)

Returns the average of the specified property values.

**Signature:**
```csharp
double Avg<TResult>(Expression<Func<T, TResult>> selector)
```

#### Min&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector)

Returns the minimum value of the specified property.

**Signature:**
```csharp
TResult Min<TResult>(Expression<Func<T, TResult>> selector)
```

#### Max&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector)

Returns the maximum value of the specified property.

**Signature:**
```csharp
TResult Max<TResult>(Expression<Func<T, TResult>> selector)
```

## Terminal Operations (Async)

### SelectAsync Methods

#### SelectAsync(CancellationToken cancellationToken = default)

Asynchronously executes the query and returns all results as a list of entities using strict mapping mode.

**Signature:**
```csharp
Task<List<T>> SelectAsync(CancellationToken cancellationToken = default)
```

#### SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default)

Asynchronously executes the query and returns results with only the specified columns mapped using partial mapping mode.

**Signature:**
```csharp
Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default)
```

#### SelectPartialAsync(Expression&lt;Func&lt;T, object?&gt;&gt;[] columns, CancellationToken cancellationToken = default)

Asynchronously executes the query and returns results with only the specified properties mapped using partial mapping mode.

**Signature:**
```csharp
Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
```

### Async Single Result Methods

#### SelectFirstAsync(CancellationToken cancellationToken = default)

Asynchronously returns the first result from the query. Throws an exception if the result set is empty.

**Signature:**
```csharp
Task<T> SelectFirstAsync(CancellationToken cancellationToken = default)
```

#### SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)

Asynchronously returns the first result from the query or the default value if the result set is empty.

**Signature:**
```csharp
Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
```

#### SelectSingleAsync(CancellationToken cancellationToken = default)

Asynchronously returns the single result from the query. Throws an exception if the result set is empty or contains more than one element.

**Signature:**
```csharp
Task<T> SelectSingleAsync(CancellationToken cancellationToken = default)
```

#### SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default)

Asynchronously returns the single result from the query or the default value if the result set is empty. Throws an exception if the result set contains more than one element.

**Signature:**
```csharp
Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default)
```

### Async Aggregate Methods

#### CountAsync(CancellationToken cancellationToken = default)

Asynchronously returns the count of records in the result set.

**Signature:**
```csharp
Task<int> CountAsync(CancellationToken cancellationToken = default)
```

#### LongCountAsync(CancellationToken cancellationToken = default)

Asynchronously returns the count of records in the result set as a long value.

**Signature:**
```csharp
Task<long> LongCountAsync(CancellationToken cancellationToken = default)
```

#### CountAsync&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector, CancellationToken cancellationToken = default)

Asynchronously returns the count of records for the specified property.

**Signature:**
```csharp
Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
```

#### SumAsync&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector, CancellationToken cancellationToken = default)

Asynchronously returns the sum of the specified property values.

**Signature:**
```csharp
Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
```

#### AvgAsync&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector, CancellationToken cancellationToken = default)

Asynchronously returns the average of the specified property values.

**Signature:**
```csharp
Task<double> AvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
```

#### MinAsync&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector, CancellationToken cancellationToken = default)

Asynchronously returns the minimum value of the specified property.

**Signature:**
```csharp
Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
```

#### MaxAsync&lt;TResult&gt;(Expression&lt;Func&lt;T, TResult&gt;&gt; selector, CancellationToken cancellationToken = default)

Asynchronously returns the maximum value of the specified property.

**Signature:**
```csharp
Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
```

## SQL Generation Methods

#### ToSql()

Generates the SQL string for the current query without executing it.

**Signature:**
```csharp
string ToSql()
```

#### ToSql(params string[] columns)

Generates the SQL string for the current query with the specified columns.

**Signature:**
```csharp
string ToSql(params string[] columns)
```

#### ToSql(params Expression&lt;Func&lt;T, object?&gt;&gt;[] columns)

Generates the SQL string for the current query with the specified properties.

**Signature:**
```csharp
string ToSql(params Expression<Func<T, object?>>[] columns)
```

**Example:**
```csharp
var sql = connection.From<Product>()
    .Where(p => p.CategoryId == 1)
    .OrderBy(p => p.ProductName)
    .ToSql();
// Returns: "SELECT * FROM products WHERE category_id = @p0 ORDER BY product_name"
```

## Advanced Features

### GROUP BY Operations

#### GroupBy&lt;TKey&gt;(Expression&lt;Func&lt;T, TKey&gt;&gt; keySelector)

Groups results by the specified key.

**Signature:**
```csharp
IGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector)
```

**Example:**
```csharp
var groupedProducts = connection.From<Product>()
    .GroupBy(p => p.CategoryId)
    .Select(g => new { g.Key, Count = g.Count(), AveragePrice = g.Average(p => p.Price) });
```

## Important Notes

- **Type Safety**: The fluent API provides compile-time validation of property names
- **SQL Generation**: The API automatically generates appropriate SQL for different database providers
- **Parameterization**: All values are automatically parameterized to prevent SQL injection
- **Async Support**: All operations have both synchronous and asynchronous variants
- **Mapping Mode**: All query operations use strict mapping by default
- **Performance**: The fluent API builds SQL dynamically but still leverages Jaunty's performance optimizations
- **Database Compatibility**: The API works with all supported database providers (SQL Server, SQLite, PostgreSQL, MySQL)
- **Method Chaining**: All methods (except terminal operations) return interfaces that allow further method chaining
- **IntelliSense**: Full IntelliSense support for property names and method chaining
- **Alias Support**: Table aliases can be used for complex queries with joins