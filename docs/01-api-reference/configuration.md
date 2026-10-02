# Configuration

## Overview

Jaunty provides global configuration options through the `JauntyConfig` class to customize naming
conventions and other behaviors.

**Names, mapping hooks, enum storage and type handlers are set once, at startup, in one
`JauntyConfig.Configure` call, and cannot change afterwards.** Their getters stay on
`JauntyConfig`; their setters are on the builder that `Configure` passes in:

```csharp
JauntyConfig.Configure(c =>
{
    c.TableNameResolver  = type => ToSnakeCase(type.Name) + "s";   // your own helper
    c.ColumnNameResolver = ToSnakeCase;
    c.DefaultEnumStorage = EnumStorage.String;
    c.RegisterTypeHandler(new GuidAsStringHandler());
});
```

One operation reads these settings more than once (for its SQL text, then for its parameters), so
a change from another thread could pair SQL built from one setting with parameters built from
another. Setting them once removes that window. See
[decision 014](../decisions/2026-10-02-014-configuration-is-set-once-at-startup.md) and, if you
are upgrading code that assigned these properties directly,
[Upgrading to `JauntyConfig.Configure`](../06-releases/upgrading-to-configure.md).

- **Call it before anything touches Jaunty.** A query, a Fluent query, a generated member such
  as `Product.Jaunty.TableName`, or reading a mapping setting such as
  `JauntyConfig.DefaultEnumStorage` counts as first use, and `Configure` after that throws.
- **A second call with the same settings does nothing; with different settings it throws.**
  Delegates compare equal when they refer to the same method on the same target, which holds for
  method groups and for lambdas that capture nothing; type handlers compare by their type, so
  `new GuidAsStringHandler()` on each run counts as the same. Make a call whose lambda captures a
  value through `JauntyConfig.TryConfigure` instead. A repeated `Configure` runs your callback
  again to compare, so keep it free of side effects.
- **Reflection mapping is switched on first** when Extrode.Jaunty.Extensions.Reflection is
  referenced, and your callback can override any hook it set. Call `c.UseReflectionMapping()`
  yourself only in a trimmed or NativeAOT publish, where the automatic step cannot load the
  assembly.
- **Still settable at runtime:** `Logger`, the capacity properties, interceptors,
  `BulkCopyConfiguration` and dialect registration. Each is read once per use.

## JauntyConfig Class

The `JauntyConfig` class provides static properties for configuring global behaviors:

### SchemaNameResolver

A function that resolves schema names for entity types. Receives the entity type and returns the schema name to use.

**Property** (read on `JauntyConfig`, set on `JauntyConfigBuilder`):
```csharp
public static Func<Type, string>? SchemaNameResolver { get; }
```

**Example:**
```csharp
// Use schema based on namespace
JauntyConfig.Configure(c => c.SchemaNameResolver = type => type.Namespace?.Split('.').Last() ?? string.Empty);

// Or scoped: SQL Server entities get a schema, SQLite entities stay unqualified
JauntyConfig.Configure(c => c.SchemaNameResolver = type =>
    type.Namespace?.StartsWith("App.Sqlite", StringComparison.Ordinal) == true
        ? string.Empty
        : "dbo");
```

**The resolver is global and dialect-blind.** It receives a `Type` and nothing else, so one
resolver serves every connection in the process. A constant such as `type => "dbo"` is emitted
verbatim on PostgreSQL and SQLite as well as SQL Server, giving `dbo.products` and a
"no such table" error there. Scope it by type, or return `string.Empty` to leave an entity
unqualified. Returning `string.Empty` emits no schema at all, which is what Jaunty does by
default — see [`schemas.md`](schemas.md).

It applies to reflection-mapped and source-generated entities alike. A non-empty `[Table]`
schema wins over it; see the [name resolution order](../02-architecture/metadata-system-spec.md#name-resolution-order).

### TableNameResolver

A function that resolves table names for entity types. Receives the entity type and returns the table name to use.

**Property** (read on `JauntyConfig`, set on `JauntyConfigBuilder`):
```csharp
public static Func<Type, string>? TableNameResolver { get; }
```

**Example:**
```csharp
// Use plural table names
JauntyConfig.Configure(c => c.TableNameResolver = type => $"{type.Name}s");

// Or snake_case table names
JauntyConfig.Configure(c => c.TableNameResolver = type => ToSnakeCase(type.Name) + "s");   // your own helper
```

### ColumnNameResolver

A function that resolves column names for property names. Receives the property name and returns the column name to use.

**Property** (read on `JauntyConfig`, set on `JauntyConfigBuilder`):
```csharp
public static Func<string, string>? ColumnNameResolver { get; }
```

**Example:**
```csharp
// Use snake_case column names
JauntyConfig.Configure(c => c.ColumnNameResolver = ToSnakeCase);   // your own helper

// Or lowercase column names
JauntyConfig.Configure(c => c.ColumnNameResolver = propertyName => propertyName.ToLower());
```

### TryConfigure()

Applies the settings like `Configure` on the first call in a process and returns `true`; every
later call returns `false` without running the callback. Use it where startup code can run more
than once in one process with settings that compare unequal, such as a lambda capturing a value
read from configuration in a test host that runs `Program.cs` again, possibly on parallel threads.
It still throws once an operation has read the settings.

```csharp
JauntyConfig.TryConfigure(c => c.SchemaNameResolver = _ => schemaFromConfig);
```

### IsConfigured

`true` once `Configure` or `TryConfigure` has completed. Checking it and then calling `Configure`
is not atomic; use `TryConfigure` for that.

### Reset()

Restores every setting to its default and allows `Configure` to run again. **For tests only:** it
races any operation running at the same time, so a test that calls `Reset()` and then `Configure`
belongs in a test collection that does not run in parallel.

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

The global default strategy for storing enum-typed properties. Defaults to `EnumStorage.Numeric`.

**Property** (read on `JauntyConfig`, set on `JauntyConfigBuilder`):
```csharp
public static EnumStorage DefaultEnumStorage { get; }
```

**Example:**
```csharp
// Store all enums as their string name instead of the numeric value
JauntyConfig.Configure(c => c.DefaultEnumStorage = EnumStorage.String);
```

Use the `[EnumStorage(...)]` attribute (see [Attributes](attributes.md)) to override this global default on individual properties.

### RegisterTypeHandler

Registers a custom type handler for a CLR type, so Jaunty knows how to convert it to and from a database value. It is a method on `JauntyConfigBuilder`, called inside `Configure`, and returns the builder so calls chain. Jaunty is delegate-first: the simplest way to register a handler is with two conversion functions, but a `TypeHandler<T>` subclass can be used instead when the conversion needs shared state or more structure.

**Delegate-based registration:**
```csharp
public JauntyConfigBuilder RegisterTypeHandler<T>(Func<object?, T> fromDb, Func<T?, object?> toDb)
```

**Example:**
```csharp
// Store Guid values as strings
JauntyConfig.Configure(c => c.RegisterTypeHandler<Guid>(
    fromDb: dbValue => dbValue is string s ? Guid.Parse(s) : Guid.Empty,
    toDb: value => value == Guid.Empty ? null : value.Value.ToString("D")));
```

**Class-based registration:**
```csharp
public JauntyConfigBuilder RegisterTypeHandler<T>(TypeHandler<T> handler)
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

JauntyConfig.Configure(c => c.RegisterTypeHandler(new GuidAsStringHandler()));
```

Handlers are fixed at startup: there is no way to remove one later. `RemoveTypeHandler` was
removed with the move to `Configure`; a test that needs a clean slate calls `JauntyConfig.Reset()`.

## Naming conventions

> **This page previously documented a `NamingConvention` class with `ToSnakeCase`, `ToLowerCase`,
> `Pluralize`, `SnakeCasePluralTable` and `SnakeCaseColumn`. No such class has ever existed in
> `src/`.** The section was written from an intended design and never checked against the code.
> Corrected 2026-08-31.

Jaunty ships no naming-convention helpers. You supply the conversion yourself through the three
resolver delegates, set inside `JauntyConfig.Configure`:

| Delegate | Signature | Applied to |
|---|---|---|
| `SchemaNameResolver` | `Func<Type, string>?` | the schema name |
| `TableNameResolver` | `Func<Type, string>?` | the table name |
| `ColumnNameResolver` | `Func<string, string>?` | each member name |

All three are nullable and default to `null`, which means the .NET name is used unchanged. They
apply to reflection-mapped and source-generated entities alike, and only to names that no
attribute fixes: see the [name resolution order](../02-architecture/metadata-system-spec.md#name-resolution-order).

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

JauntyConfig.Configure(c =>
{
    c.ColumnNameResolver = ToSnakeCase;
    c.TableNameResolver  = type => ToSnakeCase(type.Name) + "s";   // your pluralisation
});
```

## CommandOptions Class

The `CommandOptions` class provides per-operation configuration options:

### Constructor

```csharp
public readonly struct CommandOptions<T>(Func<IDataReader, T>? mapper = null, IDbTransaction? transaction = null,
    int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
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
    ProductId = reader.GetInt32(0),
    ProductName = reader.GetString(1)
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
    mapper: reader => new Product { ProductId = reader.GetInt32(0) },
    transaction: transaction,
    timeoutSeconds: 30
);
```

## Non-Generic CommandOptions

For scalar operations that don't require a specific entity type:

### Constructor

```csharp
public readonly struct CommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null,
    CommandType commandType = CommandType.Text)
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

Jaunty resolves every table, schema and column name in one order, whether the entity is
reflection-mapped or source-generated:

1. **[Table] and [Column] attributes**, when the name they give is non-empty. `[Column("")]`
   names nothing and falls through.
2. **JauntyConfig resolvers**, when set and returning non-null. A resolver is not called for a name
   an attribute already fixes.
3. **Property/type names** (no schema), as the default.

The [name resolution order](../02-architecture/metadata-system-spec.md#name-resolution-order) diagrams each case.

**Example:**
```csharp
// If you have this configuration:
JauntyConfig.Configure(c => c.ColumnNameResolver = ToSnakeCase);   // a helper you write; Extrode.Jaunty ships none

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

`Configure` runs once, at application startup, before any queries are executed:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Set Extrode.Jaunty configuration at startup
    JauntyConfig.Configure(c =>
    {
        c.ColumnNameResolver = ToSnakeCase;   // a helper you write; Extrode.Jaunty ships none
        c.TableNameResolver = type => ToSnakeCase(type.Name) + "s";
    });
    
    // Register your database connection
    services.AddScoped<IDbConnection>(provider => 
        new SqlConnection(connectionString));
}
```

### 2. Use Attributes for Specific Overrides

Use attributes for specific entity or property overrides:

```csharp
[Table("products", "inventory")]
public class Product
{
    [Column("prod_id")]
    public int ProductId { get; set; }
    
    [Ignore]
    public string? ComputedProperty { get; set; }
    
    public string ProductName { get; set; } = string.Empty;
}
```

### 3. A Startup Path That Can Run Twice

Integration test hosts such as `WebApplicationFactory` can run `Program.cs` more than once in one
process. Identical settings are a no-op; settings captured from configuration are not, so use
`TryConfigure` for them:

```csharp
JauntyConfig.TryConfigure(c => c.TableNameResolver = type => prefix + type.Name);
```

### 4. Reset Configuration for Tests

`Reset()` restores the defaults and allows `Configure` again. Run tests that use it in a
collection that does not run in parallel with other tests using Jaunty:

```csharp
public void Cleanup()
{
    JauntyConfig.Reset(); // Reset to defaults after each test
}
```

## Important Notes

- **Global Configuration**: Settings in `JauntyConfig` affect all queries globally
- **Set Once**: `Configure` runs once per process, before first use; the mapping settings cannot change afterwards
- **Thread Safety**: The settings are one immutable snapshot shared across all threads, so no operation can see two versions of them
- **Performance**: Once configured, the resolvers are cached and have minimal performance impact
- **Fallback Behavior**: When resolvers return null, Jaunty falls back to default behavior
- **Attribute Override**: a non-empty `[Table]` or `[Column]` name, and `[Ignore]`, take precedence over configuration, on both mapping paths
- **Reset Capability**: Use `JauntyConfig.Reset()` in tests to clear all configuration, return to defaults and allow `Configure` again
- **IMapped Behavior**: `IMapped<T>.ReadEntity` is strict/full-shape mapping; for `QueryPartial*` projections, prefer `CommandOptions<T>.WithMapper(...)` if your query may omit columns
