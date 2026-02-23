# Configuration

## Overview

Jaunty provides global configuration options through the `JauntyConfig` class to customize naming conventions and other behaviors. Configuration should be set at application startup before any queries are executed, as metadata is cached statically and won't pick up changes afterward.

## JauntyConfig Class

The `JauntyConfig` class provides static properties for configuring global behaviors:

### SchemaNameResolver

A function that resolves schema names for entity types. Receives the entity type and returns the schema name to use.

**Property:**
```csharp
public static Func<Type, string>? SchemaNameResolver { get; set; }
```

**Example:**
```csharp
// Use 'dbo' schema for all tables
JauntyConfig.SchemaNameResolver = type => "dbo";

// Use schema based on namespace
JauntyConfig.SchemaNameResolver = type => type.Namespace?.Split('.').Last() ?? "dbo";
```

### TableNameResolver

A function that resolves table names for entity types. Receives the entity type and returns the table name to use.

**Property:**
```csharp
public static Func<Type, string>? TableNameResolver { get; set; }
```

**Example:**
```csharp
// Use plural table names
JauntyConfig.TableNameResolver = type => $"{type.Name}s";

// Use snake_case table names
JauntyConfig.TableNameResolver = type => NamingConvention.ToSnakeCasePluralTable(type.Name);
```

### ColumnNameResolver

A function that resolves column names for property names. Receives the property name and returns the column name to use.

**Property:**
```csharp
public static Func<string, string>? ColumnNameResolver { get; set; }
```

**Example:**
```csharp
// Use snake_case column names
JauntyConfig.ColumnNameResolver = propertyName => NamingConvention.ToSnakeCase(propertyName);

// Use lowercase column names
JauntyConfig.ColumnNameResolver = propertyName => propertyName.ToLower();
```

### Reset()

Resets all configuration to their default values (null).

**Method:**
```csharp
public static void Reset()
```

**Example:**
```csharp
// Reset all configuration to defaults
JauntyConfig.Reset();
```

## NamingConvention Class

The `NamingConvention` class provides built-in naming convention helpers:

### ToSnakeCase(string input)

Converts a PascalCase or camelCase string to snake_case.

**Method:**
```csharp
public static string ToSnakeCase(string input)
```

**Example:**
```csharp
var result = NamingConvention.ToSnakeCase("ProductName"); // "product_name"
var result2 = NamingConvention.ToSnakeCase("categoryId"); // "category_id"
```

### ToLowerCase(string input)

Converts a string to lowercase.

**Method:**
```csharp
public static string ToLowerCase(string input)
```

**Example:**
```csharp
var result = NamingConvention.ToLowerCase("ProductName"); // "productname"
```

### Pluralize(string singular)

Converts a singular noun to its plural form.

**Method:**
```csharp
public static string Pluralize(string singular)
```

**Example:**
```csharp
var result = NamingConvention.Pluralize("Category"); // "Categories"
var result2 = NamingConvention.Pluralize("Product"); // "Products"
```

### SnakeCasePluralTable(string typeName)

Converts a type name to snake_case plural table name.

**Method:**
```csharp
public static string SnakeCasePluralTable(string typeName)
```

**Example:**
```csharp
var result = NamingConvention.SnakeCasePluralTable("Product"); // "products"
var result2 = NamingConvention.SnakeCasePluralTable("Category"); // "categories"
```

### SnakeCaseColumn(string propertyName)

Converts a property name to snake_case column name.

**Method:**
```csharp
public static string SnakeCaseColumn(string propertyName)
```

**Example:**
```csharp
var result = NamingConvention.SnakeCaseColumn("ProductName"); // "product_name"
```

## CommandOptions Class

The `CommandOptions` class provides per-operation configuration options:

### Constructor

```csharp
public readonly struct CommandOptions<T>(Func<IDataReader, T>? mapper = null, IDbTransaction? transaction = null, int? commandTimeout = null)
```

**Parameters:**
- `mapper`: Custom mapper function for entity mapping
- `transaction`: Database transaction to use
- `commandTimeout`: Command timeout in seconds

### Static Factory Methods

#### WithMapper(Func&lt;IDataReader, T&gt; mapper)

Creates CommandOptions with a custom mapper.

**Method:**
```csharp
public static CommandOptions<T> WithMapper(Func<IDataReader, T> mapper)
```

**Example:**
```csharp
var options = CommandOptions<Product>.WithMapper(reader => new Product
{
    ProductId = reader.GetInt32("product_id"),
    ProductName = reader.GetString("product_name")
});
```

#### WithTransaction(IDbTransaction transaction)

Creates CommandOptions with a transaction.

**Method:**
```csharp
public static CommandOptions<T> WithTransaction(IDbTransaction transaction)
```

**Example:**
```csharp
using var transaction = connection.BeginTransaction();
var options = CommandOptions<Product>.WithTransaction(transaction);
```

#### WithTimeout(int seconds)

Creates CommandOptions with a command timeout.

**Method:**
```csharp
public static CommandOptions<T> WithTimeout(int seconds)
```

**Example:**
```csharp
var options = CommandOptions<Product>.WithTimeout(30); // 30 second timeout
```

#### With(Func&lt;IDataReader, T&gt; mapper, IDbTransaction transaction, int timeoutSeconds)

Creates CommandOptions with all options.

**Method:**
```csharp
public static CommandOptions<T> With(Func<IDataReader, T> mapper, IDbTransaction transaction, int timeoutSeconds)
```

**Example:**
```csharp
using var transaction = connection.BeginTransaction();
var options = CommandOptions<Product>.With(
    mapper: reader => new Product { ProductId = reader.GetInt32("product_id") },
    transaction: transaction,
    timeoutSeconds: 30
);
```

## Non-Generic CommandOptions

For scalar operations that don't require a specific entity type:

### Constructor

```csharp
public readonly struct CommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null)
```

### Static Factory Methods

#### WithTransaction(IDbTransaction transaction)

Creates CommandOptions with a transaction.

**Method:**
```csharp
public static CommandOptions WithTransaction(IDbTransaction transaction)
```

#### WithTimeout(int seconds)

Creates CommandOptions with a command timeout.

**Method:**
```csharp
public static CommandOptions WithTimeout(int seconds)
```

#### With(IDbTransaction transaction, int timeoutSeconds)

Creates CommandOptions with both transaction and timeout.

**Method:**
```csharp
public static CommandOptions With(IDbTransaction transaction, int timeoutSeconds)
```

## Configuration Priority

Jaunty uses the following priority order for determining table/column names:

1. **[Table] and [Column] attributes** - Highest priority
2. **JauntyConfig resolvers** - Medium priority
3. **Property/type names** - Default fallback

**Example:**
```csharp
// If you have this configuration:
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;

// And this entity:
public class Product
{
    [Column("product_identifier")]  // This takes highest priority
    public int ProductId { get; set; }
    
    public string ProductName { get; set; }  // This will use snake_case: "product_name"
}
```

## Best Practices

### 1. Set Configuration at Startup

Configuration should be set once at application startup before any queries are executed:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Set Jaunty configuration at startup
    JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;
    JauntyConfig.TableNameResolver = NamingConvention.SnakeCasePluralTable;
    
    // Register your database connection
    services.AddScoped<IDbConnection>(provider => 
        new SqlConnection(connectionString));
}
```

### 2. Use Attributes for Specific Overrides

Use attributes for specific entity or property overrides:

```csharp
[Table("products", Schema = "inventory")]
public class Product
{
    [Column("prod_id")]
    public int ProductId { get; set; }
    
    [Ignore]
    public string? ComputedProperty { get; set; }
    
    public string ProductName { get; set; } = string.Empty;
}
```

### 3. Consider Thread Safety

Since configuration is global and static, ensure thread safety when setting configuration:

```csharp
// Set configuration before any threads start using Jaunty
public static void InitializeJaunty()
{
    JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;
    JauntyConfig.TableNameResolver = NamingConvention.Pluralize;
}
```

### 4. Reset Configuration for Tests

For testing purposes, you might want to reset configuration between test runs:

```csharp
public void Cleanup()
{
    JauntyConfig.Reset(); // Reset to defaults after each test
}
```

## Important Notes

- **Global Configuration**: Settings in `JauntyConfig` affect all queries globally
- **Static Caching**: Metadata is cached per-type in static constructors and won't pick up configuration changes after first use
- **Startup Timing**: Configuration must be set before any queries execute to be effective
- **Thread Safety**: The configuration properties are static and shared across all threads
- **Performance**: Once configured, the resolvers are cached and have minimal performance impact
- **Fallback Behavior**: When resolvers return null, Jaunty falls back to default behavior
- **Attribute Override**: `[Table]`, `[Column]`, and `[Ignore]` attributes take precedence over configuration
- **Reset Capability**: Use `JauntyConfig.Reset()` to clear all configuration and return to defaults
- **IMapped Behavior**: `IMapped<T>.ReadEntity` is strict/full-shape mapping; for `QueryPartial*` projections, prefer `CommandOptions<T>.WithMapper(...)` if your query may omit columns
