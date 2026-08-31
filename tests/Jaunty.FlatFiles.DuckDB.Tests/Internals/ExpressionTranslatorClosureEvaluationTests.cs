using System.Linq.Expressions;

using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R25 (B7-8): <c>EvaluateExpression</c> fell back to
/// <c>Expression.Lambda&lt;Func&lt;object?&gt;&gt;(...).Compile()()</c> for every operand that was
/// not a <see cref="ConstantExpression"/> or a <c>Convert</c> over one.
///
/// <para>
/// The overwhelmingly common case - a closure-captured local, i.e.
/// <c>.Where(x =&gt; x.Age &gt; minAge)</c> - arrives as a <see cref="MemberExpression"/> over a
/// compiler-generated closure <see cref="ConstantExpression"/> and so never took the constant fast
/// path: each captured value cost a full expression compile (a <c>DynamicMethod</c> emit, tens to
/// hundreds of microseconds plus permanent code heap) where reading the closure field directly is a
/// handful of nanoseconds. A predicate with five captured values paid it five times, per
/// <c>Delete</c>/<c>Update</c> call, with nothing reused between calls.
/// </para>
///
/// <para>
/// This was the eighth byte-identical copy of the helper; the seven in Jaunty.Fluent now share
/// <c>ExpressionEvaluator</c>. This one stays duplicated because the assemblies are independent -
/// Jaunty.FlatFiles.DuckDB does not reference Jaunty.Fluent - so it needs its own coverage.
/// </para>
///
/// <para>
/// These tests pin translated values, not speed: "did this compile a <c>DynamicMethod</c>?" is not
/// observable from managed code. What is verified is that every operand shape which used to
/// evaluate still evaluates to the same parameter value, including the shapes deliberately left to
/// the compile fallback.
/// </para>
/// </summary>
public class ExpressionTranslatorClosureEvaluationTests
{
    [Fact]
    public void Translate_ClosureCapturedLocal_BindsItsValue()
    {
        int minId = 42;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id > minId;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(42, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_ClosureCapturedLocal_ReadsTheCurrentValueOnEachCall()
    {
        // The existing comment in EvaluateExpression explains why a string-keyed cache of compiled
        // delegates would be wrong - the fast path must read the field, not snapshot it.
        int minId = 1;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id > minId;

        var (_, first) = ExpressionTranslator.Translate(predicate);
        Assert.Equal(1, Assert.Single(first).Value);

        minId = 2;

        var (_, second) = ExpressionTranslator.Translate(predicate);
        Assert.Equal(2, Assert.Single(second).Value);
    }

    [Fact]
    public void Translate_MultipleCapturedLocals_BindsAllOfThem()
    {
        int low = 5;
        int high = 10;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id > low && x.Id < high;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(2, parameters.Count);
        Assert.Equal(5, parameters[0].Value);
        Assert.Equal(10, parameters[1].Value);
    }

    [Fact]
    public void Translate_CapturedNull_StillTranslatesToIsNull()
    {
        string? region = null;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Region == region;

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("IS NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translate_FieldOfACapturedObject_WalksTheChain()
    {
        var filter = new Filter { Field = 7 };
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == filter.Field;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(7, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_PropertyOfACapturedObject_WalksTheChain()
    {
        var filter = new Filter { Property = 9 };
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == filter.Property;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(9, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_NestedMemberChain_WalksAllTheWayDown()
    {
        var outer = new Filter { Nested = new Filter { Property = 11 } };
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == outer.Nested!.Property;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(11, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_Constant_StillBindsItsValue()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == 3;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(3, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_StaticField_BindsItsValue()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == StaticField;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(13, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_MethodCall_StillEvaluatesViaTheCompileFallback()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == Compute(3);

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(6, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_Arithmetic_StillEvaluatesViaTheCompileFallback()
    {
        int captured = 4;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == captured * 2;

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(8, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Translate_IndexerRead_StillEvaluatesViaTheCompileFallback()
    {
        var ids = new[] { 10, 20, 30 };
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == ids[1];

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal(20, Assert.Single(parameters).Value);
    }

    // ---------------------------------------------------------------------------

    private static readonly int StaticField = 13;

    private static int Compute(int x) => x * 2;

    private sealed class Filter
    {
        public int Field;
        public int Property { get; set; }
        public Filter? Nested { get; set; }
    }
}
