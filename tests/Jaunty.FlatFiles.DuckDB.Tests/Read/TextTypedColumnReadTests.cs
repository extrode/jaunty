using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-027, end to end. A CSV carries every column as text unless DuckDB's sniffer recognises a
/// shape it has a type for, so a <see cref="Guid"/>, <see cref="TimeSpan"/> or <see cref="char"/>
/// property arrives at the converter as a string; the two timestamp-backed columns arrive as
/// <see cref="DateTime"/> for <see cref="DateOnly"/> and <see cref="TimeOnly"/> targets.
///
/// <para>
/// The fluent read path maps through core's <c>DbValueConversion</c> rather than through
/// <c>ReaderValueConverter</c> - which is the point. Both had the same gap, independently, and
/// neither the unit tests for one nor the unit tests for the other would have caught the row
/// failing to materialise here. <c>ReaderValueConverterTextTypesTests</c> covers the flat-file
/// converter directly.
/// </para>
/// </summary>
public class TextTypedColumnReadTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_text_typed_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public TextTypedColumnReadTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "records.csv");
        File.WriteAllText(csvPath,
            "Id,Reference,Elapsed,StartedOn,StartedAt,Grade\n" +
            "1,{6F9619FF-8B86-D011-B42D-00C04FC964FF},1.02:30:15,2026-08-02 13:45:30,2026-08-02 13:45:30,A\n" +
            "2,{00000000-0000-0000-0000-000000000000},00:00:05,2020-01-31 23:59:59,2020-01-31 23:59:59,B\n");

        var options = new FlatFileOptions();
        options.AddCsv<TextTypedRecord>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EveryTextBackedTypeMaterialises()
    {
        TextTypedRecord row = _db.Connection.From<TextTypedRecord>().Where(r => r.Id == 1).Select().Single();

        Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), row.Reference);
        Assert.Equal(new TimeSpan(1, 2, 30, 15), row.Elapsed);
        Assert.Equal('A', row.Grade);
    }

    [Fact]
    public void TimestampBackedDateOnlyAndTimeOnlyMaterialise()
    {
        TextTypedRecord row = _db.Connection.From<TextTypedRecord>().Where(r => r.Id == 2).Select().Single();

        Assert.Equal(new DateOnly(2020, 1, 31), row.StartedOn);
        Assert.Equal(new TimeOnly(23, 59, 59), row.StartedAt);
    }

    [Fact]
    public void EveryRowMaterialises()
    {
        List<TextTypedRecord> rows = _db.Connection.From<TextTypedRecord>().Select().ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), rows.Single(r => r.Id == 2).Elapsed);
        Assert.Equal(new DateOnly(2026, 8, 2), rows.Single(r => r.Id == 1).StartedOn);
    }
}
