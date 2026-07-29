using System.Linq.Expressions;

using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R26-067 (round 26, batch 7, low/bug). <c>ExpressionTranslator.GetColumnName</c> was a fourth,
/// independent copy of the "[Column] name or property name" rule, and the only one that never asked
/// whether the property was mapped at all - so <c>Update&lt;T&gt;</c> and <c>Delete&lt;T&gt;</c> built
/// SQL against a property the rest of the library treats as unmapped.
///
/// <para>
/// It did fail, so nothing was corrupted, but it failed at the provider with a message naming neither
/// the entity nor the reason:
/// <c>Binder Error: Referenced update column Secret not found in table!</c> for the update, and
/// <c>Binder Error: Referenced column "Secret" not found in FROM clause!</c> for the delete. A caller
/// who has just added <c>[Ignore]</c> to a property has no thread back from that to the cause.
/// </para>
///
/// <para>
/// <c>MappedPropertyFilter</c> exists (AUD-R25) precisely because two copies of this rule had already
/// drifted; <c>ColumnMappingCache</c> and <c>TargetDdlGenerator</c> were brought under it then, for
/// the filtering half. Both halves now live there and all four sites share them.
/// </para>
/// </summary>
public class UnmappedPropertyGuardTests
{
    private class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        [Ignore]
        public string? Ignored { get; set; }

        [Column("renamed_column")]
        public string? Renamed { get; set; }

        public string ReadOnly => "no setter";
    }

    private static string Resolve(Expression<Func<Row, object>> selector) =>
        ExpressionTranslator.ResolveColumnName(selector);

    [Fact]
    public void AnIgnoredProperty_IsRejectedByNameWithItsEntity()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Resolve(r => r.Ignored!));

        Assert.Contains("Row.Ignored", ex.Message, StringComparison.Ordinal);
        Assert.Contains("[Ignore]", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A get-only property is unmapped for a different reason - <c>MappedPropertyFilter.IsMapped</c>
    /// requires both accessors - and must be rejected the same way rather than reaching the provider.
    /// </summary>
    [Fact]
    public void AGetOnlyProperty_IsRejectedToo()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Resolve(r => r.ReadOnly));

        Assert.Contains("Row.ReadOnly", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // What must keep working
    // ------------------------------------------------------------------

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
    /// The naming half of the rule moved to <c>MappedPropertyFilter</c> so the four sites cannot
    /// drift again; this is the assertion that the translator reads the same answer that
    /// <c>ColumnMappingCache</c> and <c>TargetDdlGenerator</c> now do.
    /// </summary>
    [Fact]
    public void TheTranslatorAgreesWithTheSharedRule()
    {
        System.Reflection.PropertyInfo renamed = typeof(Row).GetProperty(nameof(Row.Renamed))!;

        Assert.Equal(MappedPropertyFilter.GetColumnName(renamed), Resolve(r => r.Renamed!));
    }
}
