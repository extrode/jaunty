# Multiple Result Sets in ORMs: Research and Solutions

## Introduction

Multiple result sets (MRS) is a feature that allows a single database command to return multiple result sets, which is commonly used in stored procedures or batch queries. Different ORMs handle this feature differently, and database providers have varying levels of support for it.

## How Different ORMs Handle Multiple Result Sets

### 1. Dapper

Dapper provides multiple approaches for handling multiple result sets:

#### QueryMultiple Method
```csharp
using (var multi = connection.QueryMultiple("sp_GetOrdersAndDetails", commandType: CommandType.StoredProcedure))
{
    var orders = multi.Read<Order>().ToList();
    var orderDetails = multi.Read<OrderDetail>().ToList();
}
```

#### Key Features:
- Uses `GridReader` (similar to Jaunty's approach)
- Provides `Read<T>()`, `ReadAsync<T>()`, `ReadFirst<T>()`, `ReadSingle<T>()`, `ReadSingleOrDefault<T>()`, etc.
- Handles multiple result sets sequentially
- Supports cancellation tokens for async operations
- Allows buffering control (buffered: true/false)
- Works with both raw SQL and stored procedures

#### Implementation Details:
- QueryMultiple returns a GridReader that holds the results of multiple queries
- Each call to Read/ReadFirst/ReadSingle methods advances the reader to the next result set
- The GridReader implements IDisposable and should be properly disposed
- Supports all standard Dapper parameterization features

#### Limitations:
- Like Jaunty, faces issues with SQLite's limited MRS support
- Relies on the underlying database provider's MRS implementation

### 2. Entity Framework Core

EF Core handles multiple result sets differently and has limited direct support:

#### GitHub Issue #8127: "FromSql: Support multiple resultsets"
- This is a long-standing feature request from 2017 that remains open
- Currently, EF Core's `FromSql` method only supports single result sets
- For multiple result sets, the workaround is to drop down to ADO.NET and use DbConnection directly
- EF Core focuses on related entity loading rather than arbitrary multiple result sets

#### Split Queries (EF Core 5+)
```csharp
var blogs = context.Blogs
    .FromSqlRaw("SELECT * FROM Blogs; SELECT * FROM Posts;")
    .Include(blog => blog.Posts)
    .AsSplitQuery()
    .ToList();
```

#### Key Features:
- Uses `Include` with `AsSplitQuery()` for related data
- Provides better abstraction over the database provider
- When split query is used, the result sets of all but the last query are buffered (unless MARS is enabled)

#### Limitations:
- No direct support for arbitrary multiple result sets like Dapper
- More abstracted from raw SQL multiple result sets
- Less direct control over multiple result set handling
- SQLite support varies depending on EF Core version

### 3. PetaPoco

PetaPoco handles multiple result sets differently - through multi-poco queries rather than multiple result sets:

#### Multi-Poco Queries
Instead of multiple result sets, PetaPoco provides multi-poco queries for JOIN operations:
```csharp
var sql = PetaPoco.Sql.Builder
                .Append("SELECT articles.*, users.*")
                .Append("FROM articles")
                .Append("LEFT JOIN users ON articles.user_id = users.user_id");

var result = db.Query<article, user, article>((a,u) => { a.user=u; return a }, sql);
```

#### Key Features:
- Maps columns from multiple joined tables to different POCOs in a single result set
- Determines split points based on column order and naming conflicts
- Supports up to 5 POCO types in multi-poco queries
- Offers both `Fetch` and `Query` variations

#### Limitations:
- Focuses on JOIN queries rather than multiple result sets
- Does not provide direct multiple result set functionality like Dapper's QueryMultiple

### 4. ServiceStack.OrmLite

ServiceStack.OrmLite's approach to multiple result sets is less clear from the documentation, but based on community discussions:

#### Community Discussions
- There are questions about whether OrmLite has a QueryMultiple solution like Dapper
- For scenarios requiring multiple result sets, developers often resort to raw ADO.NET
- The documentation doesn't explicitly describe multiple result set functionality

#### Key Features (Inferred):
- Primarily focuses on single result set operations
- May not have direct multiple result set support comparable to Dapper
- Uses SqlList() for single result set operations from stored procedures

## Database Provider Support for Multiple Result Sets

### SQL Server
- Full support for multiple result sets
- Supports both synchronous and asynchronous operations
- Reliable `NextResult()` and `NextResultAsync()` implementations
- Works well with all major ORMs
- Supports MARS (Multiple Active Result Sets) for concurrent result set operations

### SQLite
- **Severe limitations** for multiple result sets
- `NextResult()` and `NextResultAsync()` have known issues
- `IsDBNull()` can throw `NullReferenceException` in certain states
- `GetSQLiteType()` and `GetValue()` can throw exceptions when reader is in invalid state
- Not designed for complex multiple result set scenarios
- Better suited for single-result queries
- Each sqlite3_stmt* handle represents one result set - multiple result sets require separate statements

### PostgreSQL
- Good support for multiple result sets
- More reliable than SQLite but not as robust as SQL Server
- Some async operation limitations in older versions

### MySQL
- Moderate support for multiple result sets
- `NextResult()` generally works but has some edge cases
- Async support varies by connector version

## Recommendations for Jaunty

### 1. Documentation and Error Handling
- Clearly document SQLite's limitations with multiple result sets
- Provide specific error messages when MRS operations are attempted on SQLite
- Suggest alternatives for SQLite users (separate queries, different database providers)

### 2. Provider Detection
- Implement automatic detection of SQLite to provide appropriate warnings or alternative behavior
- Consider disabling MRS functionality with SQLite and providing clear guidance

### 3. Testing Strategy
- Separate MRS tests from basic query tests
- Mark MRS tests as provider-dependent with appropriate skip conditions
- Use different test databases for different feature sets

### 4. Alternative APIs
- Consider providing alternative approaches for scenarios that need multiple queries
- Offer batch query alternatives that work with SQLite
- Provide clear guidance on when to use MRS vs. separate queries

## Recommendations for Jaunty

### 1. Documentation
- Clearly document SQLite's limitations with multiple result sets
- Provide guidance on which databases fully support MRS
- Suggest alternatives for SQLite users (separate queries)

### 2. Error Handling
- Provide more specific error messages when MRS isn't supported
- Detect SQLite and provide helpful guidance
- Implement graceful degradation for unsupported scenarios

### 3. Testing Strategy
- Separate MRS tests from basic query tests
- Use different test databases for different feature sets
- Clearly mark MRS tests as provider-dependent

### 4. Alternative APIs
- Consider providing alternative approaches for scenarios that need multiple queries
- Offer batch query alternatives that work with SQLite
- Provide clear guidance on when to use MRS vs. separate queries

## Conclusion

Multiple result set support varies significantly across database providers. While SQL Server provides robust MRS support, SQLite has fundamental limitations that affect all ORMs similarly. The approach taken by Jaunty is consistent with industry standards (similar to Dapper), but the library needs to better handle provider-specific limitations, particularly with SQLite.

The solution isn't necessarily to "fix" the SQLite MRS functionality (since it's a database limitation), but rather to provide better error messages, documentation, and alternative approaches for users who need to work with SQLite.