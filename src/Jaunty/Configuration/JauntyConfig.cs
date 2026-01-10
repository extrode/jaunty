namespace Jaunty.Configuration;

public static class JauntyConfig
{
    private static Func<Type, string>? _schemaNameResolver;
    private static Func<Type, string>? _tableNameResolver;
    private static Func<string, string>? _columnNameResolver;

    /// <summary>
    /// Custom resolver for schema names. Receives entity type, returns schema name.
    /// Return null to fall back to default behavior.
    /// </summary>
    public static Func<Type, string>? SchemaNameResolver
    {
        get => _schemaNameResolver;
        set => _schemaNameResolver = value;
    }

    /// <summary>
    /// Custom resolver for table names. Receives entity type, returns table name.
    /// Return null to fall back to default behavior.
    /// </summary>
    public static Func<Type, string>? TableNameResolver
    {
        get => _tableNameResolver;
        set => _tableNameResolver = value;
    }

    /// <summary>
    /// Custom resolver for column names. Receives property name, returns column name.
    /// Return null to fall back to default behavior.
    /// </summary>
    public static Func<string, string>? ColumnNameResolver
    {
        get => _columnNameResolver;
        set => _columnNameResolver = value;
    }

    /// <summary>
    /// Resets all configuration to defaults.
    /// </summary>
    public static void Reset()
    {
        _schemaNameResolver = null;
        _tableNameResolver = null;
        _columnNameResolver = null;
    }
}
