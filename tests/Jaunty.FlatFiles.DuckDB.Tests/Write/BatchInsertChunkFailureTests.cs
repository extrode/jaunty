using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// AUD-R35-032 and AUD-R35-033. The chunked batch insert issues one statement per chunk under DuckDB
/// autocommit but set <c>_modified</c> only once, after the whole loop. A throw on a later chunk
/// therefore left the earlier chunks' rows committed with <c>IsModified&lt;T&gt;()</c> still false,
/// so a caller driving Save/Export off that flag silently skipped the write-back. A null element in
/// the batch was a separate hole: a bare NullReferenceException out of the compiled getter, naming
/// neither the row nor the entity type, where the single-entity overloads have always guarded it.
/// </summary>
public class BatchInsertChunkFailureTests : IDisposable
{
    // DuckDbDialect.MaxParametersPerStatement is 32768 and the entity has two mapped properties, so
    // the batch chunks at 16384 rows. One row past that is the smallest input that reaches a second
    // statement, which is where the flag used to be lost.
    private const int RowsPerChunk = 32768 / 2;

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_chunk_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    [Table("chunked")]
    private sealed class ChunkedRow
    {
        public long Id { get; set; }
        // The file column is BIGINT; declaring the property as string lets one row carry a value
        // DuckDB cannot cast, which is what makes a chosen chunk fail.
        public string Amount { get; set; } = "0";
    }

    public BatchInsertChunkFailureTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "chunked.csv");
        File.WriteAllText(csvPath, "Id,Amount\n1,10\n");

        var options = new FlatFileOptions();
        options.AddCsv<ChunkedRow>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static List<ChunkedRow> Rows(int count, int badIndex = -1)
    {
        var rows = new List<ChunkedRow>(count);

        for (int i = 0; i < count; i++)
            rows.Add(new ChunkedRow { Id = i + 100, Amount = i == badIndex ? "not-a-number" : i.ToString() });

        return rows;
    }

    private long RowCount()
    {
        using System.Data.IDbCommand cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chunked";
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void AFailureOnTheSecondChunkStillReportsTheFirstChunkAsModified()
    {
        List<ChunkedRow> rows = Rows(RowsPerChunk + 1, badIndex: RowsPerChunk);

        Assert.ThrowsAny<Exception>(() => _db.Insert<ChunkedRow>(rows));

        Assert.True(_db.IsModified<ChunkedRow>(), "the first chunk committed but IsModified reported false");
        Assert.Equal(RowsPerChunk + 1, RowCount());
    }

    [Fact]
    public async Task AFailureOnTheSecondChunkStillReportsTheFirstChunkAsModified_Async()
    {
        List<ChunkedRow> rows = Rows(RowsPerChunk + 1, badIndex: RowsPerChunk);

        await Assert.ThrowsAnyAsync<Exception>(async () => await _db.InsertAsync<ChunkedRow>(rows));

        Assert.True(_db.IsModified<ChunkedRow>());
        Assert.Equal(RowsPerChunk + 1, RowCount());
    }

    /// <summary>
    /// The control: a failure on the <em>first</em> chunk commits nothing, so the flag must stay
    /// unset. Marking unconditionally would pass the two tests above and be wrong here.
    /// </summary>
    [Fact]
    public void AFailureOnTheFirstChunkReportsNothingAsModified()
    {
        Assert.ThrowsAny<Exception>(() => _db.Insert<ChunkedRow>(Rows(3, badIndex: 0)));

        Assert.False(_db.IsModified<ChunkedRow>());
        Assert.Equal(1, RowCount());
    }

    [Fact]
    public void ASuccessfulBatchStillReportsModified()
    {
        Assert.Equal(3, _db.Insert<ChunkedRow>(Rows(3)));

        Assert.True(_db.IsModified<ChunkedRow>());
    }

    [Fact]
    public void ANullElementNamesItsIndexAndItsEntityType()
    {
        var rows = new List<ChunkedRow> { new(), null!, new() };

        ArgumentException ex = Assert.Throws<ArgumentException>(() => _db.Insert<ChunkedRow>(rows));

        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ChunkedRow), ex.Message, StringComparison.Ordinal);
        Assert.Equal("entities", ex.ParamName);
    }

    [Fact]
    public async Task ANullElementNamesItsIndexAndItsEntityType_Async()
    {
        var rows = new List<ChunkedRow> { new(), null! };

        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _db.InsertAsync<ChunkedRow>(rows));

        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A null element must be rejected before anything is written, not after the rows ahead of it
    /// have already gone in - the guard sits in the row-building loop, which runs before the
    /// statement is issued.
    /// </summary>
    [Fact]
    public void ANullElementWritesNothing()
    {
        Assert.Throws<ArgumentException>(() => _db.Insert<ChunkedRow>(new List<ChunkedRow> { new(), null! }));

        Assert.Equal(1, RowCount());
    }
}
