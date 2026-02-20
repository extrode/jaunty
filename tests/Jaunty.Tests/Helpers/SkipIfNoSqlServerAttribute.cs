namespace Jaunty.Tests.Helpers;

/// <summary>
/// Skips test when SQL Server connection string is not configured.
/// Configure via environment variable JAUNTY_TEST_SQLSERVER or appsettings.json.
/// </summary>
public sealed class SkipIfNoSqlServerFactAttribute : FactAttribute
{
    public SkipIfNoSqlServerFactAttribute()
    {
        if (!TestConfiguration.HasSqlServer)
        {
            Skip = "SQL Server not configured. Set JAUNTY_TEST_SQLSERVER environment variable or add appsettings.json with ConnectionStrings:SqlServer.";
        }
    }
}

/// <summary>
/// Skips theory when SQL Server connection string is not configured.
/// </summary>
public sealed class SkipIfNoSqlServerTheoryAttribute : TheoryAttribute
{
    public SkipIfNoSqlServerTheoryAttribute()
    {
        if (!TestConfiguration.HasSqlServer)
        {
            Skip = "SQL Server not configured. Set JAUNTY_TEST_SQLSERVER environment variable or add appsettings.json with ConnectionStrings:SqlServer.";
        }
    }
}
