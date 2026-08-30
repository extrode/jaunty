# Configuration

## Overview

Jaunty provides global configuration options through the `JauntyConfig` class to customize naming
conventions and other behaviors. Set them at application startup, before any queries run.

**That is advice, not a constraint.** Metadata used to be cached in a static constructor and
compiled once per type per process, so a resolver registered after a type had been read was
silently ignored for the life of the process. It no longer is: every setter on `JauntyConfig`
bumps a configuration generation, and metadata compiled under an older generation — along with
the per-reader setter caches built from it — is retired and rebuilt on next use. Startup is
still the right place, because changing configuration mid-flight throws away compiled work.

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
JauntyConfig.TableNameResolver = type => ToSnakeCase(type.Name) + "s";   // your own helper
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
JauntyConfig.ColumnNameResolver = propertyName => ToSnakeCase(propertyName);   // your own helper

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

### DefaultEnumStorage

Gets or sets the global default strategy for storing enum-typed properties. Defaults to `EnumStorage.Numeric`.

**Property:**
```csharp
public static EnumStorage DefaultEnumStorage { get; set; }
```

**Example:**
```csharp
// Store all enums as their string name instead of the numeric value
JauntyConfig.DefaultEnumStorage = EnumStorage.String;
```

Use the `[EnumStorage(...)]` attribute (see [Attributes](attributes.md)) to override this global default on individual properties.

### RegisterTypeHandler

Registers a custom type handler for a CLR type, so Jaunty knows how to convert it to and from a database value. Jaunty is delegate-first: the simplest way to register a handler is with two conversion functions, but a `TypeHandler<T>` subclass can be used instead when the conversion needs shared state or more structure.

**Delegate-based registration:**
```csharp
public static void RegisterTypeHandler<T>(Func<object?, T> fromDb, Func<T?, object?> toDb)
```

**Example:**
```csharp
// Store Guid values as strings
JauntyConfig.RegisterTypeHandler<Guid>(
    fromDb: dbValue => dbValue is string s ? Guid.Parse(s) : Guid.Empty,
    toDb: value => value == Guid.Empty ? null : value.Value.ToString("D"));
```

**Class-based registration:**
```csharp
public static void RegisterTypeHandler<T>(TypeHandler<T> handler)
```

**Example:**
```csharp
public class GuidAsStringHandler : TypeHandler<Guid>
{
    public override Guid Parse(object? dbValue) =>
        dbValue is string s ? Guid.Parse(s) : Guid.Empty;

    public override object? ToDbValue(Guid? value) =>
        value is null || value == Guid.Empty ? null : value.Value.ToString("D");
}

JauntyConfig.RegisterTypeHandler(new GuidAsStringHandler());
```

### RemoveTypeHandler&lt;T&gt;()

Removes the registered type handler for a type, if one exists.

**Method:**
```csharp
public static bool RemoveTypeHandler<T>()
```

**Returns:** `true` if a handler was registered and removed; `false` if no handler was registered for `T`.

**Example:**
```csharp
JauntyConfig.RemoveTypeHandler<Guid>();
```

## Naming conventions

> **This page previously documented a `NamingConvention` class with `ToSnakeCase`, `ToLowerCase`,
> `Pluralize`, `SnakeCasePluralTable` and `SnakeCaseColumn`. No such class has ever existed in
> `src/`.** The section was written from an intended design and never checked against the code.
> Corrected 2026-08-31.

Jaunty ships no naming-convention helpers. You supply the conversion yourself through the three
resolver delegates on `JauntyConfig`:

| Delegate | Signature | Applied to |
|---|---|---|
| `SchemaNameResolver` | `Func<Type, string>?` | the schema name |
| `TableNameResolver` | `Func<Type, string>?` | the table name |
| `ColumnNameResolver` | `Func<string, string>?` | each member name |

All three are nullable and default to `null`, which means the .NET name is used unchanged.

Nothing stops you writing the conversions; there is deliberately no inflector in the box, because
pluralisation is language- and schema-specific and a wrong guess is worse than no guess.

```csharp
static string ToSnakeCase(string name)
{
    var sb = new StringBuilder(name.Length + 8);
    for (int i = 0; i < name.Length; i++)
    {
        char c = name[i];
        if (char.IsUpper(c))
        {
            if (i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        else sb.Append(c);
    }
    return sb.ToString();
}

JauntyConfig.ColumnNameResolver = ToSnakeCase;
JauntyConfig.TableNameResolver  = type => ToSnakeCase(type.Name) + "s";   // your pluralisation
```

Setting any resolver bumps a configuration generation, so metadata already compiled for a type is
retired and rebuilt rather than going stale. Startup is still the sensible place to configure
them, because a mid-flight change discards compiled mappers, but it is not a correctness
requirement.

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
JauntyConfig.ColumnNameResolver = ToSnakeCase;   // a helper you write; Jaunty ships none

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
    JauntyConfig.ColumnNameResolver = ToSnakeCase;   // a helper you write; Jaunty ships none
    JauntyConfig.TableNameResolver = type => ToSnakeCase(type.Name) + "s";
    
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
    JauntyConfig.ColumnNameResolver = ToSnakeCase;   // a helper you write; Jaunty ships none
    JauntyConfig.TableNameResolver = type => type.Name + "s";
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
