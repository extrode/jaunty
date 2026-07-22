using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Configuration;
using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Providers.PostgreSql;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Schema;

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
            DatabaseProvider provider = options.Provider == DatabaseProvider.AutoDetect
                ? DetectProvider(options.ConnectionString)
                : options.Provider;

            // Get schema reader and type mapper for provider
            (ISchemaReader? schemaReader, ITypeMapper? typeMapper) = GetProviderComponents(provider);

            // Read schema
            var readerOptions = new SchemaReaderOptions
            {
                IncludeTables = options.IncludeTables.Count > 0 ? options.IncludeTables : null,
                ExcludeTables = options.ExcludeTables.Count > 0 ? options.ExcludeTables : null,
                IncludeSchemas = options.IncludeSchemas.Count > 0 ? options.IncludeSchemas : null,
                IncludeForeignKeys = options.IncludeForeignKeys
            };

            DatabaseSchema schema = await schemaReader.ReadSchemaAsync(
                options.ConnectionString,
                readerOptions,
                cancellationToken).ConfigureAwait(false);

            if (schema.Tables.Count == 0)
            {
                return ScaffoldResult.Failed("No tables found matching the specified criteria.");
            }

            // Detect table-name collisions up front (e.g. "Product" and "Products" both
            // singularizing to "Product") before writing anything, rather than either
            // silently overwriting the first file (--force) or failing mid-run (File.Exists).
            var tablesByClassName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (TableSchema table in schema.Tables)
            {
                var className = GetClassName(table.TableName, options);
                if (!tablesByClassName.TryGetValue(className, out List<string>? tableNames))
                {
                    tableNames = [];
                    tablesByClassName[className] = tableNames;
                }
                tableNames.Add(table.TableName);
            }

            List<string> collisions = tablesByClassName
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => $"{kvp.Key} <- [{string.Join(", ", kvp.Value)}]")
                .ToList();

            if (collisions.Count > 0)
            {
                return ScaffoldResult.Failed(
                    "Multiple tables map to the same generated class name: " +
                    string.Join("; ", collisions) +
                    ". Use ClassPrefix/ClassSuffix, disable Singularize, or exclude one of the tables.");
            }

            // Generate code
            var codeGenerator = new EntityCodeGenerator(typeMapper);
            CodeGeneratorOptions codeGenOptions = MapToCodeGenOptions(options);
            var generatedFiles = new List<string>();

            // Detect pre-existing output files up front (like the class-name-collision check
            // above) so a collision discovered partway through a multi-table run can't leave
            // earlier tables' files written to disk while later ones fail.
            if (!options.DryRun && !options.Force)
            {
                List<string> existingFiles = schema.Tables
                    .Select(table => Path.Combine(options.OutputDirectory, $"{GetClassName(table.TableName, options)}.cs"))
                    .Where(File.Exists)
                    .ToList();

                if (existingFiles.Count > 0)
                {
                    return ScaffoldResult.Failed(
                        "File(s) already exists: " + string.Join(", ", existingFiles) +
                        ". Use --force to overwrite.");
                }
            }

            // Create output directory
            if (!options.DryRun)
            {
                Directory.CreateDirectory(options.OutputDirectory);
            }

            foreach (TableSchema table in schema.Tables)
            {
                var code = codeGenerator.GenerateEntity(table, codeGenOptions);
                var className = GetClassName(table.TableName, options);
                var filePath = Path.Combine(options.OutputDirectory, $"{className}.cs");

                if (!options.DryRun)
                {
                    await File.WriteAllTextAsync(filePath, code, cancellationToken).ConfigureAwait(false);
                }

                generatedFiles.Add(filePath);
            }

            return ScaffoldResult.Succeeded(generatedFiles);
        }
        // AUD-R22: let OperationCanceledException (e.g. from a cancelled cancellationToken)
        // propagate instead of being reported as an ordinary ScaffoldResult.Failed - matches
        // ListTablesAsync, which has no catch and lets cancellation propagate normally, so a
        // caller can distinguish "cancelled" from "the database read failed" consistently
        // across both public APIs.
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        DatabaseProvider resolvedProvider = provider == DatabaseProvider.AutoDetect
            ? DetectProvider(connectionString)
            : provider;

        (ISchemaReader? schemaReader, ITypeMapper _) = GetProviderComponents(resolvedProvider);

        DatabaseSchema schema = await schemaReader.ReadSchemaAsync(
            connectionString,
            new SchemaReaderOptions(),
            cancellationToken).ConfigureAwait(false);

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

    internal static DatabaseProvider DetectProvider(string connectionString)
    {
        var lower = connectionString.ToLowerInvariant();

        // SQLite detection
        if (lower.Contains(".db") || lower.Contains(".sqlite") ||
            (lower.Contains("data source=") && !lower.Contains("initial catalog=") && !lower.Contains("database=")))
            return DatabaseProvider.SQLite;

        // PostgreSQL detection - checked before SQL Server because Npgsql accepts "Server="
        // and "User Id=" as aliases for its canonical "Host="/"Username=" keys, so a valid
        // Npgsql connection string can otherwise satisfy the SQL Server heuristic below.
        // "Host=" is Npgsql's unambiguous canonical key; port 5432 is Postgres's default and
        // not used by SQL Server, so either signal is safe to check ahead of SQL Server without
        // reclassifying genuine "Server=/Database=/User Id=" SQL Server connection strings
        // (which carry neither "host=" nor "port=5432").
        if (lower.Contains("database=") && (lower.Contains("username=") || lower.Contains("user id=")) &&
            (lower.Contains("host=") || lower.Contains("port=5432")))
            return DatabaseProvider.PostgreSql;

        // SQL Server detection
        if ((lower.Contains("server=") || lower.Contains("data source=")) &&
            (lower.Contains("initial catalog=") || lower.Contains("database=")) &&
            (lower.Contains("trusted_connection=") || lower.Contains("user id=") || lower.Contains("integrated security=")))
            return DatabaseProvider.SqlServer;

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

    private static string GetClassName(string tableName, ScaffoldOptions options) =>
        NamingHelper.ToClassName(tableName, options.Singularize, options.ClassPrefix, options.ClassSuffix);
}