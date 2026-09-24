using DuckDB.NET.Data;

using Extrode.Jaunty.Attributes;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// <c>ImportExecutor</c>'s two schema-alignment failures: a mapped column the source file lacks, and
/// a target table that cannot be read with <c>CreateTableIfMissing</c> off.
/// </summary>
public class ImportExecutorFailureTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_fail_{Guid.NewGuid():N}");
    private readonly string _csvPath;

    public ImportExecutorFailureTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csvPath = Path.Combine(_dataDir, "items.csv");

        using var gen = new DuckDBConnection("DataSource=:memory:");
        gen.Open();
        using DuckDBCommand cmd = gen.CreateCommand();
        cmd.CommandText = $@"
            COPY (SELECT * FROM (VALUES (1, 'a'), (2, 'b')) AS t(""ItemId"", ""ItemName""))
            TO '{_csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private DuckDb Open<T>() where T : class, new()
    {
        var options = new FlatFileOptions();
        options.AddCsv<T>(_csvPath);
        return new DuckDb(options);
    }

    private static SqliteConnection Sqlite()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        return conn;
    }

    [Fact]
    public async Task AMappedColumnMissingFromTheSource_ListsTheSourceColumns()
    {
        using DuckDb db = Open<ItemWithColour>();
        using SqliteConnection target = Sqlite();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.ImportIntoAsync<ItemWithColour>(target, new ImportOptions(createTableIfMissing: true)).AsTask());

        Assert.Equal(
            "Schema alignment failed: Source does not contain column 'Colour' required by entity mapping. " +
            "Available columns: ItemId, ItemName",
            ex.Message);
    }

    [Fact]
    public async Task AMissingTargetTable_WithoutCreateTableIfMissing_ExplainsTheLikelyCause()
    {
        using DuckDb db = Open<Item>();
        using SqliteConnection target = Sqlite();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.ImportIntoAsync<Item>(target).AsTask());

        Assert.StartsWith("Could not read the schema of target table 'items': ", ex.Message);
        Assert.EndsWith(
            " The most likely cause is that the table does not exist and CreateTableIfMissing " +
            "is false - set CreateTableIfMissing = true to auto-create it, or create it " +
            "manually before importing. If the table does exist, see the inner exception: " +
            "a permission denial, a broken connection or an unquotable table name reaches " +
            "this handler the same way.",
            ex.Message);
        Assert.IsType<SqliteException>(ex.InnerException);
    }

    [Table("items")]
    public class Item
    {
        [Key] public int ItemId { get; set; }
        public string ItemName { get; set; } = "";
    }

    [Table("items")]
    public class ItemWithColour
    {
        [Key] public int ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public string Colour { get; set; } = "";
    }
}
