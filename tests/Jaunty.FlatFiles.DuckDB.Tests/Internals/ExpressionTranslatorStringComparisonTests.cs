using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R25: ExpressionTranslator's Contains/StartsWith/EndsWith handlers read only
/// <c>Arguments[0]</c> and emitted a bare <c>LIKE ... ESCAPE '\'</c>, discarding the
/// <see cref="StringComparison"/> argument of the two-argument overloads. DuckDB's LIKE is
/// case-sensitive (it follows PostgreSQL), so
/// <c>Where(p =&gt; p.Name.Contains("abc", StringComparison.OrdinalIgnoreCase))</c> compiled, ran,
/// and returned case-sensitively filtered rows. Jaunty.Fluent's WhereExpressionVisitor carried the
/// identical omission and is fixed alongside this.
/// </summary>
public class ExpressionTranslatorStringComparisonTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r25_ilike_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public ExpressionTranslatorStringComparisonTests()
    {
        Directory.CreateDirectory(_dataDir);
        var csvPath = Path.Combine(_dataDir, "inventory.csv");

        using (var generator = new DuckDBConnection("DataSource=:memory:"))
        {
            generator.Open();
            using DuckDBCommand cmd = generator.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'Widget Alpha', 'Electronics', 1, 1.00, true),
                        (2, 'WIDGET BETA',  'Electronics', 2, 2.00, true),
                        (3, 'gadget gamma', 'Hardware',    3, 3.00, true)
                    ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
                ) TO '{csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    [Fact]
    public void Contains_WithOrdinalIgnoreCase_MatchesBothCasings()
    {
        // Before the fix this returned only "Widget Alpha": the uppercase row was filtered out by a
        // case-sensitive LIKE despite the caller explicitly asking for OrdinalIgnoreCase.
        int count = _db.Delete<InventoryItem>(i => i.ItemName.Contains("widget", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, count);
    }

    [Fact]
    public void Contains_WithoutAComparison_StaysCaseSensitive()
    {
        int count = _db.Delete<InventoryItem>(i => i.ItemName.Contains("widget"));

        Assert.Equal(0, count);
    }

    [Fact]
    public void StartsWith_WithOrdinalIgnoreCase_MatchesBothCasings()
    {
        int count = _db.Delete<InventoryItem>(i => i.ItemName.StartsWith("wid", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, count);
    }

    [Fact]
    public void EndsWith_WithOrdinalIgnoreCase_MatchesRegardlessOfCase()
    {
        int count = _db.Delete<InventoryItem>(i => i.ItemName.EndsWith("BETA", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, count);
    }

    [Fact]
    public void EndsWith_WithoutAComparison_StaysCaseSensitive()
    {
        int count = _db.Delete<InventoryItem>(i => i.ItemName.EndsWith("BETA"));

        Assert.Equal(1, count);

        // ...and the differently-cased row is untouched, proving the match was exact rather than folded.
        Assert.Equal(0, _db.Delete<InventoryItem>(i => i.ItemName.EndsWith("beta")));
    }

    [Theory]
    [InlineData(StringComparison.InvariantCultureIgnoreCase)]
    [InlineData(StringComparison.CurrentCultureIgnoreCase)]
    public void Contains_WithAnyIgnoreCaseComparison_IsCaseInsensitive(StringComparison comparison)
    {
        int count = _db.Delete<InventoryItem>(i => i.ItemName.Contains("widget", comparison));

        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(StringComparison.Ordinal)]
    [InlineData(StringComparison.InvariantCulture)]
    public void Contains_WithACaseSensitiveComparison_StaysCaseSensitive(StringComparison comparison)
    {
        int count = _db.Delete<InventoryItem>(i => i.ItemName.Contains("widget", comparison));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Contains_WithOrdinalIgnoreCase_StillEscapesWildcards()
    {
        // The ILIKE switch must not disturb the ESCAPE clause: "%" in the value stays a literal.
        int count = _db.Delete<InventoryItem>(i => i.ItemName.Contains("%", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, count);
    }
}
