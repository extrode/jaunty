# Phase 1: Database Scaffolding MVP - Implementation Plan

## Goal

Build a minimal viable scaffolding tool that:
- Connects to a SQLite database
- Reads table/view schema
- Generates basic C# entity classes
- Provides a simple CLI interface

**Why SQLite first?**
- Simplest schema introspection (PRAGMA commands)
- Already have Northwind test database
- No external database server needed for testing
- Fast iteration during development

---

## Project Structure

```
Jaunty/
├── src/
│   ├── Jaunty/                          # Existing library
│   └── Jaunty.Scaffold/                 # NEW: Scaffolding tool
│       ├── Jaunty.Scaffold.csproj
│       ├── Program.cs                   # CLI entry point
│       ├── Commands/
│       │   └── ScaffoldCommand.cs       # Main scaffold command
│       ├── Schema/
│       │   ├── ISchemaReader.cs         # Interface for dialect readers
│       │   ├── SqliteSchemaReader.cs    # SQLite implementation
│       │   ├── DatabaseSchema.cs        # Schema model
│       │   ├── TableSchema.cs
│       │   └── ColumnSchema.cs
│       ├── TypeMapping/
│       │   ├── ITypeMapper.cs           # Interface for type mapping
│       │   └── SqliteTypeMapper.cs      # SQLite → C# type mapping
│       ├── Naming/
│       │   ├── NamingStyle.cs           # Enum: snake_case, PascalCase, etc.
│       │   └── NameConverter.cs         # Conversion utilities
│       └── CodeGen/
│           ├── EntityGenerator.cs       # Generates entity classes
│           └── CodeWriter.cs            # File writing utilities
│
└── tests/
    └── Jaunty.Scaffold.Tests/           # NEW: Scaffold tests
        ├── Schema/
        │   └── SqliteSchemaReaderTests.cs
        ├── TypeMapping/
        │   └── SqliteTypeMapperTests.cs
        ├── Naming/
        │   └── NameConverterTests.cs
        └── CodeGen/
            └── EntityGeneratorTests.cs
```

---

## Component Specifications

### 1. Schema Models

```csharp
// Schema/DatabaseSchema.cs
namespace Jaunty.Scaffold.Schema;

public sealed class DatabaseSchema
{
    public required string DatabaseName { get; init; }
    public required IReadOnlyList<TableSchema> Tables { get; init; }
    public required IReadOnlyList<TableSchema> Views { get; init; }
}

// Schema/TableSchema.cs
public sealed class TableSchema
{
    public required string Name { get; init; }
    public string? Schema { get; init; }  // null for SQLite
    public required bool IsView { get; init; }
    public required IReadOnlyList<ColumnSchema> Columns { get; init; }
    public required IReadOnlyList<string> PrimaryKeyColumns { get; init; }
}

// Schema/ColumnSchema.cs
public sealed class ColumnSchema
{
    public required string Name { get; init; }
    public required string DataType { get; init; }      // Raw SQL type
    public required bool IsNullable { get; init; }
    public required bool IsPrimaryKey { get; init; }
    public required int Ordinal { get; init; }

    // Optional metadata
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public bool IsAutoIncrement { get; init; }
    public string? DefaultValue { get; init; }
}
```

### 2. Schema Reader Interface & SQLite Implementation

```csharp
// Schema/ISchemaReader.cs
namespace Jaunty.Scaffold.Schema;

public interface ISchemaReader
{
    Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}

// Schema/SqliteSchemaReader.cs
public sealed class SqliteSchemaReader : ISchemaReader
{
    public async Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tables = await ReadTablesAsync(connection, isView: false, cancellationToken);
        var views = await ReadTablesAsync(connection, isView: true, cancellationToken);

        return new DatabaseSchema
        {
            DatabaseName = ExtractDatabaseName(connectionString),
            Tables = tables,
            Views = views
        };
    }

    private async Task<List<TableSchema>> ReadTablesAsync(
        SqliteConnection connection,
        bool isView,
        CancellationToken cancellationToken)
    {
        var tableType = isView ? "view" : "table";
        var tables = new List<TableSchema>();

        // Get all tables/views
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT name FROM sqlite_master
            WHERE type = '{tableType}'
            AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var tableNames = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            tableNames.Add(reader.GetString(0));
        }

        // Get columns for each table
        foreach (var tableName in tableNames)
        {
            var columns = await ReadColumnsAsync(connection, tableName, cancellationToken);
            var primaryKeys = columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();

            tables.Add(new TableSchema
            {
                Name = tableName,
                Schema = null,
                IsView = isView,
                Columns = columns,
                PrimaryKeyColumns = primaryKeys
            });
        }

        return tables;
    }

    private async Task<List<ColumnSchema>> ReadColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName}')";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var columns = new List<ColumnSchema>();

        // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnSchema
            {
                Ordinal = reader.GetInt32(0),           // cid
                Name = reader.GetString(1),              // name
                DataType = reader.GetString(2),          // type
                IsNullable = reader.GetInt32(3) == 0,    // notnull (inverted)
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsPrimaryKey = reader.GetInt32(5) == 1,  // pk
                IsAutoIncrement = false  // Check separately via sqlite_sequence
            });
        }

        // Check for autoincrement (INTEGER PRIMARY KEY is implicit autoincrement in SQLite)
        foreach (var col in columns.Where(c => c.IsPrimaryKey))
        {
            if (col.DataType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
            {
                // SQLite: INTEGER PRIMARY KEY is alias for rowid (autoincrement)
                col = col with { IsAutoIncrement = true };
            }
        }

        return columns;
    }
}
```

### 3. Type Mapper

```csharp
// TypeMapping/ITypeMapper.cs
namespace Jaunty.Scaffold.TypeMapping;

public interface ITypeMapper
{
    string MapToClrType(ColumnSchema column);
    string GetDataReaderMethod(string clrType);
}

// TypeMapping/SqliteTypeMapper.cs
public sealed class SqliteTypeMapper : ITypeMapper
{
    public string MapToClrType(ColumnSchema column)
    {
        // SQLite type affinity rules:
        // https://www.sqlite.org/datatype3.html
        var sqlType = column.DataType.ToUpperInvariant();

        var baseType = sqlType switch
        {
            // INTEGER affinity
            var t when t.Contains("INT") => "int",
            var t when t.Contains("TINYINT") => "byte",
            var t when t.Contains("SMALLINT") => "short",
            var t when t.Contains("MEDIUMINT") => "int",
            var t when t.Contains("BIGINT") => "long",
            "INTEGER" => column.IsPrimaryKey ? "int" : "long",

            // TEXT affinity
            var t when t.Contains("CHAR") => "string",
            var t when t.Contains("CLOB") => "string",
            var t when t.Contains("TEXT") => "string",
            "VARCHAR" => "string",
            "NVARCHAR" => "string",

            // REAL affinity
            var t when t.Contains("REAL") => "double",
            var t when t.Contains("DOUBLE") => "double",
            var t when t.Contains("FLOAT") => "double",

            // NUMERIC affinity
            var t when t.Contains("DECIMAL") => "decimal",
            var t when t.Contains("NUMERIC") => "decimal",
            "BOOLEAN" => "bool",
            "DATE" => "DateTime",
            "DATETIME" => "DateTime",

            // BLOB affinity
            "BLOB" => "byte[]",
            var t when t.Contains("BLOB") => "byte[]",

            // Fallback
            _ => "object"
        };

        // Handle nullability
        if (column.IsNullable && baseType != "string" && baseType != "byte[]" && baseType != "object")
        {
            return baseType + "?";
        }

        return baseType;
    }

    public string GetDataReaderMethod(string clrType)
    {
        // Strip nullable suffix
        var baseType = clrType.TrimEnd('?');

        return baseType switch
        {
            "int" => "GetInt32",
            "long" => "GetInt64",
            "short" => "GetInt16",
            "byte" => "GetByte",
            "bool" => "GetBoolean",
            "decimal" => "GetDecimal",
            "double" => "GetDouble",
            "float" => "GetFloat",
            "string" => "GetString",
            "DateTime" => "GetDateTime",
            "Guid" => "GetGuid",
            "byte[]" => "GetValue",  // Cast to byte[]
            _ => "GetValue"
        };
    }
}
```

### 4. Name Converter

```csharp
// Naming/NamingStyle.cs
namespace Jaunty.Scaffold.Naming;

public enum NamingStyle
{
    Unknown,
    SnakeCase,       // product_name
    ScreamingSnake,  // PRODUCT_NAME
    CamelCase,       // productName
    PascalCase,      // ProductName
    KebabCase        // product-name (rare in DBs)
}

// Naming/NameConverter.cs
public static class NameConverter
{
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle snake_case and SCREAMING_SNAKE_CASE
        if (name.Contains('_'))
        {
            return string.Concat(
                name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Capitalize));
        }

        // Handle already PascalCase or camelCase
        if (char.IsLower(name[0]))
        {
            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        // Already PascalCase or ALLCAPS
        if (name.All(c => char.IsUpper(c) || char.IsDigit(c)))
        {
            // ALLCAPS → Allcaps
            return Capitalize(name);
        }

        return name;
    }

    public static string ToClassName(string tableName, ClassNaming naming)
    {
        var pascal = ToPascalCase(tableName);

        return naming switch
        {
            ClassNaming.Singularize => Singularize(pascal),
            ClassNaming.Pluralize => Pluralize(pascal),
            ClassNaming.Preserve => pascal,
            _ => pascal
        };
    }

    /// <summary>
    /// Convert plural to singular: Products → Product, Categories → Category
    /// </summary>
    public static string Singularize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        // Common irregular plurals
        var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["People"] = "Person",
            ["Men"] = "Man",
            ["Women"] = "Woman",
            ["Children"] = "Child",
            ["Mice"] = "Mouse",
            ["Geese"] = "Goose",
            ["Teeth"] = "Tooth",
            ["Feet"] = "Foot",
            ["Data"] = "Data",        // Unchanged (mass noun)
            ["Media"] = "Media",      // Unchanged (mass noun)
            ["Information"] = "Information",
            ["Equipment"] = "Equipment",
        };

        if (irregulars.TryGetValue(word, out var irregular))
            return irregular;

        // Words ending in -ies → -y (Categories → Category, Companies → Company)
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^3] + "y";

        // Words ending in -ves → -f or -fe (Wives → Wife, Knives → Knife, Leaves → Leaf)
        if (word.EndsWith("ves", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
        {
            var stem = word[..^3];
            // Check common -fe words
            if (stem.EndsWith("wi", StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("kni", StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("li", StringComparison.OrdinalIgnoreCase))
                return stem + "fe";
            return stem + "f";  // Leaves → Leaf
        }

        // Words ending in -oes → -o (Heroes → Hero, Potatoes → Potato)
        if (word.EndsWith("oes", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^2];

        // Words ending in -xes, -ches, -shes, -sses, -zes → remove -es
        if ((word.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("zes", StringComparison.OrdinalIgnoreCase)) && word.Length > 2)
            return word[..^2];

        // Words ending in -es (but not -ses, -xes, etc.) → remove -s
        // Addresses → Addresse is wrong, so be careful
        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
        {
            var beforeEs = word[^3];
            // If consonant + es, likely just remove s: Types → Type
            if (!"sxzh".Contains(char.ToLowerInvariant(beforeEs)))
                return word[..^1];
        }

        // Standard -s ending (Products → Product, Orders → Order)
        if (word.EndsWith('s') && !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && word.Length > 1)
            return word[..^1];

        // Already singular or uncountable
        return word;
    }

    /// <summary>
    /// Convert singular to plural: Product → Products, Category → Categories
    /// </summary>
    public static string Pluralize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        // Common irregular plurals
        var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Person"] = "People",
            ["Man"] = "Men",
            ["Woman"] = "Women",
            ["Child"] = "Children",
            ["Mouse"] = "Mice",
            ["Goose"] = "Geese",
            ["Tooth"] = "Teeth",
            ["Foot"] = "Feet",
            ["Data"] = "Data",
            ["Media"] = "Media",
            ["Information"] = "Information",
            ["Equipment"] = "Equipment",
        };

        if (irregulars.TryGetValue(word, out var irregular))
            return irregular;

        // Words ending in consonant + y → -ies (Category → Categories)
        if (word.EndsWith('y') && word.Length > 1 &&
            !"aeiou".Contains(char.ToLowerInvariant(word[^2])))
            return word[..^1] + "ies";

        // Words ending in -s, -x, -z, -ch, -sh → -es
        if (word.EndsWith('s') || word.EndsWith('x') || word.EndsWith('z') ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            return word + "es";

        // Words ending in -f or -fe → -ves (Leaf → Leaves, but not Roof → Rooves)
        // This is tricky - only some words follow this rule
        // For safety, just add -s for MVP

        // Standard: add -s
        return word + "s";
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }
}
```

### 5. Entity Generator

```csharp
// CodeGen/EntityGenerator.cs
namespace Jaunty.Scaffold.CodeGen;

public sealed class EntityGenerator
{
    private readonly ITypeMapper _typeMapper;

    public EntityGenerator(ITypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }

    public string Generate(TableSchema table, GeneratorOptions options)
    {
        var className = NameConverter.ToClassName(table.Name);
        var pkColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
        var pkType = pkColumn != null ? _typeMapper.MapToClrType(pkColumn).TrimEnd('?') : "int";
        var sb = new StringBuilder();

        // File header - usings depend on attribute style and options
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Data;");

        if (options.AttributeStyle == AttributeStyle.Jaunty)
        {
            sb.AppendLine("using Jaunty.Attributes;");
            if (options.GenerateMapped || options.GenerateEntity)
                sb.AppendLine("using Jaunty.Interfaces;");
        }
        else // DataAnnotations
        {
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            if (options.GenerateMapped || options.GenerateEntity)
                sb.AppendLine("using Jaunty.Interfaces;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {options.Namespace};");
        sb.AppendLine();

        // Table attribute (if name differs from class name)
        if (!table.Name.Equals(className, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"[Table(\"{table.Name}\")]");
        }

        // Class declaration with optional interfaces
        var interfaces = new List<string>();
        if (options.GenerateEntity && pkColumn != null)
            interfaces.Add($"IEntity<{pkType}>");
        if (options.GenerateMapped)
            interfaces.Add($"IMapped<{className}>");

        var inheritance = interfaces.Count > 0 ? $" : {string.Join(", ", interfaces)}" : "";
        sb.AppendLine($"public class {className}{inheritance}");
        sb.AppendLine("{");

        // IEntity<T>.Id property (if generating entity and PK name differs from "Id")
        if (options.GenerateEntity && pkColumn != null)
        {
            var pkPropertyName = NameConverter.ToPascalCase(pkColumn.Name);
            if (!pkPropertyName.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                var ignoreAttr = options.AttributeStyle == AttributeStyle.Jaunty
                    ? "[Ignore]"
                    : "[NotMapped]";
                sb.AppendLine($"    {ignoreAttr}");
                sb.AppendLine($"    public {pkType} Id");
                sb.AppendLine("    {");
                sb.AppendLine($"        get => {pkPropertyName};");
                sb.AppendLine($"        set => {pkPropertyName} = value;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        // Properties
        foreach (var column in table.Columns)
        {
            GenerateProperty(sb, column, options);
        }

        // IMapped<T>.ReadEntity() method
        if (options.GenerateMapped)
        {
            GenerateReadEntityMethod(sb, table, className, options);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateProperty(
        StringBuilder sb,
        ColumnSchema column,
        GeneratorOptions options)
    {
        var propertyName = NameConverter.ToPascalCase(column.Name);
        var clrType = _typeMapper.MapToClrType(column);

        // Key attribute
        if (column.IsPrimaryKey)
        {
            sb.AppendLine("    [Key]");
        }

        // DatabaseGenerated attribute for auto-increment
        if (column.IsAutoIncrement)
        {
            sb.AppendLine("    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
        }

        // Column attribute (if name differs)
        if (!column.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"    [Column(\"{column.Name}\")]");
        }

        // Property declaration
        var defaultValue = GetDefaultValue(clrType, options);
        sb.AppendLine($"    public {clrType} {propertyName} {{ get; set; }}{defaultValue}");
        sb.AppendLine();
    }

    private void GenerateReadEntityMethod(
        StringBuilder sb,
        TableSchema table,
        string className,
        GeneratorOptions options)
    {
        sb.AppendLine($"    public static {className} ReadEntity(IDataReader reader)");
        sb.AppendLine("    {");
        sb.AppendLine("        var ordinal = new OrdinalCache(reader);");
        sb.AppendLine();
        sb.AppendLine("        return new()");
        sb.AppendLine("        {");

        foreach (var column in table.Columns)
        {
            var propertyName = NameConverter.ToPascalCase(column.Name);
            var clrType = _typeMapper.MapToClrType(column);
            var readerMethod = _typeMapper.GetDataReaderMethod(clrType);
            var isNullable = clrType.EndsWith("?") || clrType == "string" || clrType == "byte[]";

            if (isNullable && clrType != "string" && clrType != "byte[]")
            {
                // Nullable value type: int?, decimal?, etc.
                sb.AppendLine($"            {propertyName} = reader.IsDBNull(ordinal[\"{column.Name}\"]) ? null : reader.{readerMethod}(ordinal[\"{column.Name}\"]),");
            }
            else if (clrType == "string")
            {
                // String: could be null
                sb.AppendLine($"            {propertyName} = reader.IsDBNull(ordinal[\"{column.Name}\"]) ? null : reader.{readerMethod}(ordinal[\"{column.Name}\"]),");
            }
            else
            {
                // Non-nullable value type
                sb.AppendLine($"            {propertyName} = reader.{readerMethod}(ordinal[\"{column.Name}\"]),");
            }
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // OrdinalCache helper struct
        sb.AppendLine("    private readonly struct OrdinalCache(IDataReader reader)");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly Dictionary<string, int> _cache = [];");
        sb.AppendLine();
        sb.AppendLine("        public int this[string columnName] =>");
        sb.AppendLine("            _cache.TryGetValue(columnName, out var ordinal)");
        sb.AppendLine("                ? ordinal");
        sb.AppendLine("                : _cache[columnName] = reader.GetOrdinal(columnName);");
        sb.AppendLine("    }");
    }

    private static string GetDefaultValue(string clrType, GeneratorOptions options)
    {
        if (clrType == "string")
        {
            return options.NullableReferenceTypes ? "" : " = null!;";
        }
        return "";
    }
}

// CodeGen/GeneratorOptions.cs
public sealed class GeneratorOptions
{
    public required string Namespace { get; init; }
    public required string OutputDirectory { get; init; }
    public bool NullableReferenceTypes { get; init; } = true;
    public bool GenerateMapped { get; init; } = false;
    public bool GenerateEntity { get; init; } = false;
    public bool OneFilePerEntity { get; init; } = true;
    public AttributeStyle AttributeStyle { get; init; } = AttributeStyle.Jaunty;
    public ClassNaming ClassNaming { get; init; } = ClassNaming.Singularize;
}

public enum AttributeStyle
{
    Jaunty,          // Jaunty.Attributes
    DataAnnotations  // System.ComponentModel.DataAnnotations
}

public enum ClassNaming
{
    Singularize,     // Products → Product (default)
    Pluralize,       // Product → Products
    Preserve         // Keep as-is: Products → Products
}
```

### 6. CLI Entry Point

```csharp
// Program.cs
using System.CommandLine;
using Jaunty.Scaffold.Commands;

var rootCommand = new RootCommand("Jaunty database scaffolding tool");
rootCommand.AddCommand(new ScaffoldCommand());

return await rootCommand.InvokeAsync(args);

// Commands/ScaffoldCommand.cs
namespace Jaunty.Scaffold.Commands;

public sealed class ScaffoldCommand : Command
{
    public ScaffoldCommand() : base("scaffold", "Generate entity classes from database schema")
    {
        var connectionOption = new Option<string>(
            aliases: ["--connection", "-c"],
            description: "Database connection string")
        { IsRequired = true };

        var outputOption = new Option<DirectoryInfo>(
            aliases: ["--output", "-o"],
            description: "Output directory for generated files")
        { IsRequired = true };

        var namespaceOption = new Option<string>(
            aliases: ["--namespace", "-n"],
            description: "Namespace for generated classes",
            getDefaultValue: () => "Entities");

        var tablesOption = new Option<string[]>(
            aliases: ["--tables", "-t"],
            description: "Specific tables to scaffold (comma-separated)")
        { AllowMultipleArgumentsPerToken = true };

        var excludeViewsOption = new Option<bool>(
            "--exclude-views",
            description: "Exclude views from scaffolding");

        // === NEW OPTIONS ===

        var attributeStyleOption = new Option<AttributeStyle>(
            "--attributes",
            description: "Attribute style to use for annotations",
            getDefaultValue: () => AttributeStyle.Jaunty);

        var generateMappedOption = new Option<bool>(
            "--generate-mapped",
            description: "Generate IMapped<T> implementation with ReadEntity() method");

        var generateEntityOption = new Option<bool>(
            "--generate-entity",
            description: "Generate IEntity<TKey> implementation with Id property");

        var classNamingOption = new Option<ClassNaming>(
            "--class-naming",
            description: "How to derive class names from table names",
            getDefaultValue: () => ClassNaming.Singularize);

        AddOption(connectionOption);
        AddOption(outputOption);
        AddOption(namespaceOption);
        AddOption(tablesOption);
        AddOption(excludeViewsOption);
        AddOption(attributeStyleOption);
        AddOption(generateMappedOption);
        AddOption(generateEntityOption);
        AddOption(classNamingOption);

        this.SetHandler(ExecuteAsync,
            connectionOption, outputOption, namespaceOption,
            tablesOption, excludeViewsOption,
            attributeStyleOption, generateMappedOption, generateEntityOption);
    }

    // Enum for attribute style choice
    public enum AttributeStyle
    {
        Jaunty,          // Jaunty.Attributes ([Table], [Column], [Key], [Ignore])
        DataAnnotations  // System.ComponentModel.DataAnnotations ([Table], [Column], [Key], [NotMapped])
    }

    // Enum for class naming strategy
    public enum ClassNaming
    {
        Singularize,     // Products → Product, Categories → Category (default)
        Pluralize,       // Product → Products (rare, but supported)
        Preserve         // Keep table name as-is: Products → Products
    }

    private async Task ExecuteAsync(
        string connection,
        DirectoryInfo output,
        string ns,
        string[]? tables,
        bool excludeViews)
    {
        Console.WriteLine($"Scaffolding from: {MaskConnectionString(connection)}");
        Console.WriteLine($"Output directory: {output.FullName}");
        Console.WriteLine($"Namespace: {ns}");

        // Read schema
        var schemaReader = new SqliteSchemaReader();
        var schema = await schemaReader.ReadSchemaAsync(connection);

        Console.WriteLine($"Found {schema.Tables.Count} tables, {schema.Views.Count} views");

        // Filter tables if specified
        var tablesToGenerate = schema.Tables.AsEnumerable();
        if (tables?.Length > 0)
        {
            var tableSet = tables.ToHashSet(StringComparer.OrdinalIgnoreCase);
            tablesToGenerate = tablesToGenerate.Where(t => tableSet.Contains(t.Name));
        }

        // Add views unless excluded
        if (!excludeViews)
        {
            tablesToGenerate = tablesToGenerate.Concat(schema.Views);
        }

        // Generate entities
        var generator = new EntityGenerator(new SqliteTypeMapper());
        var options = new GeneratorOptions
        {
            Namespace = ns,
            OutputDirectory = output.FullName,
            NullableReferenceTypes = true
        };

        output.Create();

        foreach (var table in tablesToGenerate)
        {
            var code = generator.Generate(table, options);
            var className = NameConverter.ToClassName(table.Name);
            var filePath = Path.Combine(output.FullName, $"{className}.cs");

            await File.WriteAllTextAsync(filePath, code);
            Console.WriteLine($"  Generated: {className}.cs");
        }

        Console.WriteLine("Scaffolding complete!");
    }

    private static string MaskConnectionString(string conn)
    {
        // Basic masking for passwords
        return System.Text.RegularExpressions.Regex.Replace(
            conn,
            @"(Password|Pwd)=[^;]+",
            "$1=***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
```

---

## Project File

```xml
<!-- src/Jaunty.Scaffold/Jaunty.Scaffold.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>jaunty</ToolCommandName>
    <PackageId>Jaunty.Scaffold</PackageId>
    <Version>0.1.0</Version>
    <Description>Database scaffolding tool for Jaunty micro-ORM</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Jaunty\Jaunty.csproj" />
  </ItemGroup>

</Project>
```

---

## Test Plan

### Unit Tests

```csharp
// tests/Jaunty.Scaffold.Tests/Naming/NameConverterTests.cs
public class NameConverterTests
{
    [Theory]
    [InlineData("product_name", "ProductName")]
    [InlineData("PRODUCT_NAME", "ProductName")]
    [InlineData("productName", "ProductName")]
    [InlineData("ProductName", "ProductName")]
    [InlineData("product_id", "ProductId")]
    [InlineData("id", "Id")]
    [InlineData("XML", "Xml")]
    public void ToPascalCase_ConvertsCorrectly(string input, string expected)
    {
        var result = NameConverter.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Products", "Product")]
    [InlineData("Categories", "Category")]
    [InlineData("Companies", "Company")]
    [InlineData("order_details", "OrderDetail")]
    [InlineData("Addresses", "Address")]
    [InlineData("Boxes", "Box")]
    [InlineData("Matches", "Match")]
    [InlineData("Bushes", "Bush")]
    [InlineData("Classes", "Class")]
    [InlineData("Heroes", "Hero")]
    [InlineData("People", "Person")]       // Irregular
    [InlineData("Children", "Child")]      // Irregular
    [InlineData("Data", "Data")]           // Mass noun - unchanged
    [InlineData("Order", "Order")]         // Already singular
    public void Singularize_ConvertsCorrectly(string input, string expected)
    {
        var result = NameConverter.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Product", "Products")]
    [InlineData("Category", "Categories")]
    [InlineData("Company", "Companies")]
    [InlineData("Box", "Boxes")]
    [InlineData("Match", "Matches")]
    [InlineData("Bush", "Bushes")]
    [InlineData("Class", "Classes")]
    [InlineData("Person", "People")]       // Irregular
    [InlineData("Child", "Children")]      // Irregular
    [InlineData("Data", "Data")]           // Mass noun - unchanged
    public void Pluralize_ConvertsCorrectly(string input, string expected)
    {
        var result = NameConverter.Pluralize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Products", ClassNaming.Singularize, "Product")]
    [InlineData("Products", ClassNaming.Preserve, "Products")]
    [InlineData("Product", ClassNaming.Pluralize, "Products")]
    [InlineData("categories", ClassNaming.Singularize, "Category")]
    public void ToClassName_RespectsNamingOption(string input, ClassNaming naming, string expected)
    {
        var result = NameConverter.ToClassName(input, naming);
        Assert.Equal(expected, result);
    }
}

// tests/Jaunty.Scaffold.Tests/TypeMapping/SqliteTypeMapperTests.cs
public class SqliteTypeMapperTests
{
    private readonly SqliteTypeMapper _mapper = new();

    [Theory]
    [InlineData("INTEGER", false, false, "long")]
    [InlineData("INTEGER", true, false, "int")]   // PK → int
    [InlineData("INTEGER", false, true, "long?")]  // Nullable
    [InlineData("TEXT", false, false, "string")]
    [InlineData("REAL", false, false, "double")]
    [InlineData("REAL", false, true, "double?")]
    [InlineData("BLOB", false, false, "byte[]")]
    [InlineData("BOOLEAN", false, false, "bool")]
    [InlineData("VARCHAR(255)", false, false, "string")]
    public void MapToClrType_MapsCorrectly(
        string sqlType, bool isPk, bool isNullable, string expected)
    {
        var column = new ColumnSchema
        {
            Name = "test",
            DataType = sqlType,
            IsPrimaryKey = isPk,
            IsNullable = isNullable,
            Ordinal = 0
        };

        var result = _mapper.MapToClrType(column);
        Assert.Equal(expected, result);
    }
}
```

### Integration Test (with Northwind)

```csharp
// tests/Jaunty.Scaffold.Tests/Integration/SqliteSchemaReaderTests.cs
public class SqliteSchemaReaderTests : IDisposable
{
    private readonly string _dbPath;

    public SqliteSchemaReaderTests()
    {
        // Copy Northwind.db to temp location
        _dbPath = Path.Combine(Path.GetTempPath(), $"northwind_{Guid.NewGuid()}.db");
        File.Copy("TestData/Northwind.db", _dbPath);
    }

    [Fact]
    public async Task ReadSchema_ReturnsAllTables()
    {
        var reader = new SqliteSchemaReader();
        var schema = await reader.ReadSchemaAsync($"Data Source={_dbPath}");

        Assert.NotEmpty(schema.Tables);
        Assert.Contains(schema.Tables, t => t.Name == "Products");
        Assert.Contains(schema.Tables, t => t.Name == "Categories");
        Assert.Contains(schema.Tables, t => t.Name == "Orders");
    }

    [Fact]
    public async Task ReadSchema_ProductsTable_HasCorrectColumns()
    {
        var reader = new SqliteSchemaReader();
        var schema = await reader.ReadSchemaAsync($"Data Source={_dbPath}");

        var products = schema.Tables.Single(t => t.Name == "Products");

        Assert.Contains(products.Columns, c => c.Name == "ProductID" && c.IsPrimaryKey);
        Assert.Contains(products.Columns, c => c.Name == "ProductName");
        Assert.Contains(products.Columns, c => c.Name == "UnitPrice");
    }

    public void Dispose() => File.Delete(_dbPath);
}
```

---

## Deliverables Checklist

### Core Components
- [ ] `DatabaseSchema`, `TableSchema`, `ColumnSchema` models
- [ ] `ISchemaReader` interface
- [ ] `SqliteSchemaReader` implementation
- [ ] `ITypeMapper` interface
- [ ] `SqliteTypeMapper` implementation
- [ ] `NameConverter` with snake_case → PascalCase
- [ ] `EntityGenerator` for basic class generation
- [ ] CLI with `scaffold` command

### Testing
- [ ] `NameConverterTests` - naming conversion
- [ ] `SqliteTypeMapperTests` - type mapping
- [ ] `SqliteSchemaReaderTests` - schema reading with Northwind
- [ ] `EntityGeneratorTests` - code generation output

### Documentation
- [ ] README.md for Jaunty.Scaffold
- [ ] Usage examples in docs/

---

## Usage After Phase 1

```bash
# Install as .NET tool
dotnet tool install --global Jaunty.Scaffold

# Basic: Generate plain entity classes
jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities

# With IMapped<T> for optimal performance
jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --generate-mapped

# With IEntity<T> for CRUD convenience
jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --generate-entity

# Full-featured: Both interfaces
jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --generate-mapped \
  --generate-entity

# Using DataAnnotations instead of Jaunty.Attributes
jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --attributes DataAnnotations

# Class naming options
jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --class-naming Singularize    # Products → Product (default)

jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --class-naming Preserve       # Products → Products (keep as-is)

jaunty scaffold \
  --connection "Data Source=./Northwind.db" \
  --output ./Entities \
  --namespace MyApp.Entities \
  --class-naming Pluralize      # Product → Products (rare use case)

# Output:
# Scaffolding from: Data Source=./Northwind.db
# Options: Attributes=Jaunty, GenerateMapped=true, GenerateEntity=true
# Found 13 tables, 16 views
#   Generated: Category.cs
#   Generated: Customer.cs
#   Generated: Employee.cs
#   Generated: Order.cs
#   Generated: OrderDetail.cs
#   Generated: Product.cs
#   ...
# Scaffolding complete!
```

---

### Sample Output: Basic (no interfaces)

```csharp
using System;
using Jaunty.Attributes;

namespace MyApp.Entities;

[Table("Products")]
public class Product
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int? SupplierId { get; set; }

    public int? CategoryId { get; set; }

    public string? QuantityPerUnit { get; set; }

    public decimal? UnitPrice { get; set; }

    public short? UnitsInStock { get; set; }

    public short? UnitsOnOrder { get; set; }

    public short? ReorderLevel { get; set; }

    public bool Discontinued { get; set; }
}
```

---

### Sample Output: With `--generate-mapped --generate-entity`

```csharp
using System;
using System.Data;
using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace MyApp.Entities;

[Table("Products")]
public class Product : IEntity<int>, IMapped<Product>
{
    [Ignore]
    public int Id
    {
        get => ProductId;
        set => ProductId = value;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    [Column("supplier_id")]
    public int? SupplierId { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("quantity_per_unit")]
    public string? QuantityPerUnit { get; set; }

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }

    [Column("units_in_stock")]
    public short? UnitsInStock { get; set; }

    [Column("units_on_order")]
    public short? UnitsOnOrder { get; set; }

    [Column("reorder_level")]
    public short? ReorderLevel { get; set; }

    public bool Discontinued { get; set; }

    public static Product ReadEntity(IDataReader reader)
    {
        var ordinal = new OrdinalCache(reader);

        return new()
        {
            ProductId = reader.GetInt32(ordinal["product_id"]),
            ProductName = reader.GetString(ordinal["product_name"]),
            SupplierId = reader.IsDBNull(ordinal["supplier_id"]) ? null : reader.GetInt32(ordinal["supplier_id"]),
            CategoryId = reader.IsDBNull(ordinal["category_id"]) ? null : reader.GetInt16(ordinal["category_id"]),
            QuantityPerUnit = reader.IsDBNull(ordinal["quantity_per_unit"]) ? null : reader.GetString(ordinal["quantity_per_unit"]),
            UnitPrice = reader.IsDBNull(ordinal["unit_price"]) ? null : reader.GetDecimal(ordinal["unit_price"]),
            UnitsInStock = reader.IsDBNull(ordinal["units_in_stock"]) ? null : reader.GetInt16(ordinal["units_in_stock"]),
            UnitsOnOrder = reader.IsDBNull(ordinal["units_on_order"]) ? null : reader.GetInt16(ordinal["units_on_order"]),
            ReorderLevel = reader.IsDBNull(ordinal["reorder_level"]) ? null : reader.GetInt16(ordinal["reorder_level"]),
            Discontinued = reader.GetBoolean(ordinal["discontinued"]),
        };
    }

    private readonly struct OrdinalCache(IDataReader reader)
    {
        private readonly Dictionary<string, int> _cache = [];

        public int this[string columnName] =>
            _cache.TryGetValue(columnName, out var ordinal)
                ? ordinal
                : _cache[columnName] = reader.GetOrdinal(columnName);
    }
}
```

---

### Sample Output: With `--attributes DataAnnotations`

```csharp
using System;
using System.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.Entities;

[Table("Products")]
public class Product
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    [Column("supplier_id")]
    public int? SupplierId { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    // ... etc
}
```

---

## Phase 2 Preview

After Phase 1 is complete:
- Add `SqlServerSchemaReader`
- Add `PostgresSchemaReader`
- Add `--dialect` option to CLI
- Expand type mappings for each dialect

---

## Open Questions for Phase 1

1. **Tool name**: `jaunty scaffold` or separate `jaunty-scaffold` command?
2. **Package name**: `Jaunty.Scaffold` or `Jaunty.Tools`?
3. **Single file output option**: Generate all entities in one file for small DBs?
4. **Column ordering**: Match database ordinal or alphabetical?
