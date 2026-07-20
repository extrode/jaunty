using Jaunty.Dialects;

namespace Jaunty.Extensions.Reflection.Dialects;

/// <summary>
/// Factory that provides dialects with bulk copy support.
/// Intercepts dialect creation and returns enhanced versions with bulk copy providers.
/// </summary>
internal static class BulkCopyDialectFactory
{
    private static volatile bool _enabled = false;

    /// <summary>
    /// Enables bulk copy dialect factory.
    /// Call this from UseNativeBulkCopy().
    /// </summary>
    public static void Enable()
    {
        _enabled = true;
    }

    /// <summary>
    /// Gets a dialect with bulk copy support if enabled.
    /// </summary>
    public static ISqlDialect GetDialect(ISqlDialect baseDialect)
    {
        if (!_enabled)
            return baseDialect;

        // Pass baseDialect through to the wrapper rather than constructing a fresh stock
        // dialect: preserves any state/overrides on the caller's instance (e.g. a subclass)
        // instead of silently discarding it.
        return baseDialect switch
        {
            SqlServerDialect sqlServer => new SqlServerDialectWithBulkCopy(sqlServer),
            PostgreSqlDialect postgres => new PostgreSqlDialectWithBulkCopy(postgres),
            MySqlDialect mysql => new MySqlDialectWithBulkCopy(mysql),
            SQLiteDialect sqlite => new SQLiteDialectWithBulkCopy(sqlite),
            _ => baseDialect
        };
    }
}