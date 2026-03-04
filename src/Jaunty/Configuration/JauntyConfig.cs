namespace Jaunty.Configuration;

using System.Data;
using System.Data.Common;

/// <summary>
/// Provides global configuration options for Jaunty's entity-to-database mapping behavior.
/// </summary>
public static class JauntyConfig
{
    private static Func<Type, string>? _schemaNameResolver;
    private static Func<Type, string>? _tableNameResolver;
    private static Func<string, string>? _columnNameResolver;
    private static Action<string, object?>? _logger;
    private static Func<Type, global::Jaunty.Internals.Enums.MappingMode, object>? _reflectionMapperResolver;
    private static Func<Type, IDataReader, object>? _specialTypeMapperResolver;
    private static Func<Type, Action<IDbCommand, object>>? _reflectionInsertBinderResolver;
    private static Func<Type, Action<IDbCommand, object>>? _reflectionUpdateBinderResolver;
    private static Func<Type, Action<IDbCommand, object>>? _reflectionDeleteBinderResolver;

    private static int _parameterParsingCapacity = 8;
    private static int _queryResultCapacity = 64;
    private static int _csvFieldCapacity = 16;

    /// <summary>
    /// Gets or sets the initial capacity for parameter name lists when parsing SQL.
    /// Default: 8. Most queries have fewer than 8 parameters.
    /// </summary>
    public static int ParameterParsingCapacity
    {
        get => _parameterParsingCapacity;
        set => _parameterParsingCapacity = value > 0 ? value : 8;
    }

    /// <summary>
    /// Gets or sets the initial capacity for query result lists.
    /// Default: 64. Suitable for most queries without immediate reallocation.
    /// </summary>
    public static int QueryResultCapacity
    {
        get => _queryResultCapacity;
        set => _queryResultCapacity = value > 0 ? value : 64;
    }

    /// <summary>
    /// Gets or sets the initial capacity for CSV field lists when parsing CSV rows.
    /// Default: 16. Typical CSV files have fewer than 16 columns.
    /// </summary>
    public static int CsvFieldCapacity
    {
        get => _csvFieldCapacity;
        set => _csvFieldCapacity = value > 0 ? value : 16;
    }

    /// <summary>
    /// Gets or sets a custom resolver for schema names.
    /// </summary>
    public static Func<Type, string>? SchemaNameResolver
    {
        get => _schemaNameResolver;
        set => _schemaNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a custom resolver for table names.
    /// </summary>
    public static Func<Type, string>? TableNameResolver
    {
        get => _tableNameResolver;
        set => _tableNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a custom resolver for column names.
    /// </summary>
    public static Func<string, string>? ColumnNameResolver
    {
        get => _columnNameResolver;
        set => _columnNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a global diagnostic logger for SQL commands.
    /// </summary>
    public static Action<string, object?>? Logger
    {
        get => _logger;
        set => _logger = value;
    }

    /// <summary>
    /// Optional fallback mapper resolver for types that are not source-generated.
    /// Typically provided by Jaunty.Extensions.Reflection.
    /// Returns a Func&lt;IDataReader, T&gt; or Func&lt;DbDataReader, T&gt; cast to object.
    /// </summary>
    public static Func<Type, global::Jaunty.Internals.Enums.MappingMode, object>? ReflectionMapperResolver
    {
        get => _reflectionMapperResolver;
        set => _reflectionMapperResolver = value;
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for INSERT operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver
    {
        get => _reflectionInsertBinderResolver;
        set => _reflectionInsertBinderResolver = value;
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for UPDATE operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionUpdateBinderResolver
    {
        get => _reflectionUpdateBinderResolver;
        set => _reflectionUpdateBinderResolver = value;
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for DELETE operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionDeleteBinderResolver
    {
        get => _reflectionDeleteBinderResolver;
        set => _reflectionDeleteBinderResolver = value;
    }

    /// <summary>
    /// Optional fallback table metadata resolver for types that are not source-generated.
    /// Returns an EntityMetadata object.
    /// </summary>
    public static Func<Type, object>? ReflectionTableMetadataResolver { get; set; }

    /// <summary>
    /// Optional fallback multi-mapper resolver for types that are not source-generated.
    /// Returns a MultiEntityMapper object.
    /// </summary>
    public static Func<Type, Type, object>? ReflectionMultiMapperResolver { get; set; }

    /// <summary>
    /// Optional resolver for special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject).
    /// Typically provided by Jaunty.Extensions.Reflection.SpecialTypeMappers.Register().
    /// Returns a Func&lt;IDataReader, object&gt; that creates the special type instance.
    /// </summary>
    public static Func<Type, IDataReader, object>? SpecialTypeMapperResolver
    {
        get => _specialTypeMapperResolver;
        set => _specialTypeMapperResolver = value;
    }

    /// <summary>
    /// Resets all configuration options to their default values.
    /// </summary>
    public static void Reset()
    {
        _schemaNameResolver = null;
        _tableNameResolver = null;
        _columnNameResolver = null;
        _logger = null;
        _reflectionMapperResolver = null;
        _specialTypeMapperResolver = null;
        _reflectionInsertBinderResolver = null;
        _reflectionUpdateBinderResolver = null;
        _reflectionDeleteBinderResolver = null;
        ReflectionTableMetadataResolver = null;
        ReflectionMultiMapperResolver = null;
        _parameterParsingCapacity = 8;
        _queryResultCapacity = 64;
        _csvFieldCapacity = 16;
    }
}
