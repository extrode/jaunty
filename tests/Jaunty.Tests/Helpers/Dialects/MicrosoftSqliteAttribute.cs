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

    public override ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>> GetData(
        System.Reflection.MethodInfo testMethod,
        Xunit.Sdk.DisposalTracker disposalTracker)
    {
        // xunit.v3 fails a theory when a data attribute yields zero rows,
        // so return a single skipped row on .NET Framework instead of none.
        return new ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>>(
            new Xunit.ITheoryDataRow[]
            {
                new Xunit.TheoryDataRow<DialectInfo>(DialectInfo.MicrosoftSqlite)
                {
                    Skip = "Microsoft.Data.Sqlite is not exercised on .NET Framework."
                }
            });
    }
}
#endif