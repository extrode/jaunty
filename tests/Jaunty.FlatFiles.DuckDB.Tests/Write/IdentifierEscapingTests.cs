using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// Round 10 audit regression: DuckDbWrite/DuckDbUpdate/DuckDbDelete used to interpolate
/// source.TableName directly into SQL (e.g. $"INSERT INTO \"{source.TableName}\" ...")
/// instead of going through DuckDbDialect.EscapeTableName/EscapeColumnName, which double
/// embedded quote characters. A table name containing a literal '"' would previously break
/// out of the quoted identifier and produce invalid SQL.
/// </summary>
public class IdentifierEscapingTests : IDisposable
{
    private readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_quoted_table_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public IdentifierEscapingTests()
    {
        Directory.CreateDirectory(DataDir);
        _csvPath = Path.Combine(DataDir, "quoted.csv");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Alpha', 10),
                    (2, 'Beta', 20)
                ) AS t(""Id"", ""Name"", ""Quantity"")
            ) TO '{_csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<QuotedTableItem>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(DataDir, true); } catch { }
    }

    [Fact]
    public async Task InsertAsync_SingleRow_TableNameWithEmbeddedQuote_Succeeds()
    {
        var affected = await _db.InsertAsync(new QuotedTableItem { Id = 100, Name = "New", Quantity = 5 });
        Assert.Equal(1, affected);

        var results = _db.Connection.From<QuotedTableItem>().Where(i => i.Id == 100).Select();
        Assert.Single(results);
        Assert.Equal("New", results[0].Name);
    }

    [Fact]
    public async Task InsertAsync_Batch_TableNameWithEmbeddedQuote_Succeeds()
    {
        var items = Enumerable.Range(200, 5)
            .Select(i => new QuotedTableItem { Id = i, Name = $"Item_{i}", Quantity = i })
            .ToList();

        var affected = await _db.InsertAsync<QuotedTableItem>(items);
        Assert.Equal(5, affected);

        var count = _db.Connection.From<QuotedTableItem>().Count();
        Assert.Equal(7, count); // 2 seeded + 5 batch
    }

    [Fact]
    public async Task UpdateAsync_TableNameWithEmbeddedQuote_Succeeds()
    {
        var affected = await _db.UpdateAsync<QuotedTableItem>(i => i.Id == 1, i => i.Quantity, 999);
        Assert.Equal(1, affected);

        var results = _db.Connection.From<QuotedTableItem>().Where(i => i.Id == 1).Select();
        Assert.Single(results);
        Assert.Equal(999, results[0].Quantity);
    }

    [Fact]
    public async Task DeleteAsync_TableNameWithEmbeddedQuote_Succeeds()
    {
        var affected = await _db.DeleteAsync<QuotedTableItem>(i => i.Id == 2);
        Assert.Equal(1, affected);

        var count = _db.Connection.From<QuotedTableItem>().Count();
        Assert.Equal(1, count);
    }
}
