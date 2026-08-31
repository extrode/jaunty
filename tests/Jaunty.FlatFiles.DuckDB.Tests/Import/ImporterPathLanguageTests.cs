using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

using Jaunty.FlatFiles.Import;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// AUD-R35-245. <c>FlatFileImporter.ImportAsync&lt;T&gt;(string, ...)</c> resolved its path with
/// <c>Path.GetFullPath</c> and required <c>File.Exists</c>, so a glob pattern or a remote URL threw
/// <c>FileNotFoundException</c> - while <c>FlatFile.Open(string)</c>, the sibling single-path entry
/// point over the same pipeline, accepts both. Both now share <c>FlatFile.ResolvePath</c>.
/// </summary>
public class ImporterPathLanguageTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_importer_paths_{Guid.NewGuid():N}");

    public ImporterPathLanguageTests()
    {
        Directory.CreateDirectory(_dataDir);
        WriteCsv("inventory_a.csv", 1, 2);
        WriteCsv("inventory_b.csv", 3, 4);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private void WriteCsv(string name, int firstId, int secondId)
    {
        string path = Path.Combine(_dataDir, name).Replace("\\", "/").Replace("'", "''");

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    ({firstId}, 'Widget {firstId}', 'Electronics', 10, 1.50, true),
                    ({secondId}, 'Widget {secondId}', 'Electronics', 20, 2.50, false)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{path}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();
    }

    private static SqliteConnection OpenTarget()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static long CountRows(SqliteConnection connection)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM inventory";
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task AGlobPattern_ImportsEveryMatchingFile()
    {
        using SqliteConnection target = OpenTarget();

        long imported = await FlatFileImporter.ImportAsync<InventoryItem>(
            Path.Combine(_dataDir, "inventory_*.csv"),
            target,
            new ImportOptions(createTableIfMissing: true));

        Assert.Equal(4, imported);
        Assert.Equal(4, CountRows(target));
    }

    [Fact]
    public async Task APlainLocalPath_StillImports()
    {
        using SqliteConnection target = OpenTarget();

        long imported = await FlatFileImporter.ImportAsync<InventoryItem>(
            Path.Combine(_dataDir, "inventory_a.csv"),
            target,
            new ImportOptions(createTableIfMissing: true));

        Assert.Equal(2, imported);
        Assert.Equal(2, CountRows(target));
    }

    [Fact]
    public async Task AMissingLocalFile_StillThrowsFileNotFound()
    {
        using SqliteConnection target = OpenTarget();

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await FlatFileImporter.ImportAsync<InventoryItem>(
                Path.Combine(_dataDir, "absent.csv"),
                target));
    }

    [Fact]
    public async Task ADisallowedUriScheme_ThrowsArgumentExceptionNamingTheParameter()
    {
        using SqliteConnection target = OpenTarget();

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await FlatFileImporter.ImportAsync<InventoryItem>("ftp://example.com/data.csv", target));

        Assert.Equal("filePath", ex.ParamName);
        Assert.Contains("ftp://", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAllowedUriScheme_GetsPastPathResolution()
    {
        using SqliteConnection target = OpenTarget();

        // No network call is made here: an https source reaches DuckDB, which fails to fetch it.
        // What matters is that it is no longer rejected up front as a missing local file - the
        // defect was that this threw FileNotFoundException naming a bogus absolute path.
        Exception ex = await Record.ExceptionAsync(async () =>
            await FlatFileImporter.ImportAsync<InventoryItem>(
                "https://example.invalid/inventory.csv",
                target,
                new ImportOptions(createTableIfMissing: true)));

        Assert.NotNull(ex);
        Assert.IsNotType<FileNotFoundException>(ex);
    }
}
