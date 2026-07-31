using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

public class DateTimeOffsetReadTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_dto_read_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public DateTimeOffsetReadTests()
    {
        Directory.CreateDirectory(_dataDir);
        var csvPath = Path.Combine(_dataDir, "offsets.csv");
        File.WriteAllText(csvPath,
            "Id,OccurredAt\n" +
            "1,2024-01-15 14:30:00\n" +
            "2,2023-06-30 09:05:42\n");

        var options = new FlatFileOptions();
        options.AddCsv<OffsetRecord>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    [Fact]
    public void Select_DateTimeOffsetProperty_Materializes()
    {
        var results = _db.Connection.From<OffsetRecord>().Select().ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(new DateTime(2024, 1, 15, 14, 30, 0), results.Single(r => r.Id == 1).OccurredAt.DateTime);
        Assert.Equal(new DateTime(2023, 6, 30, 9, 5, 42), results.Single(r => r.Id == 2).OccurredAt.DateTime);
    }

    [Fact]
    public void Where_DateTimeOffsetRow_FiltersById()
    {
        var results = _db.Connection.From<OffsetRecord>().Where(r => r.Id == 2).Select().ToList();

        Assert.Single(results);
        Assert.Equal(new DateTime(2023, 6, 30, 9, 5, 42), results[0].OccurredAt.DateTime);
    }
}
