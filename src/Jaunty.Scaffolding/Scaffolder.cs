using System.Data.Common;

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
            return ScaffoldResult.Failed(Describe(ex));
        }
    }

    /// <summary>
    /// Flattens an exception chain into one message.
    /// </summary>
    /// <remarks>
    /// AUD-R26: reporting <c>ex.Message</c> alone discards the cause whenever a provider wraps
    /// it, and ADO.NET providers wrap routinely. The reflection wrapper that produced the worst
    /// case is fixed at its source in <see cref="Internals.ReflectedConnectionFactory"/>, but a
    /// nested cause is normal enough - a connection failure whose real reason is a socket error,
    /// for instance - that the top-level message is often the least informative part of the chain.
    /// </remarks>
    private static string Describe(Exception ex)
    {
        var message = ex.Message;

        Exception? inner = ex.InnerException;
        var depth = 0;

        // Bounded: a corrupt or self-referential chain must not produce an unbounded string.
        while (inner is not null && depth < 5)
        {
            if (!string.IsNullOrWhiteSpace(inner.Message) &&
                !message.Contains(inner.Message, StringComparison.Ordinal))
            {
                message = message + " -> " + inner.Message;
            }

            inner = inner.InnerException;
            depth++;
        }

        return message;
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
        // AUD-R32-007 (re-found from round 27): a null options dereferenced inside ScaffoldAsync's
        // try, so the NullReferenceException was swallowed by the catch-all and came back as
        // ScaffoldResult.Failed("Object reference not set to an instance of an object.") -
        // indistinguishable from a database failure. Every other bad argument here throws
        // ArgumentException, which the caller converts to a Failed result with a usable message.
        if (options is null)
            throw new ArgumentNullException(nameof(options), "Scaffold options are required.");

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("Connection string is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.Namespace))
            throw new ArgumentException("Namespace is required.", nameof(options));

        // AUD-R26: the namespace is interpolated straight into the generated file. Checked here,
        // before a connection is opened, so a typo is reported against the option the user set
        // rather than as a compiler error in a file they then have to read backwards.
        if (!NamingHelper.IsValidNamespace(options.Namespace))
            throw new ArgumentException(
                $"Namespace '{options.Namespace}' is not a valid C# namespace. It must be a " +
                "dot-separated sequence of identifiers, each starting with a letter or underscore.",
                nameof(options));
    }

    /// <summary>
    /// Infers the provider from the shape of a connection string.
    /// </summary>
    /// <remarks>
    /// AUD-R26: this used to run <c>Contains</c> over the lowercased connection string as one
    /// flat blob, which meant every heuristic also matched against the **values** - including the
    /// password. Measured: <c>Server=prod;Initial Catalog=Sales;User Id=sa;Password=hunter2.dbx</c>
    /// was detected as **SQLite**, purely because the password contains ".db", and the scaffold
    /// then failed with "unable to open database file" - a message that points nowhere near the
    /// cause. The same string with the password <c>hunter2</c> detected correctly as SqlServer.
    /// A value could equally contain "database=" or "host=" and tip any of the other rules.
    ///
    /// <para>
    /// The rules themselves are unchanged; they are now evaluated against the parsed key set,
    /// with only the data-source and port values ever inspected. <see cref="DbConnectionStringBuilder"/>
    /// lives in System.Data.Common, so parsing costs no provider dependency.
    /// </para>
    /// </remarks>
    internal static DatabaseProvider DetectProvider(string connectionString)
    {
        Dictionary<string, string>? keys = TryParseConnectionString(connectionString);

        // Unparseable: keep the historical fallback rather than guessing. Opening the connection
        // is what will report the malformed string, and now does so with the real message.
        if (keys is null)
            return DatabaseProvider.SqlServer;

        var hasDataSource = keys.ContainsKey("data source") || keys.ContainsKey("datasource") ||
                            keys.ContainsKey("filename");
        var hasDatabase = keys.ContainsKey("database");
        var hasInitialCatalog = keys.ContainsKey("initial catalog");
        var hasServer = keys.ContainsKey("server");
        var hasUserId = keys.ContainsKey("user id") || keys.ContainsKey("userid");

        // SQLite detection. A file extension is only meaningful on the data-source value itself.
        if (TryGetValue(keys, out var dataSource, "data source", "datasource", "filename") &&
            HasSqliteFileExtension(dataSource))
            return DatabaseProvider.SQLite;

        if (hasDataSource && !hasInitialCatalog && !hasDatabase)
            return DatabaseProvider.SQLite;

        // PostgreSQL detection - checked before SQL Server because Npgsql accepts "Server="
        // and "User Id=" as aliases for its canonical "Host="/"Username=" keys, so a valid
        // Npgsql connection string can otherwise satisfy the SQL Server heuristic below.
        // "Host=" is Npgsql's unambiguous canonical key; port 5432 is Postgres's default and
        // not used by SQL Server, so either signal is safe to check ahead of SQL Server without
        // reclassifying genuine "Server=/Database=/User Id=" SQL Server connection strings
        // (which carry neither "host=" nor "port=5432").
        var isPostgresPort = keys.TryGetValue("port", out var port) &&
                             port.Trim().Equals("5432", StringComparison.Ordinal);

        if (hasDatabase && (keys.ContainsKey("username") || hasUserId) &&
            (keys.ContainsKey("host") || isPostgresPort))
            return DatabaseProvider.PostgreSql;

        // SQL Server detection
        if ((hasServer || hasDataSource) &&
            (hasInitialCatalog || hasDatabase) &&
            (keys.ContainsKey("trusted_connection") || hasUserId || keys.ContainsKey("integrated security")))
            return DatabaseProvider.SqlServer;

        // MySQL detection (after SQL Server since both can have server= and database=)
        if (hasServer && hasDatabase && (keys.ContainsKey("uid") || keys.ContainsKey("user")))
            return DatabaseProvider.MySql;

        // Default to SQL Server
        return DatabaseProvider.SqlServer;
    }

    /// <summary>
    /// Parses a connection string into its key/value pairs, with keys lowercased and trimmed.
    /// Returns null when the string is not a well-formed connection string at all.
    /// </summary>
    private static Dictionary<string, string>? TryParseConnectionString(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var keys = new Dictionary<string, string>(builder.Count, StringComparer.OrdinalIgnoreCase);

            foreach (string key in builder.Keys.Cast<string>())
                keys[key.Trim()] = builder[key]?.ToString() ?? string.Empty;

            return keys;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool TryGetValue(Dictionary<string, string> keys, out string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (keys.TryGetValue(candidate, out string? found))
            {
                value = found;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool HasSqliteFileExtension(string dataSource)
    {
        // Trailing SQLite URI query parameters ("file:app.db?mode=ro") are not part of the path.
        var path = dataSource;
        var query = path.IndexOf('?');
        if (query >= 0)
            path = path[..query];

        return path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".db3", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase);
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