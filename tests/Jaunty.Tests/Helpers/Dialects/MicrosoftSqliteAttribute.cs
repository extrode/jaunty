using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Helpers.Dialects;

public sealed class MicrosoftSqliteAttribute : DialectDataAttributeBase
{
    private static readonly bool _isAvailable = ProbeAvailability();

    public MicrosoftSqliteAttribute()
        : base("Microsoft.Data.Sqlite provider/native library is not available in this runtime.")
    {
        ApplySkipIfUnavailable();
    }

    protected override bool IsAvailable => _isAvailable;

    protected override DialectInfo Dialect => DialectInfo.MicrosoftSqlite;

    private static bool ProbeAvailability()
    {
        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
