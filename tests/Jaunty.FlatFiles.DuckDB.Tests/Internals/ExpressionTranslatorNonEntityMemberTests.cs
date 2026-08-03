using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-025. Only <c>ExtractColumnAndValue</c> asked <c>IsEntityMember</c> before treating a
/// member expression as a column. The three other sites that resolve one did not, so a property
/// read with nothing to do with the entity was emitted as a column reference. The registered DuckDB
/// view exposes every column in the file, so a name collision with a real column meant the
/// predicate silently filtered on file data instead of the caller's value.
/// </summary>
public class ExpressionTranslatorNonEntityMemberTests
{
    private sealed class Captured
    {
        public bool Flag { get; set; }
        public int Quantity { get; set; }
        public string Region { get; set; } = "north";
    }

    private static class StaticSettings
    {
        public static bool DebugMode => true;
        public static int Quantity => 3;
    }

    // ------------------------------------------------------------------
    // VisitBoolMember
    // ------------------------------------------------------------------

    [Fact]
    public void BoolMemberOnACapturedObject_Throws()
    {
        var captured = new Captured { Flag = true };
        Expression<Func<SalesRecord, bool>> predicate = x => captured.Flag;

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Contains("not a column", ex.Message);
    }

    [Fact]
    public void BoolMemberOnAStaticProperty_Throws() =>
        Assert.Throws<NotSupportedException>(() =>
            ExpressionTranslator.Translate<SalesRecord>(x => StaticSettings.DebugMode));

    // ------------------------------------------------------------------
    // VisitMethodCall, string branch
    // ------------------------------------------------------------------

    [Fact]
    public void StringMethodWithTheReceiverAndArgumentReversed_Throws()
    {
        // x => caption.StartsWith(x.ProductName) used to emit a LIKE over a column "caption".
        var caption = new Captured();

        Assert.Throws<NotSupportedException>(() =>
            ExpressionTranslator.Translate<SalesRecord>(x => caption.Region.StartsWith(x.ProductName)));
    }

    // ------------------------------------------------------------------
    // HandleInClause
    // ------------------------------------------------------------------

    [Fact]
    public void InClauseOverACapturedItem_Throws()
    {
        // x => ids.Contains(threshold) used to become "Quantity" IN (...), filtering on the file's
        // own Quantity column rather than on the caller's captured value.
        int[] ids = [1, 2, 3];
        var captured = new Captured { Quantity = 2 };

        Assert.Throws<NotSupportedException>(() =>
            ExpressionTranslator.Translate<SalesRecord>(x => ids.Contains(captured.Quantity)));
    }

    [Fact]
    public void InClauseOverAStaticItem_Throws()
    {
        int[] ids = [1, 2, 3];

        Assert.Throws<NotSupportedException>(() =>
            ExpressionTranslator.Translate<SalesRecord>(x => ids.Contains(StaticSettings.Quantity)));
    }

    // ------------------------------------------------------------------
    // Controls: the supported shapes must still translate
    // ------------------------------------------------------------------

    [Fact]
    public void ABoolPropertyOfTheEntity_StillTranslates()
    {
        (string sql, _) = ExpressionTranslator.Translate<BoolEntity>(x => x.Active);

        Assert.Contains("\"Active\" = true", sql);
    }

    [Fact]
    public void AStringMethodOnAnEntityProperty_StillTranslates()
    {
        (string sql, _) = ExpressionTranslator.Translate<SalesRecord>(x => x.ProductName.StartsWith("a"));

        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void AnInClauseOverAnEntityProperty_StillTranslates()
    {
        int[] ids = [1, 2, 3];

        (string sql, _) = ExpressionTranslator.Translate<SalesRecord>(x => ids.Contains(x.Id));

        Assert.Contains("\"Id\" IN", sql);
    }

    [Fact]
    public void AComparisonAgainstACapturedValue_StillTranslates()
    {
        var captured = new Captured { Quantity = 5 };

        (string sql, List<DuckDBParameter> parameters) =
            ExpressionTranslator.Translate<SalesRecord>(x => x.Quantity > captured.Quantity);

        Assert.Contains("\"Quantity\" > $1", sql);
        Assert.Equal(5, parameters[0].Value);
    }

    [Fact]
    public void AComparisonWithTheCapturedValueOnTheLeft_StillTranslates()
    {
        var captured = new Captured { Quantity = 5 };

        (string sql, _) = ExpressionTranslator.Translate<SalesRecord>(x => captured.Quantity > x.Quantity);

        Assert.Contains("\"Quantity\" < $1", sql);
    }

    private sealed class BoolEntity
    {
        public bool Active { get; set; }
    }
}
