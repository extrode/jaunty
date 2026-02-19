# Analysis: Async Namespaces and Fluent API Integration

## Updated Structure with Async Namespaces

### Directory Structure:
```
src/Jaunty/
├── Read/
│   ├── Sync/          # Query, QueryFirst, QueryScalar, etc.
│   └── Async/         # QueryAsync, QueryFirstAsync, QueryScalarAsync, etc.
├── Write/             # (Future) Insert, Update, Delete
│   ├── Sync/
│   └── Async/
├── Streaming/         # Both sync and async streaming methods
│   ├── Sync/
│   └── Async/
├── Multiple/          # QueryMultiple, GridReader
│   ├── Sync/
│   └── Async/
├── Configuration/     # JauntyConfig, NamingConvention
├── Core/              # CommandOptions, attributes
├── Fluent/            # Fluent API (fluent queries, builders, etc.)
│   ├── Sync/
│   └── Async/
└── Jaunty.csproj
```

### Namespace Structure:
```csharp
// Synchronous read operations
namespace Jaunty.Read.Sync;
connection.Query<T>(sql);
connection.QueryFirst<T>(sql);
connection.QueryScalar<T>(sql);

// Asynchronous read operations  
namespace Jaunty.Read.Async;
connection.QueryAsync<T>(sql);
connection.QueryFirstAsync<T>(sql);
connection.QueryScalarAsync<T>(sql);

// Synchronous write operations (future)
namespace Jaunty.Write.Sync;
connection.Insert<T>(entity);
connection.Update<T>(entity);

// Asynchronous write operations (future)
namespace Jaunty.Write.Async;
connection.InsertAsync<T>(entity);
connection.UpdateAsync<T>(entity);

// Streaming operations
namespace Jaunty.Streaming.Sync;
connection.QueryStream<T>(sql);

namespace Jaunty.Streaming.Async;
connection.QueryStreamAsync<T>(sql);

// Multiple result sets
namespace Jaunty.Multiple.Sync;
connection.QueryMultiple(sql);

namespace Jaunty.Multiple.Async;
connection.QueryMultipleAsync(sql);

// Configuration
namespace Jaunty.Configuration;
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;

// Core types (shared across all)
namespace Jaunty;
var options = CommandOptions.WithTimeout(30);

// Fluent API
namespace Jaunty.Fluent.Sync;
var products = connection.From<Product>()
                        .Where(p => p.Price > 100)
                        .OrderBy(p => p.Name)
                        .ToList();

namespace Jaunty.Fluent.Async;
var products = await connection.From<Product>()
                               .Where(p => p.Price > 100)
                               .ToListAsync();
```

## Fluent API Integration Analysis

### Where Fluent API Fits:
The Fluent API should be a **separate category** because:
1. **Different paradigm**: Type-safe C# code vs raw SQL
2. **Higher abstraction**: Builds on top of existing read/write APIs
3. **Distinct usage pattern**: Method chaining vs direct method calls
4. **Separate concerns**: Query building vs execution

### Fluent API Structure:
```
Jaunty.Fluent/
├── Sync/
│   ├── QueryBuilder.cs      # Main fluent builder
│   ├── WhereClause.cs       # Where operations
│   ├── OrderByClause.cs     # Ordering operations  
│   ├── JoinClause.cs        # Join operations
│   └── Execution.cs         # ToList, First, Single, etc.
└── Async/
    ├── QueryBuilder.cs      # Async version
    ├── Execution.cs         # ToListAsync, FirstAsync, etc.
    └── Streaming.cs         # Async streaming operations
```

## Benefits of This Structure:

### 1. Clear Separation of Concerns:
- **Raw SQL**: Jaunty.Read/Write - direct SQL execution
- **Fluent API**: Jaunty.Fluent - type-safe query building
- **Both paradigms** can coexist without conflict

### 2. Predictable Async Organization:
- Every sync category has corresponding async category
- Consistent naming pattern across all functionality
- Easy to find async versions of any operation

### 3. Scalable Architecture:
- New paradigms (like Fluent API) get their own clear space
- Doesn't disrupt existing read/write organization
- Easy to add new functionality categories

### 4. User Experience:
```csharp
// For raw SQL approach
using Jaunty.Read.Sync;
using Jaunty.Read.Async;

// For fluent approach  
using Jaunty.Fluent.Sync;
using Jaunty.Fluent.Async;

// For configuration
using Jaunty.Configuration;

// For core utilities
using Jaunty;
```

## API Usage Examples:

### Raw SQL Approach:
```csharp
using Jaunty.Read.Sync;
using Jaunty.Read.Async;

var products = connection.Query<Product>("SELECT * FROM products WHERE price > @Price", new { Price = 100 });
var expensiveProducts = await connection.QueryAsync<Product>("SELECT * FROM products WHERE price > @Price", new { Price = 1000 });
```

### Fluent API Approach:
```csharp
using Jaunty.Fluent.Sync;
using Jaunty.Fluent.Async;

var products = connection.From<Product>()
                        .Where(p => p.Price > 100)
                        .OrderBy(p => p.Name)
                        .ToList();

var expensiveProducts = await connection.From<Product>()
                                       .Where(p => p.Price > 1000)
                                       .ToListAsync();
```

## Final Recommendation: STICK WITH THIS STRUCTURE

### Why This Structure Is Superior:

1. **Addresses async requirement**: Every sync category has async counterpart
2. **Accommodates Fluent API**: Natural, separate space for type-safe queries
3. **Maintains read/write separation**: Fundamental distinction preserved
4. **Scales well**: Easy to add new paradigms or functionality categories
5. **User-friendly**: Predictable, discoverable namespace organization
6. **Maintainable**: Clear boundaries and consistent patterns

### This structure successfully integrates:
- Raw SQL approach (Read/Write)
- Async organization (separate async namespaces)
- Fluent API (separate category)
- User experience (simple, predictable imports)
- Future extensibility (new paradigms can be added cleanly)

The structure is robust, scalable, and maintains excellent separation of concerns while providing intuitive access patterns for end users.