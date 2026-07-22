using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Core;

/// <summary>
/// AUD-R18 batch-7: covers the AutoOpen/Sources interaction in the <see cref="DuckDb(FlatFileOptions)"/>
/// constructor - no prior test exercised <c>AutoOpen = false</c> combined with a non-empty
/// <c>Sources</c> list, so it was unverified whether source registration (which runs a command on
/// the connection) works, throws, or silently no-ops when the connection was never opened.
/// </summary>
public class DuckDbConstructorTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    [Fact]
    public void Constructor_AutoOpenFalse_WithSources_ThrowsInvalidOperationException()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions { AutoOpen = false };
        options.AddCsv<SalesRecord>(csvPath);

        // Source registration issues a command against the (never-opened) connection - DuckDB.NET
        // does not implicitly open it, so this surfaces as a low-level ADO.NET exception rather
        // than a Jaunty-specific, actionable error message.
        var ex = Assert.Throws<InvalidOperationException>(() => new DuckDb(options));
        Assert.Contains("open connection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_AutoOpenFalse_NoSources_LeavesConnectionClosed()
    {
        var options = new FlatFileOptions { AutoOpen = false };

        using var db = new DuckDb(options);

        Assert.Equal(System.Data.ConnectionState.Closed, db.Connection.State);
    }
}
