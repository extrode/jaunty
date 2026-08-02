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

    /// <summary>
    /// AUD-R32-004: <c>Open()</c> used to run before the try/catch that disposes the connection,
    /// so a throwing Open leaked the native handle - the constructor never returns an instance
    /// the caller could Dispose. A directory path is not a DuckDB database, so Open fails.
    ///
    /// <para>
    /// Honest about what this pins: the throw itself happened before the fix too, and a native
    /// handle's disposal is not observable from managed test code. What it does pin is that the
    /// failure is repeatable and leaves nothing holding the path - if the undisposed connection
    /// ever did retain state, the second construction or the directory delete is where it would
    /// surface. The fix's real evidence is the code shape: Open is now inside the same try that
    /// already existed for source registration, for the same stated reason.
    /// </para>
    /// </summary>
    [Fact]
    public void Constructor_AutoOpenTrue_UnopenableDatabasePath_ThrowsAndDoesNotLeakTheConnection()
    {
        string dirPath = Path.Combine(Path.GetTempPath(), "jaunty-duckdb-r32-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirPath);

        try
        {
            var options = new FlatFileOptions { DatabasePath = dirPath, AutoOpen = true };

            Assert.ThrowsAny<Exception>(() => new DuckDb(options));

            // The failed constructor must not have left a handle on the path: a second attempt
            // fails the same way rather than with a different, lock-shaped error, and the
            // directory is still removable.
            Assert.ThrowsAny<Exception>(() => new DuckDb(new FlatFileOptions { DatabasePath = dirPath, AutoOpen = true }));
        }
        finally
        {
            Directory.Delete(dirPath, recursive: true);
        }
    }

    [Fact]
    public void Constructor_AutoOpenTrue_InMemory_OpensSuccessfully()
    {
        using var db = new DuckDb(new FlatFileOptions { AutoOpen = true });

        Assert.Equal(System.Data.ConnectionState.Open, db.Connection.State);
    }
}
