using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R26: the read path converted values with
/// <c>Convert.ChangeType(value, underlyingType, InvariantCulture)</c> inside a bare
/// <c>catch { }</c> whose comment read "let the property setter handle it". The setter is a compiled
/// <c>Expression.Convert</c>, so what it actually did was throw an
/// <see cref="InvalidCastException"/> naming neither the column nor the entity - after the swallow
/// had already discarded the real diagnostic.
///
/// <para>
/// Two categories could not be read at all as a result. <b>Enums</b>, because
/// <c>Convert.ChangeType</c> reaches them from neither representation DuckDB produces (an integer
/// column arrives as <see cref="long"/>, a text column as <see cref="string"/>). And
/// <b><see cref="TimeSpan"/></b>, because DuckDB.NET 1.3.0 returns <see cref="TimeOnly"/> for TIME
/// columns - a quirk <c>ImportExecutor.ConvertValue</c> had always special-cased and the read path
/// never got, so the same value could be imported but not read back.
/// </para>
///
/// <para>
/// Both paths now share <c>ReaderValueConverter</c>, so they cannot drift again.
/// </para>
/// </summary>
public class ReaderValueConverterTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (string file in _tempFiles)
        {
            try { File.Delete(file); } catch (IOException) { }
        }
    }

    public enum Status
    {
        Active = 0,
        Closed = 1,
    }

    public class Row
    {
        public int Id { get; set; }
        public Status Status { get; set; }
    }

    public class TimeRow
    {
        public int Id { get; set; }
        public TimeSpan? Duration { get; set; }
        public DateTime? Opened { get; set; }
    }

    private string WriteCsv(string name, string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"aud_r26_{name}_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static List<T> Query<T>(string csvPath, string table) where T : class, new()
    {
        var options = new FlatFileOptions();
        options.Sources.Add(new CsvFileSource(table, csvPath, typeof(T)) { HasHeader = true });
        using var db = new DuckDb(options);
        return db.Query<T>($"SELECT * FROM \"{table}\"");
    }

    // ------------------------------------------------------------------
    // Enums — both representations
    // ------------------------------------------------------------------

    [Fact]
    public void EnumStoredAsInteger_IsRead()
    {
        string path = WriteCsv("enum_int", "Id,Status\n1,1\n2,0\n");

        List<Row> rows = Query<Row>(path, "enum_int");

        Assert.Equal(Status.Closed, rows[0].Status);
        Assert.Equal(Status.Active, rows[1].Status);
    }

    [Fact]
    public void EnumStoredAsText_IsRead()
    {
        string path = WriteCsv("enum_txt", "Id,Status\n1,Closed\n2,Active\n");

        List<Row> rows = Query<Row>(path, "enum_txt");

        Assert.Equal(Status.Closed, rows[0].Status);
        Assert.Equal(Status.Active, rows[1].Status);
    }

    [Fact]
    public void EnumStoredAsText_IsCaseInsensitive()
    {
        // A flat file is an external artifact; its producer is not this library, so the member name
        // is matched the way every other name comparison in the package is.
        string path = WriteCsv("enum_case", "Id,Status\n1,cLoSeD\n");

        Assert.Equal(Status.Closed, Query<Row>(path, "enum_case")[0].Status);
    }

    [Fact]
    public void EnumWithUnknownName_ReportsTheColumnAndProperty()
    {
        string path = WriteCsv("enum_bad", "Id,Status\n1,Nonsense\n");

        var ex = Assert.Throws<InvalidOperationException>(() => Query<Row>(path, "enum_bad"));

        Assert.Contains("Status", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Row.Status", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnumWithUnmappedNumber_IsReadAsThatNumber()
    {
        // Enum.ToObject accepts any value of the underlying type; .NET's own semantics are that an
        // enum is not a closed set, so rejecting this would be stricter than the language.
        string path = WriteCsv("enum_num", "Id,Status\n1,99\n");

        Assert.Equal(99, (int)Query<Row>(path, "enum_num")[0].Status);
    }

    // ------------------------------------------------------------------
    // DuckDB.NET type quirks the import path already handled
    // ------------------------------------------------------------------

    [Fact]
    public void TimeColumn_IsReadIntoTimeSpan()
    {
        string path = WriteCsv("times", "Id,Duration,Opened\n1,01:30:00,2024-01-05\n");

        List<TimeRow> rows = Query<TimeRow>(path, "times");

        Assert.Equal(new TimeSpan(1, 30, 0), rows[0].Duration);
    }

    [Fact]
    public void DateColumn_IsReadIntoDateTime()
    {
        string path = WriteCsv("dates", "Id,Duration,Opened\n1,01:30:00,2024-01-05\n");

        List<TimeRow> rows = Query<TimeRow>(path, "dates");

        Assert.Equal(new DateTime(2024, 1, 5), rows[0].Opened);
    }

    // ------------------------------------------------------------------
    // The diagnostic
    // ------------------------------------------------------------------

    [Fact]
    public void UnconvertibleValue_NamesColumnPropertyAndBothTypes()
    {
        // Previously: "Unable to cast object of type 'System.DateOnly' to type 'System.Int32'" -
        // no column, no property, no entity.
        string path = WriteCsv("mismatch", "Id,Status\n2024-01-05,1\n");

        var ex = Assert.Throws<InvalidOperationException>(() => Query<Row>(path, "mismatch"));

        Assert.Contains("'Id'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Row.Id", ex.Message, StringComparison.Ordinal);
        Assert.Contains("DateOnly", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Int32", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Conversions that already worked must keep working
    // ------------------------------------------------------------------

    [Fact]
    public void OrdinaryConversions_AreUnaffected()
    {
        string path = WriteCsv("plain", "Id,Status\n42,0\n");

        List<Row> rows = Query<Row>(path, "plain");

        Assert.Equal(42, rows[0].Id);
        Assert.Equal(Status.Active, rows[0].Status);
    }

    [Fact]
    public void NullValue_LeavesThePropertyAtItsDefault()
    {
        string path = WriteCsv("nulls", "Id,Duration,Opened\n1,,\n");

        List<TimeRow> rows = Query<TimeRow>(path, "nulls");

        Assert.Null(rows[0].Duration);
        Assert.Null(rows[0].Opened);
    }
}
