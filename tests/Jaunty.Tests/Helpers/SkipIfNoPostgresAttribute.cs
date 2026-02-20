namespace Jaunty.Tests.Helpers;

/// <summary>
/// Skips test when PostgreSQL connection string is not configured.
/// Configure via environment variable JAUNTY_TEST_POSTGRESQL or appsettings.json.
/// </summary>
public sealed class SkipIfNoPostgresFactAttribute : FactAttribute
{
    public SkipIfNoPostgresFactAttribute()
    {
        if (!TestConfiguration.HasPostgreSql)
        {
            Skip = "PostgreSQL not configured. Set JAUNTY_TEST_POSTGRESQL environment variable or add appsettings.json with ConnectionStrings:PostgreSql.";
        }
    }
}

/// <summary>
/// Skips theory when PostgreSQL connection string is not configured.
/// </summary>
public sealed class SkipIfNoPostgresTheoryAttribute : TheoryAttribute
{
    public SkipIfNoPostgresTheoryAttribute()
    {
        if (!TestConfiguration.HasPostgreSql)
        {
            Skip = "PostgreSQL not configured. Set JAUNTY_TEST_POSTGRESQL environment variable or add appsettings.json with ConnectionStrings:PostgreSql.";
        }
    }
}
