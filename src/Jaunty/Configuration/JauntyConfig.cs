namespace Jaunty.Configuration;

/// <summary>
/// Provides global configuration options for Jaunty's entity-to-database mapping behavior.
/// </summary>
/// <remarks>
/// <para>
/// This static class allows you to customize how Jaunty resolves schema names, table names, 
/// and column names for entity classes. By default, Jaunty uses conventional naming patterns, 
/// but you can override this behavior by setting the provided resolver functions.
/// </para>
/// <para>
/// <strong>Configuration Options:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><see cref="SchemaNameResolver"/> - Custom logic for determining database schema names from entity types</description></item>
/// <item><description><see cref="TableNameResolver"/> - Custom logic for determining table names from entity types</description></item>
/// <item><description><see cref="ColumnNameResolver"/> - Custom logic for determining column names from property names</description></item>
/// </list>
/// <para>
/// <strong>Thread Safety:</strong> Configuration should be set during application startup before 
/// any database operations occur. The resolvers are read during query generation and should be 
/// thread-safe if modified at runtime.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Configuration;
/// 
/// // Configure table name resolver to use pluralized names
/// JauntyConfig.TableNameResolver = type =>
/// {
///     // Simple pluralization: add 's' to the type name
///     return type.Name + "s";
/// };
/// 
/// // Configure schema name resolver to use a specific schema per namespace
/// JauntyConfig.SchemaNameResolver = type =>
/// {
///     if (type.Namespace?.EndsWith(".Admin") == true)
///         return "admin";
///     if (type.Namespace?.EndsWith(".Catalog") == true)
///         return "catalog";
///     return "dbo"; // Default schema
/// };
/// 
/// // Configure column name resolver to use snake_case
/// JauntyConfig.ColumnNameResolver = propertyName =>
/// {
///     // Convert PascalCase to snake_case
///     return string.Concat(
///         propertyName.Select((c, i) => 
///             i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString())
///     ).TrimStart('_');
/// };
/// 
/// // Reset all configuration to defaults
/// JauntyConfig.Reset();
/// </code>
/// </example>
public static class JauntyConfig
{
    private static Func<Type, string>? _schemaNameResolver;
    private static Func<Type, string>? _tableNameResolver;
    private static Func<string, string>? _columnNameResolver;
    private static Action<string, object?>? _logger;

    /// <summary>
    /// Gets or sets a custom resolver for schema names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This function receives an entity <see cref="Type"/> and should return the corresponding 
    /// database schema name. Return <see langword="null"/> to fall back to Jaunty's default behavior.
    /// </para>
    /// <para>
    /// The default behavior uses the <c>TableAttribute.Schema</c> if present, otherwise no schema 
    /// is used (database default).
    /// </para>
    /// </remarks>
    /// <value>
    /// A function that takes an entity type and returns a schema name, or <see langword="null"/> 
    /// to use the default behavior.
    /// </value>
    /// <example>
    /// <code>
    /// // Use schema based on namespace
    /// JauntyConfig.SchemaNameResolver = type =>
    /// {
    ///     if (type.Namespace?.EndsWith(".Admin") == true)
    ///         return "admin";
    ///     return "dbo";
    /// };
    /// </code>
    /// </example>
    public static Func<Type, string>? SchemaNameResolver
    {
        get => _schemaNameResolver;
        set => _schemaNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a custom resolver for table names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This function receives an entity <see cref="Type"/> and should return the corresponding 
    /// database table name. Return <see langword="null"/> to fall back to Jaunty's default behavior.
    /// </para>
    /// <para>
    /// The default behavior uses the <c>TableAttribute.Name</c> if present, otherwise the 
    /// class name is used as the table name.
    /// </para>
    /// </remarks>
    /// <value>
    /// A function that takes an entity type and returns a table name, or <see langword="null"/> 
    /// to use the default behavior.
    /// </value>
    /// <example>
    /// <code>
    /// // Use pluralized table names
    /// JauntyConfig.TableNameResolver = type => type.Name + "s";
    /// 
    /// // Use prefix for all tables
    /// JauntyConfig.TableNameResolver = type => "tbl_" + type.Name;
    /// </code>
    /// </example>
    public static Func<Type, string>? TableNameResolver
    {
        get => _tableNameResolver;
        set => _tableNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a custom resolver for column names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This function receives a property name and should return the corresponding 
    /// database column name. Return <see langword="null"/> to fall back to Jaunty's default behavior.
    /// </para>
    /// <para>
    /// The default behavior uses the <c>ColumnAttribute.Name</c> if present, otherwise the 
    /// property name is used as the column name.
    /// </para>
    /// </remarks>
    /// <value>
    /// A function that takes a property name and returns a column name, or <see langword="null"/> 
    /// to use the default behavior.
    /// </value>
    /// <example>
    /// <code>
    /// // Convert PascalCase to snake_case
    /// JauntyConfig.ColumnNameResolver = propertyName =>
    /// {
    ///     return string.Concat(
    ///         propertyName.Select((c, i) => 
    ///             i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString())
    ///     ).TrimStart('_');
    /// };
    /// 
    /// // Add prefix to all columns
    /// JauntyConfig.ColumnNameResolver = name => "col_" + name;
    /// </code>
    /// </example>
    public static Func<string, string>? ColumnNameResolver
    {
        get => _columnNameResolver;
        set => _columnNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a global diagnostic logger for SQL commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This action is invoked every time a SQL command is executed by Jaunty. 
    /// It can be used for debugging, performance monitoring, or audit logging.
    /// </para>
    /// <para>
    /// The first parameter is the SQL string (potentially with expanded collection parameters), 
    /// and the second parameter is the original parameters object passed to the method.
    /// </para>
    /// </remarks>
    /// <value>
    /// An action that receives the SQL string and parameters object, or <see langword="null"/> 
    /// to disable logging.
    /// </value>
    /// <example>
    /// <code>
    /// // Simple console logger
    /// JauntyConfig.Logger = (sql, params) => 
    /// {
    ///     Console.WriteLine($"Executing SQL: {sql}");
    ///     if (params != null) Console.WriteLine($"With parameters: {params}");
    /// };
    /// </code>
    /// </example>
    public static Action<string, object?>? Logger
    {
        get => _logger;
        set => _logger = value;
    }

    /// <summary>
    /// Resets all configuration options to their default values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method clears all custom resolvers, restoring Jaunty's default naming behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Schema names are determined by <c>TableAttribute.Schema</c> or database default</description></item>
    /// <item><description>Table names are determined by <c>TableAttribute.Name</c> or class name</description></item>
    /// <item><description>Column names are determined by <c>ColumnAttribute.Name</c> or property name</description></item>
    /// </list>
    /// <para>
    /// <strong>Note:</strong> This affects all subsequent database operations. Use with caution 
    /// in multi-threaded applications.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Reset to default configuration
    /// JauntyConfig.Reset();
    /// 
    /// // Now Jaunty uses its default naming conventions
    /// var products = connection.Query&lt;Product&gt;("SELECT * FROM Product");
    /// </code>
    /// </example>
    public static void Reset()
    {
        _schemaNameResolver = null;
        _tableNameResolver = null;
        _columnNameResolver = null;
        _logger = null;
    }
}
