namespace Jaunty.Tests.Helpers.Dialects;

public sealed class SystemSqliteAttribute : DialectDataAttributeBase
{
    public SystemSqliteAttribute()
        : base("System.Data.SQLite provider is not available.")
    {
        ApplySkipIfUnavailable();
    }

    protected override bool IsAvailable => true;

    protected override DialectInfo Dialect => DialectInfo.SystemSqlite;
}