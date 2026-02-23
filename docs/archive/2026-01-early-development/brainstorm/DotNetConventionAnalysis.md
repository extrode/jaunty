# Analysis: .NET Conventions for Sync/Async APIs

## Current .NET Convention Analysis

### Standard .NET Approach:
Looking at major .NET libraries and frameworks:

#### System.Threading.Tasks:
- `Task.FromResult()` - sync method returning async type
- `Task.Delay()` - async method in same namespace

#### System.IO:
- `File.ReadAllText()` - sync
- `File.ReadAllTextAsync()` - async version with "Async" suffix
- Both in same namespace: `System.IO`

#### Microsoft.EntityFrameworkCore:
- `DbContext.SaveChanges()` - sync
- `DbContext.SaveChangesAsync()` - async
- Both in same namespace: `Microsoft.EntityFrameworkCore`

#### System.Net.Http:
- `HttpClient.GetAsync()` - async (only async version available)
- `HttpClient.Get()` - sync (deprecated)
- All in same namespace: `System.Net.Http`

#### System.Linq:
- `Enumerable.Where()` - sync
- `AsyncEnumerable.Where()` - async
- Different types but related functionality

## Correct .NET Convention:
**Sync and Async versions of the same functionality should live in the same namespace**, with async methods typically suffixed with "Async".

### Examples:
```csharp
// System.Data.SqlClient
using System.Data.SqlClient;

connection.Open();      // sync
connection.OpenAsync(); // async

// System.IO
using System.IO;

File.ReadAllText();     // sync
File.ReadAllTextAsync(); // async

// Microsoft.EntityFrameworkCore
using Microsoft.EntityFrameworkCore;

context.SaveChanges();      // sync
context.SaveChangesAsync(); // async
```

## Recommended Structure Based on .NET Conventions:

### Directory Structure:
```
src/Jaunty/
├── Read/              # All read operations (sync + async)
│   ├── Query.cs       // Query() + QueryAsync()
│   ├── QueryFirst.cs  // QueryFirst() + QueryFirstAsync()
│   ├── QueryScalar.cs // QueryScalar() + QueryScalarAsync()
│   └── QueryMultiple.cs // QueryMultiple() + QueryMultipleAsync()
├── Write/             # All write operations (sync + async when added)
├── Streaming/         # All streaming operations (sync + async)
│   ├── QueryStream.cs // QueryStream() + QueryStreamAsync()
│   └── QueryPartialStream.cs // QueryPartialStream() + QueryPartialStreamAsync()
├── Fluent/            # All fluent operations (sync + async)
│   ├── QueryBuilder.cs // From() + FromAsync() methods
│   └── Execution.cs   // ToList() + ToListAsync(), etc.
├── Configuration/     # JauntyConfig, NamingConvention
├── Core/              # CommandOptions, attributes
└── Jaunty.csproj
```

### Namespace Structure:
```csharp
// All read operations in same namespace
namespace Jaunty.Read;
connection.Query<T>(sql);        // sync
connection.QueryAsync<T>(sql);   // async

connection.QueryFirst<T>(sql);   // sync
connection.QueryFirstAsync<T>(sql); // async

connection.QueryScalar<T>(sql);  // sync
connection.QueryScalarAsync<T>(sql); // async

// All write operations in same namespace (future)
namespace Jaunty.Write;
connection.Insert<T>(entity);     // sync
connection.InsertAsync<T>(entity); // async

// All streaming operations in same namespace
namespace Jaunty.Streaming;
connection.QueryStream<T>(sql);     // sync
connection.QueryStreamAsync<T>(sql); // async

// All fluent operations in same namespace
namespace Jaunty.Fluent;
var query = connection.From<Product>(); // builder
var list = query.ToList();              // sync execution
var list = await query.ToListAsync();   // async execution

// Configuration
namespace Jaunty.Configuration;
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;

// Core types
namespace Jaunty;
var options = CommandOptions.WithTimeout(30);
```

## API Usage Examples Following .NET Conventions:

### Read Operations:
```csharp
using Jaunty.Read;

// Sync operations
var products = connection.Query<Product>("SELECT * FROM products");
var first = connection.QueryFirst<Product>("SELECT * FROM products LIMIT 1");
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

// Async operations (same namespace, Async suffix)
var products = await connection.QueryAsync<Product>("SELECT * FROM products");
var first = await connection.QueryFirstAsync<Product>("SELECT * FROM products LIMIT 1");
var count = await connection.QueryScalarAsync<long>("SELECT COUNT(*) FROM products");
```

### Fluent Operations:
```csharp
using Jaunty.Fluent;

var query = connection.From<Product>()
                     .Where(p => p.Price > 100);

// Sync execution
var products = query.ToList();
var first = query.First();
var count = query.Count();

// Async execution (same namespace, Async suffix)
var products = await query.ToListAsync();
var first = await query.FirstAsync();
var count = await query.CountAsync();
```

## Benefits of Convention-Compliant Structure:

### 1. Familiarity:
- .NET developers expect sync/async in same namespace
- Consistent with System.IO, EntityFramework, etc.

### 2. Discoverability:
- IntelliSense shows both sync and async versions together
- Users can easily find async equivalent of sync method

### 3. Simplicity:
- Fewer using statements needed
- Clear relationship between sync/async methods

### 4. Maintainability:
- Follows established .NET patterns
- Easier for other .NET developers to understand

## Final Recommendation:

**ABANDON the separate Async namespace approach.** Instead, use the .NET convention-compliant structure:

- Sync and Async methods in same namespace
- Async methods suffixed with "Async"
- Users import one namespace for both sync/async
- Follows .NET ecosystem patterns
- Maintains Read/Write/Fluent separation
- Preserves user-friendly experience

This approach aligns with .NET conventions while maintaining the logical separation you wanted for Read/Write/Fluent functionality.