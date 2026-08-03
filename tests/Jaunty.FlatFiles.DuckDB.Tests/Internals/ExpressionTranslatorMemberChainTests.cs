using System.Linq.Expressions;

using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-248. <c>IsEntityMember</c> walks a member chain all the way to the
/// <c>ParameterExpression</c> and accepts it, but only the leaf member was resolved - so
/// <c>x =&gt; x.Score!.Value &gt; 5</c> emitted a column <c>"Value"</c> and
/// <c>x =&gt; x.Child.Name == "a"</c> emitted <c>"Name"</c>. Neither was translated correctly nor
/// rejected: both reached the provider naming a column the caller never wrote.
/// </summary>
public class ExpressionTranslatorMemberChainTests
{
    private sealed class Child
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public int? Score { get; set; }
        public string Name { get; set; } = "";
        public Child Child { get; set; } = new();
    }

    private static string Sql(Expression<Func<Row, bool>> predicate)
        => ExpressionTranslator.Translate(predicate).Sql;

    [Fact]
    public void ALiftedValueRead_ResolvesToTheColumnItself()
    {
        Assert.Contains("\"Score\"", Sql(x => x.Score!.Value > 5), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Value\"", Sql(x => x.Score!.Value > 5), StringComparison.Ordinal);
    }

    [Fact]
    public void ALiftedValueRead_MatchesThePlainRead()
    {
        Assert.Equal(Sql(x => x.Score > 5), Sql(x => x.Score!.Value > 5));
    }

    [Fact]
    public void ANavigationIntoAnotherEntity_IsRefusedByName()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Sql(x => x.Child.Name == "a"));

        Assert.Contains("x.Child", ex.Message, StringComparison.Ordinal);
        Assert.Contains("no join to follow", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANavigationOntoANonStringMember_IsAlsoRefused()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Sql(x => x.Child.Value == 1));

        Assert.Contains("x.Child", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasValue_IsRefusedAndPointsAtTheNullComparison()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Sql(x => x.Score.HasValue));

        Assert.Contains("!= null", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullComparison_StillTranslates()
    {
        Assert.Contains("IS NOT NULL", Sql(x => x.Score != null), StringComparison.Ordinal);
        Assert.Contains("IS NULL", Sql(x => x.Score == null), StringComparison.Ordinal);
    }

    [Fact]
    public void APlainMemberOffTheParameter_IsUnaffected()
    {
        Assert.Contains("\"Name\"", Sql(x => x.Name == "a"), StringComparison.Ordinal);
        Assert.Contains("\"Id\"", Sql(x => x.Id == 1), StringComparison.Ordinal);
    }

    [Fact]
    public void ALiftedValueInAStringCall_AlsoResolvesToTheColumn()
    {
        Assert.Contains("\"Name\"", Sql(x => x.Name.StartsWith("a")), StringComparison.Ordinal);
    }
}
