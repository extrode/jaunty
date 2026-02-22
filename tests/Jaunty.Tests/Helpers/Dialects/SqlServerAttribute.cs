namespace Jaunty.Tests.Helpers.Dialects;

public sealed class SqlServerAttribute : DialectDataAttributeBase
{
    public SqlServerAttribute()
        : base("SQL Server not configured. Set JAUNTY_TEST_SQLSERVER or ConnectionStrings:SqlServer.")
    {
        ApplySkipIfUnavailable();
    }

    protected override bool IsAvailable => TestConfiguration.HasSqlServer;

    protected override DialectInfo Dialect => DialectInfo.SqlServer;
}
