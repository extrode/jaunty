using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R26: the three code paths that resolve columns from an entity disagreed about what two
/// properties on one column meant. <see cref="ColumnMappingCache"/> collapsed them and kept the
/// <b>last</b>, silently dropping the other. <see cref="TargetDdlGenerator"/> kept <b>both</b>,
/// producing <c>CREATE TABLE ("code","CODE")</c> which SQLite rejects as
/// <c>duplicate column name: CODE</c> and SQL Server likewise. <c>JauntyGenerator</c> already
/// diagnosed it at compile time as <c>JAUNTYGEN001</c> and kept the <b>first</b>.
///
/// <para>
/// Both runtime paths now refuse the entity with one message naming both properties. Picking a
/// winner was rejected as a fix: every available winner is a guess about which property the caller
/// meant, and two of the three targets already refused the DDL the silent choice produced.
/// </para>
/// </summary>
public class DuplicateColumnGuardTests
{
    private class CaseDifferingDuplicate
    {
        [Column("code")]
        public string? First { get; set; }

        [Column("CODE")]
        public string? Second { get; set; }
    }

    private class ExactDuplicate
    {
        [Column("code")]
        public string? First { get; set; }

        [Column("code")]
        public string? Second { get; set; }
    }

    private class AttributeCollidesWithPropertyName
    {
        public string? Code { get; set; }

        [Column("Code")]
        public string? Other { get; set; }
    }

    private class IgnoredPropertyResolvesTheCollision
    {
        [Column("code")]
        public string? First { get; set; }

        [Ignore]
        [Column("code")]
        public string? Second { get; set; }
    }

    private class ShadowBase
    {
        public object? Code { get; set; }
    }

    // A `new` shadow whose TYPE differs from the hidden member is the case where
    // GetProperties returns both declarations. A same-type shadow collapses to one.
    private class ShadowDerived : ShadowBase
    {
        public new string? Code { get; set; }
    }

    private class SameTypeShadowBase
    {
        public int Code { get; set; }
    }

    private class SameTypeShadowDerived : SameTypeShadowBase
    {
        public new int Code { get; set; }
    }

    private class Distinct
    {
        [Key]
        public int Id { get; set; }

        [Column("full_name")]
        public string? Name { get; set; }

        public string? Code { get; set; }
    }

    // ------------------------------------------------------------------
    // Both paths reject, and say the same thing
    // ------------------------------------------------------------------

    public static TheoryData<string, Type> DuplicateEntities() => new()
    {
        { "case-differing", typeof(CaseDifferingDuplicate) },
        { "exact", typeof(ExactDuplicate) },
        { "attribute-vs-property-name", typeof(AttributeCollidesWithPropertyName) },
    };

    [Theory]
    [MemberData(nameof(DuplicateEntities))]
    public void ColumnMappingCache_RejectsDuplicates(string kind, Type entityType)
    {
        _ = kind;

        var ex = Assert.Throws<InvalidOperationException>(() => ColumnMappingCache.Get(entityType));

        Assert.Contains(entityType.Name, ex.Message, StringComparison.Ordinal);
        Assert.Contains("more than one property", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(DuplicateEntities))]
    public void TargetDdlGenerator_RejectsTheSameEntities(string kind, Type entityType)
    {
        _ = kind;

        var ex = Assert.Throws<InvalidOperationException>(
            () => TargetDdlGenerator.GetColumnDefinitions(entityType));

        Assert.Contains(entityType.Name, ex.Message, StringComparison.Ordinal);
        Assert.Contains("more than one property", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMessageNamesBothProperties()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ColumnMappingCache.Get(typeof(CaseDifferingDuplicate)));

        Assert.Contains("First", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Second", ex.Message, StringComparison.Ordinal);
        Assert.Contains("code", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // What must still be accepted
    // ------------------------------------------------------------------

    [Fact]
    public void DistinctColumns_AreAccepted()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(Distinct));

        Assert.Equal(3, mappings.Count);
        Assert.Equal(3, TargetDdlGenerator.GetColumnDefinitions(typeof(Distinct)).Count);
    }

    /// <summary>
    /// Regression: the guard must not mistake a hidden base declaration for a second property.
    /// `GetProperties(Public | Instance)` returns BOTH `PropertyInfo`s when a `new` shadow changes
    /// the property type - measured: `string Code` hiding `object Code` yields two, where a
    /// same-type shadow collapses to one. Those two are one logical property. Rejecting them would
    /// have broken every FlatFiles.DuckDB surface for an entity that previously worked, since
    /// `ColumnMappingCache.Get` sits on the read, write, export and import paths alike.
    /// </summary>
    [Fact]
    public void ADifferentlyTypedNewShadow_IsNotACollision()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(ShadowDerived));

        ColumnMapping mapping = Assert.Single(mappings).Value;
        // The more-derived declaration must win: GetProperties lists it first, and it is the one the
        // caller means. Last-wins would have picked the hidden base declaration.
        Assert.Equal(typeof(string), mapping.PropertyType);
        Assert.Equal(typeof(ShadowDerived), mapping.Property.DeclaringType);
    }

    [Fact]
    public void ADifferentlyTypedNewShadow_ProducesOneDdlColumn()
    {
        List<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns =
            TargetDdlGenerator.GetColumnDefinitions(typeof(ShadowDerived));

        (string Name, Type ClrType, bool _, bool __) = Assert.Single(columns);
        Assert.Equal("Code", Name);
        Assert.Equal(typeof(string), ClrType);
    }

    [Fact]
    public void ASameTypedNewShadow_IsAlsoFine()
    {
        // This one never reached the guard - reflection collapses it to a single PropertyInfo - but
        // it is pinned so the two shadow shapes cannot diverge if the enumeration changes.
        Assert.Single(ColumnMappingCache.Get(typeof(SameTypeShadowDerived)));
        Assert.Single(TargetDdlGenerator.GetColumnDefinitions(typeof(SameTypeShadowDerived)));
    }

    [Fact]
    public void AnIgnoredPropertyDoesNotCollide()
    {
        // The guard runs after MappedPropertyFilter, so marking one of the pair [Ignore] is a valid
        // way out - and is one of the two fixes the error message suggests.
        IReadOnlyDictionary<string, ColumnMapping> mappings =
            ColumnMappingCache.Get(typeof(IgnoredPropertyResolvesTheCollision));

        Assert.Single(mappings);
        Assert.Single(TargetDdlGenerator.GetColumnDefinitions(typeof(IgnoredPropertyResolvesTheCollision)));
    }
}
