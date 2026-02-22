namespace Jaunty.Tests.Helpers.Dialects;

public sealed class MicrosoftSqliteAttribute : DialectDataAttributeBase
{
    public MicrosoftSqliteAttribute()
        : base("Microsoft.Data.Sqlite tests are only available on net8.0 or greater.")
    {
        ApplySkipIfUnavailable();
    }

#if NET8_0_OR_GREATER
    protected override bool IsAvailable => true;
#else
    protected override bool IsAvailable => false;
#endif

    protected override DialectInfo Dialect => DialectInfo.MicrosoftSqlite;
}
