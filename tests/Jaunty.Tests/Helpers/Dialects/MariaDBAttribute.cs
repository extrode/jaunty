namespace Jaunty.Tests.Helpers.Dialects;

public sealed class MariaDBAttribute : DialectDataAttributeBase
{
    public MariaDBAttribute()
        : base("MariaDB/MySQL not configured. Set JAUNTY_TEST_MARIADB or JAUNTY_TEST_MYSQL.")
    {
        ApplySkipIfUnavailable();
    }

    protected override bool IsAvailable => TestConfiguration.HasMariaDb || TestConfiguration.HasMySql;

    protected override DialectInfo Dialect => DialectInfo.MariaDb;
}