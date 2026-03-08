#if NET8_0_OR_GREATER
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
#else
namespace Jaunty.Tests.Helpers.Dialects;

/// <summary>
/// Placeholder for MicrosoftSqliteAttribute on .NET Framework.
/// Returns no test data to completely exclude tests from discovery.
/// </summary>
public sealed class MicrosoftSqliteAttribute : DialectDataAttributeBase
{
    public MicrosoftSqliteAttribute()
        : base(string.Empty)
    {
    }

    protected override bool IsAvailable => false;

    protected override DialectInfo Dialect => DialectInfo.MicrosoftSqlite;

    public override System.Collections.Generic.IEnumerable<object[]> GetData(System.Reflection.MethodInfo testMethod)
    {
        // Return no data on .NET Framework to exclude tests from discovery entirely
        return System.Linq.Enumerable.Empty<object[]>();
    }
}
#endif