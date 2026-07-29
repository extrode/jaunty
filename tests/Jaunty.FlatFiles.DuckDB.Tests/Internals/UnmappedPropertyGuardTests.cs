using System.Linq.Expressions;

using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R26-067 (round 26, batch 7, low/bug), and the measurement that changed what "fixing" it means.
///
/// <para>
/// The finding: <c>ExpressionTranslator.GetColumnName</c> was a fourth, independent copy of the
/// "[Column] name or property name" rule, and the only one that never asked whether the property was
/// mapped - so <c>Update&lt;T&gt;</c>/<c>Delete&lt;T&gt;</c> built SQL against a property the rest of
/// the library treats as unmapped, and failed at the provider with
/// <c>Binder Error: Referenced column "Secret" not found in FROM clause!</c>, naming neither the
/// entity nor the reason.
/// </para>
///
/// <para>
/// <b>The duplication was fixed; the guard was not, because it is a regression.</b> Both halves of
/// the rule now live in <c>MappedPropertyFilter</c> and all four sites share them. But rejecting
/// unmapped properties in the translator - which a first attempt did - breaks working code: the
/// registered view exposes every column in the <em>file</em>, not the entity's mapped subset, so an
/// <c>[Ignore]</c>d property ("do not materialise this") whose column is present in the CSV is
/// legitimately usable in a predicate. Measured both ways below.
/// </para>
///
/// <para>
/// The finding's own repro used a property with no matching file column, where the provider does
/// reject the SQL - so the complaint is real but narrower than the guard. Which of the entity's
/// mapping and the file's columns defines the queryable surface is a design decision, carried to
/// round 27.
/// </para>
/// </summary>
public class UnmappedPropertyGuardTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"jaunty_unmapped_{Guid.NewGuid():N}");
    private readonly string _csv;

    public class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        /// <summary>Marked "do not materialise", but the CSV has the column.</summary>
        [Ignore]
        public string? Audited { get; set; }

        [Column("renamed_column")]
        public string? Renamed { get; set; }
    }

    public UnmappedPropertyGuardTests()
    {
        Directory.CreateDirectory(_dir);
        _csv = Path.Combine(_dir, "rows.csv");
        File.WriteAllText(_csv, "Id,Name,Audited\n1,a,yes\n2,b,no\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>
    /// The measurement that vetoed the guard. Without it: 1 row deleted. With it:
    /// <c>InvalidOperationException</c> before any SQL is built. This is the test that must be
    /// changed deliberately if the design decision ever goes the other way.
    /// </summary>
    [Fact]
    public void AnIgnoredPropertyWhoseColumnExistsInTheFile_IsStillUsableInAPredicate()
    {
        var options = new FlatFileOptions();
        options.AddCsv<Row>(_csv);
        using var db = new DuckDb(options);

        int deleted = db.Delete<Row>(r => r.Audited == "yes");

        Assert.Equal(1, deleted);
        // Table name comes from the entity, not the file: AddCsv<Row> registers "row".
        Assert.Single(db.Query<Row>("SELECT * FROM row"));
    }

    // ------------------------------------------------------------------
    // The half that was fixed: one copy of the naming rule, shared by all four sites
    // ------------------------------------------------------------------

    private static string Resolve(Expression<Func<Row, object>> selector) =>
        ExpressionTranslator.ResolveColumnName(selector);

    [Fact]
    public void AnOrdinaryProperty_ResolvesToItsOwnName()
    {
        Assert.Equal("Name", Resolve(r => r.Name!));
    }

    [Fact]
    public void AColumnAttribute_StillWins()
    {
        Assert.Equal("renamed_column", Resolve(r => r.Renamed!));
    }

    /// <summary>
    /// The translator must read the same answer <c>ColumnMappingCache</c> and
    /// <c>TargetDdlGenerator</c> now do - that shared rule is what AUD-R26-067 actually delivered.
    /// </summary>
    [Fact]
    public void TheTranslatorAgreesWithTheSharedRule()
    {
        System.Reflection.PropertyInfo renamed = typeof(Row).GetProperty(nameof(Row.Renamed))!;
        System.Reflection.PropertyInfo name = typeof(Row).GetProperty(nameof(Row.Name))!;

        Assert.Equal(MappedPropertyFilter.GetColumnName(renamed), Resolve(r => r.Renamed!));
        Assert.Equal(MappedPropertyFilter.GetColumnName(name), Resolve(r => r.Name!));
    }

    /// <summary>
    /// And the filtering half still excludes the property from everything that maps <em>values</em> -
    /// which is what <c>[Ignore]</c> means, and why the property being queryable is not a
    /// contradiction.
    /// </summary>
    [Fact]
    public void TheIgnoredPropertyIsStillExcludedFromValueMapping()
    {
        Assert.DoesNotContain(
            MappedPropertyFilter.GetMappedProperties(typeof(Row)),
            p => p.Name == nameof(Row.Audited));
    }
}
