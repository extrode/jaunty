namespace Jaunty.Tests.Helpers.Dialects;

public sealed class PostgresAttribute : DialectDataAttributeBase
{
    public PostgresAttribute()
        : base("PostgreSQL not configured. Set JAUNTY_TEST_POSTGRESQL or ConnectionStrings:PostgreSql.")
    {
        ApplySkipIfUnavailable();
    }

    protected override bool IsAvailable => TestConfiguration.HasPostgreSql;

    protected override bool IsReachable => DialectReachability.IsPostgreSqlReachable;

    protected override string? UnreachableReason => DialectReachability.PostgreSqlError;

    protected override string RequireVariable => DialectReachability.RequirePostgreSql;

    protected override DialectInfo Dialect => DialectInfo.Postgres;
}
