using Jaunty.Attributes;
using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26-056 (batch 5, low/bug). A string method chained onto another string method crashed with
/// a runtime error naming neither the method nor the limitation.
///
/// <para>
/// The string handler in <c>VisitMethodCall</c> requires <c>node.Object</c> to be a
/// <c>MemberExpression</c> over the lambda parameter. A chained call's receiver is another
/// <c>MethodCallExpression</c>, so it falls past that handler, past the <c>Contains</c>/IN branch
/// (which excludes string receivers), and into the <c>EvaluateExpression</c> fallback - which
/// compiles a lambda still referencing the unbound parameter and throws
/// <c>InvalidOperationException: variable 'p' of type 'Item' referenced from scope '', but it is
/// not defined</c>.
/// </para>
///
/// <para>
/// Both halves work in isolation and the class summary advertises both sets, so combining them is
/// the obvious next thing a caller tries. This is the same failure mode AUD-R12 and R16 fixed for
/// column-to-column and <c>string.Length</c> comparisons: an expression still referencing the
/// lambda parameter reaching <c>Compile()</c>. The guard now catches every unsupported
/// parameter-referencing call, not only the chained-string shape.
/// </para>
/// </summary>
public class UntranslatableWhereExpressionTests
{
    [Table("untranslatable_items")]
    public class Item
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    private static string Translate(System.Linq.Expressions.Expression<Func<Item, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<Item>(new SQLiteDialect());
        return visitor.Translate(predicate).Sql;
    }

    // ------------------------------------------------------------------
    // The chains that used to crash unhelpfully
    // ------------------------------------------------------------------

    [Fact]
    public void ToUpperThenContains_ThrowsNamingTheMethodAndTheLimitation()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => Translate(p => p.Name.ToUpper().Contains("ALP")));

        Assert.Contains("Contains", exception.Message, StringComparison.Ordinal);
        Assert.Contains("chained", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TrimThenStartsWith_Throws()
    {
        Assert.Throws<NotSupportedException>(() => Translate(p => p.Name.Trim().StartsWith("al")));
    }

    [Fact]
    public void ToLowerThenEndsWith_Throws()
    {
        Assert.Throws<NotSupportedException>(() => Translate(p => p.Name.ToLower().EndsWith("ha")));
    }

    /// <summary>
    /// The message has to be actionable, not merely typed. A caller who sees it should learn which
    /// call failed and what shape does work.
    /// </summary>
    [Fact]
    public void TheMessageNamesTheWorkingShape()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => Translate(p => p.Name.ToUpper().Contains("ALP")));

        Assert.Contains("p.Name.ToUpper()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("raw SQL", exception.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // What must keep working - both halves, on their own
    // ------------------------------------------------------------------

    /// <summary>
    /// The operator is the dialect's business - SQLite emits <c>GLOB</c> for a case-sensitive
    /// match, not <c>LIKE</c>, because its <c>LIKE</c> is case-insensitive for ASCII. What matters
    /// here is that a pattern comparison against the column is produced at all.
    /// </summary>
    [Fact]
    public void ContainsOnAProperty_StillTranslates()
    {
        string sql = Translate(p => p.Name.Contains("alp"));

        Assert.Contains("Name", sql, StringComparison.Ordinal);
        Assert.True(
            sql.Contains("GLOB", StringComparison.Ordinal) || sql.Contains("LIKE", StringComparison.Ordinal),
            $"Expected a pattern-match operator, got: {sql}");
    }

    [Fact]
    public void ToUpperComparedToAConstant_StillTranslates()
    {
        Assert.Contains("UPPER", Translate(p => p.Name.ToUpper() == "ALPHA"), StringComparison.Ordinal);
    }

    [Fact]
    public void SubstringComparedToAConstant_StillTranslates()
    {
        Assert.Contains("SUBSTR", Translate(p => p.Name.Substring(0, 2) == "al"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A method call that does not touch the lambda parameter is still evaluated as a constant -
    /// the guard keys on the parameter reference, not on "is a method call", so this path is
    /// untouched.
    /// </summary>
    [Fact]
    public void AMethodCallNotTouchingTheParameter_IsStillEvaluated()
    {
        var names = new List<string> { "alpha", "beta" };

        string sql = Translate(p => names.Contains(p.Name));

        Assert.Contains("IN (", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AConstantBooleanMethodCall_IsStillEvaluated()
    {
        var flag = "yes";

        string sql = Translate(p => flag.StartsWith("y") && p.Quantity > 0);

        Assert.Contains("1 = 1", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The IN clause no longer materialises the caller's collection twice
    // ------------------------------------------------------------------

    /// <summary>
    /// AUD-R26-056, second half. The IN branch built a <c>List&lt;object&gt;</c> via
    /// <c>Cast&lt;object&gt;().ToList()</c> purely to learn the count and then read each element
    /// once. A collection that knows its own size does not need the copy. These assert the SQL is
    /// unchanged by that rewrite - the allocation is what changed, not the output.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(64)]
    public void InClause_OverAnArray_EmitsOneParameterPerValue(int count)
    {
        int[] ids = Enumerable.Range(1, count).ToArray();
        var visitor = new WhereExpressionVisitor<Item>(new SQLiteDialect());

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(p => ids.Contains(p.Id));

        Assert.Equal(count, parameters.Count);
        Assert.Contains(" IN (", sql, StringComparison.Ordinal);
        Assert.Equal(count - 1, sql.Count(c => c == ','));
    }

    [Fact]
    public void InClause_OverAList_StillWorks()
    {
        var ids = new List<int> { 4, 5, 6 };
        var visitor = new WhereExpressionVisitor<Item>(new SQLiteDialect());

        (_, List<(string Name, object? Value)> parameters) = visitor.Translate(p => ids.Contains(p.Id));

        Assert.Equal([4, 5, 6], parameters.Select(x => x.Value).Cast<int>());
    }

    /// <summary>
    /// A lazy sequence has no <c>Count</c>, so it must still be buffered - and buffered exactly
    /// once, since walking a single-pass or side-effecting sequence twice would be a correctness
    /// bug rather than a performance one.
    /// </summary>
    [Fact]
    public void InClause_OverALazySequence_EnumeratesItExactlyOnce()
    {
        int enumerations = 0;
        IEnumerable<int> Lazy()
        {
            enumerations++;
            yield return 7;
            yield return 8;
        }

        IEnumerable<int> ids = Lazy();
        var visitor = new WhereExpressionVisitor<Item>(new SQLiteDialect());

        (_, List<(string Name, object? Value)> parameters) = visitor.Translate(p => ids.Contains(p.Id));

        Assert.Equal(1, enumerations);
        Assert.Equal([7, 8], parameters.Select(x => x.Value).Cast<int>());
    }

    [Fact]
    public void InClause_OverAnEmptyCollection_IsStillAlwaysFalse()
    {
        int[] ids = [];
        var visitor = new WhereExpressionVisitor<Item>(new SQLiteDialect());

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(p => ids.Contains(p.Id));

        Assert.Contains("1 = 0", sql, StringComparison.Ordinal);
        Assert.Empty(parameters);
    }
}
