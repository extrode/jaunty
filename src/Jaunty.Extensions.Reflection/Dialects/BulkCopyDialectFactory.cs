using Jaunty.Dialects;

namespace Jaunty.Extensions.Reflection.Dialects;

/// <summary>
/// Factory that provides dialects with bulk copy support.
/// Intercepts dialect creation and returns enhanced versions with bulk copy providers.
/// </summary>
internal static class BulkCopyDialectFactory
{
    private static bool _enabled = false;

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

        return baseDialect switch
        {
            SqlServerDialect => new SqlServerDialectWithBulkCopy(),
            PostgreSqlDialect => new PostgreSqlDialectWithBulkCopy(),
            MySqlDialect => new MySqlDialectWithBulkCopy(),
            SQLiteDialect => new SQLiteDialectWithBulkCopy(),
            _ => baseDialect
        };
    }
}
