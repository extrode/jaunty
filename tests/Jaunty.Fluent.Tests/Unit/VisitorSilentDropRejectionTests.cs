using System.Linq.Expressions;

using Jaunty.Attributes;
using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R34-018/019/020. Three visitors appended fragments to a shared StringBuilder from an
/// override set that did not cover every node type the C# compiler can put in the tree. The
/// uncovered ones took <see cref="ExpressionVisitor"/>'s descend-into-children default, which emits
/// each child bare with nothing joining them - so the caller got plausible SQL with an operator or
/// a whole subexpression missing, and the wrong rows back, rather than a translation error.
/// </summary>
public class VisitorSilentDropRejectionTests
{
    [Table("visitor_left")]
    public class Left
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public DateTime Created { get; set; }
    }

    [Table("visitor_right")]
    public class Right
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Table("visitor_third")]
    public class Third
    {
        [Key]
        public int Id { get; set; }
    }

    [Table("visitor_fourth")]
    public class Fourth
    {
        [Key]
        public int Id { get; set; }
    }

    private static string TranslateJoin(Expression<Func<Left, Right, bool>> predicate)
        => new JoinExpressionVisitor<Left, Right>(new SQLiteDialect(), "l", "r").Translate(predicate).Sql;

    private static string TranslateWhere(Expression<Func<Left, bool>> predicate)
        => new WhereExpressionVisitor<Left>(new SQLiteDialect()).Translate(predicate).Sql;

    // ------------------------------------------------------------------
    // AUD-R34-019: the arity-2 JOIN visitor.
    // ------------------------------------------------------------------

    [Fact]
    public void JoinPredicate_WithAConstructorCall_ThrowsInsteadOfEmittingItsArgumentsBare()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => TranslateJoin((l, r) => l.Created == new DateTime(2020, 1, 1)));

        Assert.Contains("DateTime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinPredicate_WithATypeTest_Throws()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => TranslateJoin((l, r) => l.Name is object && l.Id == r.Id));

        Assert.Contains("Type tests", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinPredicate_WithADelegateInvocation_Throws()
    {
        Func<int, bool> positive = x => x > 0;

        var exception = Assert.Throws<NotSupportedException>(
            () => TranslateJoin((l, r) => positive(l.Id)));

        Assert.Contains("delegate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinPredicate_WithANegatedColumn_ThrowsInsteadOfDroppingTheNegation()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => TranslateJoin((l, r) => -l.Id == r.Id));

        Assert.Contains("Negate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinPredicate_ThatIsSupported_StillTranslates()
    {
        var sql = TranslateJoin((l, r) => l.Id == r.Id);

        Assert.Equal("(l.Id = r.Id)", sql);
    }

    // ------------------------------------------------------------------
    // AUD-R34-019: the same holes existed in the arity-3 and arity-4 visitors, which is the
    // arity-drift pattern earlier rounds found on the joined builders.
    // ------------------------------------------------------------------

    [Fact]
    public void JoinPredicate3_WithAConstructorCall_Throws()
    {
        var visitor = new JoinExpressionVisitor3<Left, Right, Third>(new SQLiteDialect(), "l", "r", "t");

        Assert.Throws<NotSupportedException>(
            () => visitor.Translate((l, r, t) => l.Created == new DateTime(2020, 1, 1)));
    }

    [Fact]
    public void JoinPredicate3_WithANegatedColumn_Throws()
    {
        var visitor = new JoinExpressionVisitor3<Left, Right, Third>(new SQLiteDialect(), "l", "r", "t");

        Assert.Throws<NotSupportedException>(
            () => visitor.Translate((l, r, t) => -l.Id == t.Id));
    }

    [Fact]
    public void JoinPredicate4_WithAConstructorCall_Throws()
    {
        var visitor = new JoinExpressionVisitor4<Left, Right, Third, Fourth>(
            new SQLiteDialect(), "l", "r", "t", "f");

        Assert.Throws<NotSupportedException>(
            () => visitor.Translate((l, r, t, f) => l.Created == new DateTime(2020, 1, 1)));
    }

    [Fact]
    public void JoinPredicate4_WithANegatedColumn_Throws()
    {
        var visitor = new JoinExpressionVisitor4<Left, Right, Third, Fourth>(
            new SQLiteDialect(), "l", "r", "t", "f");

        Assert.Throws<NotSupportedException>(
            () => visitor.Translate((l, r, t, f) => -l.Id == f.Id));
    }

    // ------------------------------------------------------------------
    // AUD-R34-018: the SELECT visitor's missing VisitNewArray.
    // ------------------------------------------------------------------

    [Fact]
    public void SelectProjection_OfAnArrayInitializer_ThrowsInsteadOfFlatteningToColumns()
    {
        var visitor = new SelectExpressionVisitor<Left>(new SQLiteDialect());

        var exception = Assert.Throws<NotSupportedException>(
            () => visitor.Translate(p => new[] { p.Name, p.Description }));

        Assert.Contains("Array construction", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectProjection_OfAnArrayInitializer_DoesNotSilentlyDropAnOperator()
    {
        var visitor = new SelectExpressionVisitor<Left>(new SQLiteDialect());

        Assert.Throws<NotSupportedException>(
            () => visitor.Translate(p => new[] { p.UnitPrice * 2 }));
    }

    [Fact]
    public void SelectProjection_OfAnAnonymousType_StillTranslates()
    {
        var visitor = new SelectExpressionVisitor<Left>(new SQLiteDialect());

        List<SelectColumn> columns = visitor.Translate(p => new { p.Name, p.Description });

        Assert.Equal(2, columns.Count);
    }

    // ------------------------------------------------------------------
    // AUD-R34-020: the WHERE visitor's base.VisitUnary tail.
    // ------------------------------------------------------------------

    [Fact]
    public void WherePredicate_WithANegatedColumn_ThrowsInsteadOfReturningTheOppositeRows()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => TranslateWhere(p => -p.UnitPrice > 5m));

        Assert.Contains("Negate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WherePredicate_WithATypeAsOperand_Throws()
    {
        object boxed = 5;

        Assert.Throws<NotSupportedException>(
            () => TranslateWhere(p => (p.Name as object) == boxed));
    }

    [Fact]
    public void WherePredicate_WithNotAndConvert_StillTranslates()
    {
        var sql = TranslateWhere(p => !(p.Id > 5));

        Assert.Contains("NOT (", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void WherePredicate_WithACompoundCaseCondition_StillTranslates()
    {
        // The Quote arm of VisitUnary is load-bearing: it is how a compound Sql.Case condition is
        // re-entered. Fixing the tail must not close it.
        var sql = TranslateWhere(p => p.Id > 0 && p.Name != null);

        Assert.Contains(" AND ", sql, StringComparison.Ordinal);
    }
}
