using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-075. Duplicate column names in the result set bound <b>last-wins</b> on the DuckDb read
/// path, while core's <c>Query&lt;T&gt;</c> disambiguates them.
/// <c>columnOrdinals[reader.GetName(i)] = i</c> overwrote on a repeated name, so for
/// <c>SELECT a.*, b.*</c> over two tables that both have <c>Id</c>, an <c>Id</c> property was filled
/// from the <em>rightmost</em> <c>Id</c> column with no diagnostic. Core's
/// <c>QueryCoreListDirect</c> routes the same situation through
/// <c>DuplicateColumnNames.Disambiguate</c>, which keeps the first occurrence under its bare name
/// and suffixes later ones <c>_N</c> so no value is lost - so the same entity type and the same SQL
/// mapped differently depending on which API read it.
/// <para>
/// Distinct from the <c>ColumnMappingCache</c> last-wins collapse (AUD-R26), which is two
/// <em>properties</em> claiming one column name; this is two <em>columns</em> claiming one name.
/// </para>
/// </summary>
public class DuplicateResultColumnTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public DuplicateResultColumnTests()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(Path.Combine(DataDir, "csv", "sales.csv"));
        _db = new DuckDb(options);
    }

    public void Dispose() => _db.Dispose();

    private class Row
    {
        public int Id { get; set; }
        public string? Label { get; set; }
    }

    private class SuffixedRow
    {
        public int Id { get; set; }

        [Column("Id_2")]
        public int SecondId { get; set; }
    }

    [Fact]
    public void ARepeatedColumnName_BindsTheFirstOccurrence_NotTheLast()
    {
        List<Row> rows = _db.Query<Row>("SELECT 1 AS Id, 'x' AS Label, 2 AS Id");

        // Before the fix this was 2 - the rightmost column silently won.
        Assert.Equal(1, Assert.Single(rows).Id);
    }

    [Fact]
    public void ARepeatedColumnName_IsCaseInsensitive()
    {
        List<Row> rows = _db.Query<Row>("SELECT 1 AS Id, 'x' AS Label, 2 AS ID");

        Assert.Equal(1, Assert.Single(rows).Id);
    }

    [Fact]
    public void TheLaterOccurrence_IsReachableUnderItsSuffixedName()
    {
        // No value is lost: the second Id is addressable as Id_2, exactly as on the core path.
        List<SuffixedRow> rows = _db.Query<SuffixedRow>("SELECT 1 AS Id, 2 AS Id");

        SuffixedRow row = Assert.Single(rows);
        Assert.Equal(1, row.Id);
        Assert.Equal(2, row.SecondId);
    }

    [Fact]
    public async Task TheAsyncReadPath_BindsTheFirstOccurrenceToo()
    {
        List<Row> rows = await _db.QueryAsync<Row>("SELECT 1 AS Id, 'x' AS Label, 2 AS Id");

        Assert.Equal(1, Assert.Single(rows).Id);
    }

    [Fact]
    public async Task TheAsyncReadPath_ExposesTheSuffixedNameToo()
    {
        List<SuffixedRow> rows = await _db.QueryAsync<SuffixedRow>("SELECT 1 AS Id, 2 AS Id");

        SuffixedRow row = Assert.Single(rows);
        Assert.Equal(1, row.Id);
        Assert.Equal(2, row.SecondId);
    }

    // ------------------------------------------------------------------
    // Controls: the ordinary, unique-named case is untouched.
    // ------------------------------------------------------------------

    [Fact]
    public void DistinctColumnNames_StillBindNormally()
    {
        Row row = Assert.Single(_db.Query<Row>("SELECT 7 AS Id, 'seven' AS Label"));

        Assert.Equal(7, row.Id);
        Assert.Equal("seven", row.Label);
    }

    [Fact]
    public void AnUnmatchedProperty_IsStillLeftAlone()
    {
        Row row = Assert.Single(_db.Query<Row>("SELECT 7 AS Id"));

        Assert.Equal(7, row.Id);
        Assert.Null(row.Label);
    }
}
