using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Configuration;
using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Providers.PostgreSql;
using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Providers.SQLite;

namespace Jaunty.Scaffolding;

/// <summary>
/// Main entry point for scaffolding entities from a database.
/// </summary>
public sealed class Scaffolder
{
    /// <summary>
    /// Scaffolds entity classes from a database.
    /// </summary>
    /// <param name="options">Scaffolding options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the scaffolding operation.</returns>
    public async Task<ScaffoldResult> ScaffoldAsync(
        ScaffoldOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateOptions(options);

            // Resolve provider
            var provider = options.Provider == DatabaseProvider.AutoDetect
                ? DetectProvider(options.ConnectionString)
                : options.Provider;

            // Get schema reader and type mapper for provider
            var (schemaReader, typeMapper) = GetProviderComponents(provider);

            // Read schema
            var readerOptions = new SchemaReaderOptions
            {
                IncludeTables = options.IncludeTables.Count > 0 ? options.IncludeTables : null,
                ExcludeTables = options.ExcludeTables.Count > 0 ? options.ExcludeTables : null,
                IncludeSchemas = options.IncludeSchemas.Count > 0 ? options.IncludeSchemas : null,
                IncludeForeignKeys = options.IncludeForeignKeys
            };

            var schema = await schemaReader.ReadSchemaAsync(
                options.ConnectionString,
                readerOptions,
                cancellationToken);

            if (schema.Tables.Count == 0)
            {
                return ScaffoldResult.Failed("No tables found matching the specified criteria.");
            }

            // Generate code
            var codeGenerator = new EntityCodeGenerator(typeMapper);
            var codeGenOptions = MapToCodeGenOptions(options);
            var generatedFiles = new List<string>();

            // Create output directory
            if (!options.DryRun)
            {
                Directory.CreateDirectory(options.OutputDirectory);
            }

            foreach (var table in schema.Tables)
            {
                var code = codeGenerator.GenerateEntity(table, codeGenOptions);
                var className = GetClassName(table.TableName, options);
                var filePath = Path.Combine(options.OutputDirectory, $"{className}.cs");

                if (!options.DryRun)
                {
                    // Check if file exists and force is not set
                    if (File.Exists(filePath) && !options.Force)
                    {
                        return ScaffoldResult.Failed(
                            $"File already exists: {filePath}. Use --force to overwrite.");
                    }

                    await File.WriteAllTextAsync(filePath, code, cancellationToken);
                }

                generatedFiles.Add(filePath);
            }

            return ScaffoldResult.Succeeded(generatedFiles);
        }
        catch (Exception ex)
        {
            return ScaffoldResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Lists tables in the database.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="provider">Database provider (or AutoDetect).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of table names with their schemas.</returns>
    public async Task<IReadOnlyList<(string Schema, string Table)>> ListTablesAsync(
        string connectionString,
        DatabaseProvider provider = DatabaseProvider.AutoDetect,
        CancellationToken cancellationToken = default)
    {
        var resolvedProvider = provider == DatabaseProvider.AutoDetect
            ? DetectProvider(connectionString)
            : provider;

        var (schemaReader, _) = GetProviderComponents(resolvedProvider);

        var schema = await schemaReader.ReadSchemaAsync(
            connectionString,
            new SchemaReaderOptions(),
            cancellationToken);

        return schema.Tables
            .Select(t => (t.SchemaName, t.TableName))
            .ToList();
    }

    private static void ValidateOptions(ScaffoldOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("Connection string is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.Namespace))
            throw new ArgumentException("Namespace is required.", nameof(options));
    }

    private static DatabaseProvider DetectProvider(string connectionString)
    {
        var lower = connectionString.ToLowerInvariant();

        // SQLite detection
        if (lower.Contains(".db") || lower.Contains(".sqlite") ||
            (lower.Contains("data source=") && !lower.Contains("initial catalog=")))
            return DatabaseProvider.SQLite;

        // SQL Server detection
        if ((lower.Contains("server=") || lower.Contains("data source=")) &&
            (lower.Contains("initial catalog=") || lower.Contains("database=")) &&
            (lower.Contains("trusted_connection=") || lower.Contains("user id=") || lower.Contains("integrated security=")))
            return DatabaseProvider.SqlServer;

        // PostgreSQL detection
        if (lower.Contains("host=") && lower.Contains("database=") &&
            (lower.Contains("username=") || lower.Contains("user id=")))
            return DatabaseProvider.PostgreSql;

        // MySQL detection (after SQL Server since both can have server= and database=)
        if (lower.Contains("server=") && lower.Contains("database=") &&
            (lower.Contains("uid=") || lower.Contains("user=")))
            return DatabaseProvider.MySql;

        // Default to SQL Server
        return DatabaseProvider.SqlServer;
    }

    private static (ISchemaReader, ITypeMapper) GetProviderComponents(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => (new SqlServerSchemaReader(), new SqlServerTypeMapper()),
            DatabaseProvider.PostgreSql => (new PostgreSqlSchemaReader(), new PostgreSqlTypeMapper()),
            DatabaseProvider.MySql => (new MySqlSchemaReader(), new MySqlTypeMapper()),
            DatabaseProvider.SQLite => (new SQLiteSchemaReader(), new SQLiteTypeMapper()),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };
    }

    private static CodeGeneratorOptions MapToCodeGenOptions(ScaffoldOptions options)
    {
        return new CodeGeneratorOptions
        {
            Namespace = options.Namespace,
            UseFileScopedNamespace = options.UseFileScopedNamespace,
            GenerateTableAttribute = options.GenerateTableAttribute,
            GenerateColumnAttribute = options.GenerateColumnAttribute,
            GenerateKeyAttribute = options.GenerateKeyAttribute,
            GenerateDatabaseGeneratedAttribute = options.GenerateDatabaseGeneratedAttribute,
            UseNullableReferenceTypes = options.UseNullableReferenceTypes,
            Singularize = options.Singularize,
            GeneratePartialClasses = options.GeneratePartialClasses,
            ClassPrefix = options.ClassPrefix,
            ClassSuffix = options.ClassSuffix,
            AddDataAnnotations = options.AddDataAnnotations
        };
    }

    private static string GetClassName(string tableName, ScaffoldOptions options)
    {
        var className = NamingHelper.ToPascalCase(tableName);

        if (options.Singularize)
            className = NamingHelper.Singularize(className);

        if (!string.IsNullOrEmpty(options.ClassPrefix))
            className = options.ClassPrefix + className;

        if (!string.IsNullOrEmpty(options.ClassSuffix))
            className = className + options.ClassSuffix;

        return NamingHelper.EscapeIdentifier(className);
    }
}