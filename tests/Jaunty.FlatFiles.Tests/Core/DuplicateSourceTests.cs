using Jaunty.Attributes;
using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.Tests.Core;

/// <summary>
/// AUD-R26: every <c>AddXxx&lt;T&gt;</c> resolves its table name from the entity type, so registering
/// two sources for one entity gave both the same name - and nothing objected at any layer.
/// <c>Sources</c> is a plain list, <c>DuckDbDialect.GenerateCreateViewSql</c> emits
/// <c>CREATE OR REPLACE VIEW</c>, and <c>DuckDb._sources</c> is a last-wins dictionary keyed on entity
/// type.
///
/// <para>
/// Measured end to end before the fix: <c>AddCsv&lt;Sales&gt;(a)</c> then <c>AddCsv&lt;Sales&gt;(b)</c>
/// constructed successfully and <c>Query&lt;Sales&gt;</c> returned only b's rows. a's row was simply
/// gone - no exception, no warning. "Load two files into one entity" is a natural thing to write and
/// the library does support it, through the multi-path constructor, so the error names that route.
/// </para>
/// </summary>
public class DuplicateSourceTests
{
    private class Sales
    {
        public int Id { get; set; }
        public string? Region { get; set; }
    }

    [Table("other_table")]
    private class Purchases
    {
        public int Id { get; set; }
    }

    [Fact]
    public void RegisteringTheSameEntityTwice_Throws()
    {
        var options = new FlatFileOptions();
        options.AddCsv<Sales>("sales_2023.csv");

        var ex = Assert.Throws<InvalidOperationException>(() => options.AddCsv<Sales>("sales_2024.csv"));

        Assert.Contains("already registered", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Sales", ex.Message, StringComparison.Ordinal);
        Assert.Contains("sales_2023.csv", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The message must point at the supported way to do what the caller was trying to do, not just
    /// refuse - reading several files as one table is exactly what the multi-path constructor is for.
    /// </summary>
    [Fact]
    public void TheMessageNamesTheMultiPathConstructor()
    {
        var options = new FlatFileOptions();
        options.AddCsv<Sales>("a.csv");

        var ex = Assert.Throws<InvalidOperationException>(() => options.AddCsv<Sales>("b.csv"));

        Assert.Contains("multi-path constructor", ex.Message, StringComparison.Ordinal);
        Assert.Contains("[Table(", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CollisionIsDetectedAcrossFormats()
    {
        // The table name comes from the entity, not the format, so a CSV and a Parquet source for
        // one entity collide just as surely as two CSVs.
        var options = new FlatFileOptions();
        options.AddCsv<Sales>("sales.csv");

        Assert.Throws<InvalidOperationException>(() => options.AddParquet<Sales>("sales.parquet"));
    }

    [Fact]
    public void CollisionIsDetectedCaseInsensitively()
    {
        // Target databases compare identifiers case-insensitively, so "sales" and "SALES" are one
        // table even though the strings differ.
        var options = new FlatFileOptions();
        options.AddSource(new CsvFileSource("sales", "a.csv", typeof(Sales)));

        Assert.Throws<InvalidOperationException>(
            () => options.AddSource(new CsvFileSource("SALES", "b.csv", typeof(Sales))));
    }

    [Fact]
    public void AddSource_IsGuardedToo()
    {
        var options = new FlatFileOptions();
        options.AddCsv<Sales>("a.csv");

        Assert.Throws<InvalidOperationException>(
            () => options.AddSource(new CsvFileSource("sales", "b.csv", typeof(Sales))));
    }

    // ------------------------------------------------------------------
    // AUD-R35-072: the bypass. Sources is a public mutable list, so the guard above is only as
    // good as the caller's choice of API - and the library's own FlatFile.Open(string) takes the
    // list route. DuckDb's constructor now re-checks, so the collision is caught wherever it came
    // from rather than reaching the last-wins _sources dictionary.
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureSourceTableNamesAreUnique_CatchesADuplicateAddedThroughTheList()
    {
        var options = new FlatFileOptions();
        options.AddCsv<Sales>("a.csv");
        options.Sources.Add(new CsvFileSource("sales", "b.csv", typeof(Sales)));

        var ex = Assert.Throws<InvalidOperationException>(options.EnsureSourceTableNamesAreUnique);

        Assert.Contains("already registered", ex.Message, StringComparison.Ordinal);
        Assert.Contains("a.csv", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureSourceTableNamesAreUnique_IsCaseInsensitiveToo()
    {
        var options = new FlatFileOptions();
        options.Sources.Add(new CsvFileSource("sales", "a.csv", typeof(Sales)));
        options.Sources.Add(new CsvFileSource("SALES", "b.csv", typeof(Sales)));

        Assert.Throws<InvalidOperationException>(options.EnsureSourceTableNamesAreUnique);
    }

    [Fact]
    public void EnsureSourceTableNamesAreUnique_FindsACollisionPastTheFirstPair()
    {
        var options = new FlatFileOptions();
        options.Sources.Add(new CsvFileSource("a", "a.csv", typeof(Sales)));
        options.Sources.Add(new CsvFileSource("b", "b.csv", typeof(Sales)));
        options.Sources.Add(new CsvFileSource("a", "c.csv", typeof(Sales)));

        Assert.Throws<InvalidOperationException>(options.EnsureSourceTableNamesAreUnique);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void EnsureSourceTableNamesAreUnique_DistinctNames_DoNotThrow(int count)
    {
        var options = new FlatFileOptions();
        for (int i = 0; i < count; i++)
            options.Sources.Add(new CsvFileSource($"t{i}", $"{i}.csv", typeof(Sales)));

        options.EnsureSourceTableNamesAreUnique();
    }

    // ------------------------------------------------------------------
    // What must still be accepted
    // ------------------------------------------------------------------

    [Fact]
    public void DistinctTableNames_AreAccepted()
    {
        var options = new FlatFileOptions();
        options.AddCsv<Sales>("sales.csv");
        options.AddCsv<Purchases>("purchases.csv");

        Assert.Equal(2, options.Sources.Count);
    }

    [Fact]
    public void TheMultiPathConstructor_IsTheSupportedWayToReadSeveralFiles()
    {
        // The route the error message recommends must actually work.
        var options = new FlatFileOptions();
        options.AddSource(new CsvFileSource("sales", ["sales_2023.csv", "sales_2024.csv"], typeof(Sales)));

        IFileSource source = Assert.Single(options.Sources);
        Assert.Equal(2, source.FilePaths!.Count);
    }

    [Fact]
    public void AddSource_StillRejectsNull()
    {
        var options = new FlatFileOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddSource(null!));
    }
}
