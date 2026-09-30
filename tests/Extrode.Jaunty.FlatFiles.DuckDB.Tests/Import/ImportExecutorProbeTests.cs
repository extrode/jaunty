using DuckDB.NET.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

public sealed class ImportExecutorProbeTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_probe_{Guid.NewGuid():N}");
    private readonly DuckDb _inventory;
    private readonly DuckDb _notes;

    [Table("notes")]
    public class NoteRow
    {
        [Key]
        public int Id { get; set; }
        public string? Note { get; set; }
    }

    public ImportExecutorProbeTests()
    {
        Directory.CreateDirectory(_dataDir);
        string inventoryCsv = Path.Combine(_dataDir, "inventory.csv");
        string notesCsv = Path.Combine(_dataDir, "notes.csv");

        using (var gen = new DuckDBConnection("DataSource=:memory:"))
        {
            gen.Open();
            using DuckDBCommand cmd = gen.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'Widget A', 'Electronics', 150, 29.99, true),
                        (2, 'Widget B', 'Electronics', 0, 49.99, false),
                        (3, 'Gadget C', 'Hardware', 75, 15.50, true),
                        (4, 'Gadget D', 'Hardware', 200, 8.25, true),
                        (5, 'Thingamajig', 'Misc', 10, 99.99, true)
                    ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
                ) TO '{inventoryCsv.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        File.WriteAllText(notesCsv, "Id,Note\n1,alpha\n2,\n3,gamma\n");

        var inventoryOptions = new FlatFileOptions();
        inventoryOptions.AddCsv<InventoryItem>(inventoryCsv);
        _inventory = new DuckDb(inventoryOptions);

        var notesOptions = new FlatFileOptions();
        notesOptions.AddCsv<NoteRow>(notesCsv);
        _notes = new DuckDb(notesOptions);
    }

    public void Dispose()
    {
        _inventory.Dispose();
        _notes.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static long Count(SqliteConnection connection)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"inventory\"";
        return (long)cmd.ExecuteScalar()!;
    }

    private static List<(long Id, string? Note)> Notes(SqliteConnection connection)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"Note\" FROM \"notes\" ORDER BY \"Id\"";
        using SqliteDataReader reader = cmd.ExecuteReader();
        var rows = new List<(long, string?)>();
        while (reader.Read())
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        return rows;
    }

    [Fact]
    public async Task ATargetThatIsNotOpen_IsOpenedByTheImport()
    {
        using var target = new ProbeSqliteConnection(canBatch: false);
        Assert.Equal(System.Data.ConnectionState.Closed, target.State);

        long count = await _inventory.ImportIntoAsync<InventoryItem>(target, new ImportOptions(createTableIfMissing: true));

        Assert.Equal(5, count);
        Assert.Equal(System.Data.ConnectionState.Open, target.State);
        Assert.Equal(5, Count(target.Inner));
    }

    [Fact]
    public async Task ABatchingTarget_ReceivesTheRowsInBatchesOfTheBatchSize()
    {
        using var target = new ProbeSqliteConnection(canBatch: true);
        var progress = new List<(long, long?)>();

        long count = await _inventory.ImportIntoAsync<InventoryItem>(
            target,
            new ImportOptions(batchSize: 2, createTableIfMissing: true, onProgress: (done, total) => progress.Add((done, total))));

        Assert.Equal(5, count);
        Assert.Equal([2, 2, 1], target.BatchSizes);
        Assert.Equal([(2L, (long?)null), (4L, null), (5L, null), (5L, 5L)], progress);
        Assert.Equal(5, Count(target.Inner));
    }

    [Fact]
    public async Task ABatchingTarget_BindsEachRowsValuesToNumberedParameters()
    {
        using var target = new ProbeSqliteConnection(canBatch: true);

        await _inventory.ImportIntoAsync<InventoryItem>(target, new ImportOptions(batchSize: 2, createTableIfMissing: true));

        Assert.Equal(5, target.BatchParameterNames.Count);
        Assert.All(target.BatchParameterNames, names => Assert.Equal(["@p0", "@p1", "@p2", "@p3", "@p4", "@p5"], names));
    }

    [Fact]
    public async Task ABatchingTarget_WithFewerRowsThanTheBatchSize_SendsOneBatch()
    {
        using var target = new ProbeSqliteConnection(canBatch: true);
        var progress = new List<(long, long?)>();

        await _inventory.ImportIntoAsync<InventoryItem>(
            target,
            new ImportOptions(batchSize: 5, createTableIfMissing: true, onProgress: (done, total) => progress.Add((done, total))));

        Assert.Equal([5], target.BatchSizes);
        Assert.Equal([(5L, (long?)null), (5L, 5L)], progress);
    }

    [Fact]
    public async Task ANonBatchingTarget_ReportsProgressPerBatchAndPreparesTheInsertOnce()
    {
        using var target = new ProbeSqliteConnection(canBatch: false);
        var progress = new List<(long, long?)>();

        long count = await _inventory.ImportIntoAsync<InventoryItem>(
            target,
            new ImportOptions(batchSize: 2, createTableIfMissing: true, onProgress: (done, total) => progress.Add((done, total))));

        Assert.Equal(5, count);
        Assert.Empty(target.BatchSizes);
        Assert.Equal([(2L, (long?)null), (4L, null), (5L, 5L)], progress);
        Assert.Equal(1, target.PrepareCalls);
        Assert.Equal(5, Count(target.Inner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ANullSourceValue_IsImportedAsNull(bool canBatch)
    {
        using var target = new ProbeSqliteConnection(canBatch);

        long count = await _notes.ImportIntoAsync<NoteRow>(target, new ImportOptions(createTableIfMissing: true));

        Assert.Equal(3, count);
        Assert.Equal([(1L, "alpha"), (2L, null), (3L, "gamma")], Notes(target.Inner));
    }

    [Fact]
    public async Task ATargetThatCannotBeReadBackAfterCreatingTheTable_SaysSo()
    {
        using var target = new ProbeSqliteConnection(canBatch: false)
        {
            FailReaderWhen = text => text.EndsWith("WHERE 0=1", StringComparison.Ordinal),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _inventory.ImportIntoAsync<InventoryItem>(target, new ImportOptions(createTableIfMissing: true)).AsTask());

        Assert.IsType<SqliteException>(ex.InnerException);
        Assert.Equal(
            "Target table 'inventory' could not be read back after CreateTableIfMissing created it: " +
            ex.InnerException!.Message +
            " See the inner exception - the CREATE TABLE may have failed, or the table may exist but be unreadable by this connection.",
            ex.Message);
        Assert.Contains(target.ExecutedTexts, text => text.StartsWith("CREATE TABLE", StringComparison.Ordinal));
    }
}
