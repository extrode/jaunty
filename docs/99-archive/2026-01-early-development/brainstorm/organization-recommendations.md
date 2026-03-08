# Jaunty Code Organization Analysis and Recommendations

## Current Structure Analysis

### Current Directory Structure
```
src/Jaunty/
├── InternalApi/
│   ├── Dialects/
│   ├── Enums/
│   ├── Metadata/
│   ├── Parameters/
│   ├── Write/
│   ├── CachedSql.cs
│   ├── DrDispatcher.cs
│   ├── ExecuteCore.cs
│   ├── ExecuteQueryMultiple.cs
│   ├── ExecuteQueryMultipleAsync.cs
│   ├── ExecuteReader.cs
│   ├── ExecuteReaderAsync.cs
│   ├── MappedCache.cs
│   ├── QueryCore.cs
│   └── QueryCoreAsync.cs
├── PublicApi/
│   ├── Attributes/
│   ├── Configuration/
│   ├── Interfaces/
│   ├── GridReader.cs
│   ├── CommandOptions.cs
│   ├── ExecuteScalar.cs
│   ├── ExecuteScalarAsync.cs
│   ├── Query.cs
│   ├── QueryAsync.cs
│   ├── QueryFirst.cs
│   ├── QueryFirstAsync.cs
│   ├── QueryFirstOrDefault.cs
│   ├── QueryFirstOrDefaultAsync.cs
│   ├── QueryMultiple.cs
│   ├── QueryMultipleAsync.cs
│   ├── QueryPartial.cs
│   ├── QueryPartialAsync.cs
│   ├── QueryPartialStream.cs
│   ├── QueryPartialStreamAsync.cs
│   ├── QueryScalar.cs
│   ├── QueryScalarAsync.cs
│   ├── QuerySingle.cs
│   ├── QuerySingleAsync.cs
│   ├── QuerySingleOrDefault.cs
│   ├── QuerySingleOrDefaultAsync.cs
│   ├── QueryStream.cs
│   └── QueryStreamAsync.cs
├── Readers/
│   └── EntityReader.cs
└── Jaunty.csproj
```

### Current Namespace Structure
- **Public API**: All in `namespace Jaunty;` (file-scoped)
- **Internal API**: Various internal classes scattered across different concerns
- **Attributes**: `namespace Jaunty.PublicApi.Attributes;`
- **Configuration**: `namespace Jaunty.PublicApi.Configuration;`
- **Interfaces**: `namespace Jaunty.PublicApi.Interfaces;`

## Problems with Current Organization

### 1. Namespace Confusion
- Public API methods are in `Jaunty` namespace but spread across many files
- No clear grouping by functionality
- Extension methods are not clearly organized

### 2. Developer Experience Issues
- Hard to discover related functionality
- Namespace doesn't clearly indicate purpose
- No logical grouping for end-users

### 3. Maintenance Challenges
- Internal implementation details mixed with public contracts
- Unclear boundaries between public and internal APIs
- Difficult to navigate related functionality

## Recommended Organization Structure

### New Directory Structure
```
src/Jaunty/
├── Core/
│   ├── Execution/
│   │   ├── Sync/
│   │   │   ├── QueryExecution.cs
│   │   │   ├── ScalarExecution.cs
│   │   │   └── MultipleExecution.cs
│   │   └── Async/
│   │       ├── QueryExecution.cs
│   │       ├── ScalarExecution.cs
│   │       └── MultipleExecution.cs
│   ├── Mapping/
│   │   ├── Core.cs
│   │   ├── Cache.cs
│   │   └── Dispatcher.cs
│   └── Infrastructure/
│       ├── Parameters.cs
│       ├── Readers.cs
│       └── Utilities.cs
├── Extensions/
│   ├── Query/
│   │   ├── Query.cs
│   │   ├── QueryPartial.cs
│   │   ├── QueryFirst.cs
│   │   ├── QuerySingle.cs
│   │   └── QueryScalar.cs
│   ├── Query.Async/
│   │   ├── Query.cs
│   │   ├── QueryPartial.cs
│   │   ├── QueryFirst.cs
│   │   ├── QuerySingle.cs
│   │   └── QueryScalar.cs
│   ├── Streaming/
│   │   ├── QueryStream.cs
│   │   └── QueryPartialStream.cs
│   └── Streaming.Async/
│       ├── QueryStream.cs
│       └── QueryPartialStream.cs
├── Configuration/
│   ├── JauntyConfig.cs
│   └── NamingConventions.cs
├── Attributes/
│   ├── TableAttribute.cs
│   ├── ColumnAttribute.cs
│   ├── KeyAttribute.cs
│   ├── IgnoreAttribute.cs
│   └── AttributeHelper.cs
├── Models/
│   ├── CommandOptions.cs
│   └── GridReader.cs
├── Enums/
│   └── MappingMode.cs
└── Jaunty.csproj
```

### Recommended Namespace Structure

#### 1. Public API Namespaces (for end-users)
```csharp
// Primary namespace - main extension methods
namespace Jaunty;

// Configuration
namespace Jaunty.Configuration;

// Attributes
namespace Jaunty.Attributes;

// Models/Data Structures
namespace Jaunty.Models;

// Advanced features
namespace Jaunty.Advanced;
```

#### 2. Internal API Namespaces (for maintainers)
```csharp
// Core execution engine
namespace Jaunty.Internal.Execution;
namespace Jaunty.Internal.Mapping;
namespace Jaunty.Internal.Parameters;

// Async execution
namespace Jaunty.Internal.Execution.Async;

// Infrastructure
namespace Jaunty.Internal.Infrastructure;
```

## Detailed Namespace Recommendations

### For End-Users (Public API):

#### Primary Namespace: `Jaunty`
- Contains main extension methods on `IDbConnection`
- This is what users will primarily interact with
- Methods like `Query<T>()`, `QueryAsync<T>()`, `QueryScalar<T>()`, etc.

#### Configuration: `Jaunty.Configuration`
- `JauntyConfig` class
- `NamingConvention` helper class
- All configuration-related functionality

#### Attributes: `Jaunty.Attributes`
- All mapping attributes: `[Table]`, `[Column]`, `[Key]`, `[Ignore]`
- Clear, dedicated space for mapping customization

#### Models: `Jaunty.Models`
- `CommandOptions<T>` and `CommandOptions`
- `GridReader`
- Supporting data structures

### For Developers (Internal API):

#### Core Components:
- `Jaunty.Internal.Mapping` - Entity mapping logic
- `Jaunty.Internal.Execution` - Query execution logic
- `Jaunty.Internal.Parameters` - Parameter binding logic
- `Jaunty.Internal.Caching` - Metadata caching

## Benefits of Recommended Structure

### 1. For End-Users:
- **Clear entry point**: `using Jaunty;` gives access to main functionality
- **Logical grouping**: Related features are grouped together
- **Discoverability**: IntelliSense shows related functionality together
- **Minimal cognitive load**: Only need to know main namespace for basic usage

### 2. For Developers:
- **Clear separation**: Public vs internal APIs clearly separated
- **Maintainable**: Related functionality grouped in logical modules
- **Extensible**: Easy to add new functionality in appropriate sections
- **Testable**: Clear boundaries between components

### 3. For Documentation:
- **Organized API reference**: Each namespace represents a logical feature set
- **Easy navigation**: Users can find functionality by category
- **Progressive learning**: Start with basic `Jaunty` namespace, expand to others as needed

## Usage Examples with Recommended Structure

### Basic Usage:
```csharp
using Jaunty;  // Main extension methods

var products = connection.Query<Product>("SELECT * FROM products");
```

### Advanced Configuration:
```csharp
using Jaunty;
using Jaunty.Configuration;

JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;
```

### With Attributes:
```csharp
using Jaunty;
using Jaunty.Attributes;

[Table("products")]
public class Product
{
    [Column("product_id")]
    public int Id { get; set; }
}
```

### Advanced Features:
```csharp
using Jaunty;
using Jaunty.Models;

var options = CommandOptions.WithTimeout(30);
var products = connection.Query<Product>("SELECT * FROM products", options);
```

## Migration Strategy Considerations

### Backward Compatibility:
- Keep main `Jaunty` namespace for extension methods to maintain compatibility
- Possibly add type aliases for commonly used types
- Provide clear upgrade path documentation

### Gradual Migration:
- Introduce new structure alongside existing one
- Deprecate old patterns gradually
- Maintain both for a transition period

This organization provides clear separation of concerns while maintaining usability for end-users and maintainability for developers.