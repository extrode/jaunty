# Jaunty.Scaffolding

Database scaffolding library for Jaunty. Reads schema from a live database and generates C# entity class source files, optionally with Jaunty or DataAnnotations attributes.

## Purpose

`Jaunty.Scaffolding` provides the scaffolding engine used by the `dotnet-jaunty` CLI tool and consumable directly from .NET code when you need programmatic control over scaffolding (CI pipelines, custom tooling, embedding in your own CLI).

## Supported providers

| Provider | `DatabaseProvider` enum value |
|---|---|
| SQL Server | `SqlServer` |
| PostgreSQL | `PostgreSql` |
| MySQL / MariaDB | `MySql` |
| SQLite | `SQLite` |
| Auto-detect from connection string | `AutoDetect` (default) |

## Usage

```csharp
using Jaunty.Scaffolding;
using Jaunty.Scaffolding.Configuration;

var scaffolder = new Scaffolder();

var options = new ScaffoldOptions
{
    ConnectionString    = Environment.GetEnvironmentVariable("DB_CONN")!,
    Provider            = DatabaseProvider.SQLite,
    OutputDirectory     = "./Entities",
    Namespace           = "MyApp.Data.Entities",
    UseNullableReferenceTypes = true,
    GeneratePartialClasses    = false,
    // optional filters
    Tables              = [],          // empty = all tables
    ExcludeTables       = [],
    Schemas             = [],
};

ScaffoldResult result = await scaffolder.ScaffoldAsync(options);

Console.WriteLine($"Generated {result.GeneratedFiles.Count} file(s).");
foreach (string path in result.GeneratedFiles)
    Console.WriteLine($"  {path}");
```

### List tables without generating

```csharp
IReadOnlyList<(string Schema, string Table)> tables =
    await scaffolder.ListTablesAsync(
        Environment.GetEnvironmentVariable("DB_CONN")!,
        DatabaseProvider.AutoDetect);

foreach ((string schema, string table) in tables)
    Console.WriteLine($"{schema}.{table}");
```

## Key abstractions

| Type | Role |
|---|---|
| `ISchemaReader` | Reads raw schema from a provider-specific database |
| `ITypeMapper` | Maps database column types to C# type names |
| `ICodeGenerator` | Renders entity class source from a `TableSchema` |
| `Scaffolder` | Orchestrates reader, mapper, and generator; public entry point |
| `ScaffoldOptions` | Configuration passed to `ScaffoldAsync` |
| `ScaffoldResult` | Returned list of written file paths |

### Implementing a custom provider

Implement `ISchemaReader` and `ITypeMapper`, then pass them to `Scaffolder` via the overload that accepts explicit abstractions:

```csharp
var scaffolder = new Scaffolder(
    schemaReader:  new MyCustomSchemaReader(),
    typeMapper:    new MyCustomTypeMapper(),
    codeGenerator: new EntityCodeGenerator());
```

## Naming conventions

By default class names are singularized from table names (`orders` -> `Order`). Disable with `ScaffoldOptions.SingularizeTableNames = false`. Add a prefix or suffix with `ClassPrefix` / `ClassSuffix`.
