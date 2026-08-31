using System.Collections.Concurrent;
using System.Reflection;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Core;

/// <summary>
/// R27 batch 13 (medium). EnsureExtensionsLoaded marked an extension as loaded before running
/// INSTALL/LOAD, so a failed install (offline machine, blocked repository) left the entry in
/// place and every later registration for the same scheme skipped the install, failing much
/// later with DuckDB's opaque "extension not loaded" error. A failure now rolls the mark back.
/// The install is forced to fail deterministically by closing the connection first.
/// </summary>
public class ExtensionLoadFailureTests
{
    private static ConcurrentDictionary<string, bool> LoadedExtensions(DuckDb db)
    {
        FieldInfo field = typeof(DuckDb).GetField("_loadedExtensions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (ConcurrentDictionary<string, bool>)field.GetValue(db)!;
    }

    private static IFileSource RemoteSource()
    {
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>("https://example.invalid/inventory.csv");
        return options.Sources[0];
    }

    [Fact]
    public void FailedInstall_DoesNotStayMarkedLoaded()
    {
        using var db = new DuckDb(new FlatFileOptions());
        db.Connection.Close();

        Assert.ThrowsAny<Exception>(() => db.RegisterSource(RemoteSource()));

        Assert.False(LoadedExtensions(db).ContainsKey("httpfs"));
    }

    [Fact]
    public async Task FailedInstall_Async_DoesNotStayMarkedLoaded()
    {
        using var db = new DuckDb(new FlatFileOptions());
        db.Connection.Close();

        await Assert.ThrowsAnyAsync<Exception>(() => db.RegisterSourceAsync(RemoteSource()).AsTask());

        Assert.False(LoadedExtensions(db).ContainsKey("httpfs"));
    }
}
