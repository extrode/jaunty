using System.Linq.Expressions;

using Jaunty.Fluent.Internals;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R25 (B5-4): seven byte-for-byte copies of the same <c>EvaluateExpression</c> existed in
/// Jaunty.Fluent - the six expression visitors plus <c>HavingExpressionHelpers</c> - each doing
/// <c>Expression.Lambda(expression).Compile()</c> followed by <c>Delegate.DynamicInvoke()</c> for
/// every non-<see cref="ConstantExpression"/> operand.
///
/// <para>
/// The overwhelmingly common case - a closure-captured local, i.e.
/// <c>.Where(p =&gt; p.Id == someLocal)</c> - arrives as a <see cref="MemberExpression"/> over a
/// compiler-generated closure <see cref="ConstantExpression"/>, and was therefore never taken by
/// the constant fast path: each such value cost a full expression compile (a <c>DynamicMethod</c>
/// emit, tens to hundreds of microseconds and permanent code heap) plus a reflection-based
/// <c>DynamicInvoke</c> with its <c>object[]</c> boxing, where reading the closure field directly
/// is a handful of nanoseconds. A predicate with five captured values paid it five times, on every
/// query build, with nothing cached between builds.
/// </para>
///
/// <para>
/// These tests pin the evaluator's results rather than its speed. The performance claim is not
/// directly assertable - "did this compile a DynamicMethod?" is not observable from managed code -
/// so what is verified here is that every shape which used to evaluate still evaluates to the same
/// value, including the shapes deliberately left to the compile fallback.
/// </para>
/// </summary>
public class ExpressionEvaluatorTests
{
    // ---------------------------------------------------------------------------
    // The case the fast path exists for
    // ---------------------------------------------------------------------------

    [Fact]
    public void Evaluate_ClosureCapturedLocal_ReturnsItsValue()
    {
        int captured = 42;
        Expression<Func<int>> lambda = () => captured;

        Assert.Equal(42, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_ClosureCapturedLocal_ReadsTheCurrentValueNotTheValueAtCaptureTime()
    {
        // The reason a string-keyed cache of compiled delegates would be wrong, and the reason the
        // fast path must read the field rather than snapshot it.
        int captured = 1;
        Expression<Func<int>> lambda = () => captured;

        Assert.Equal(1, ExpressionEvaluator.Evaluate(lambda.Body));

        captured = 2;

        Assert.Equal(2, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_ClosureCapturedReferenceType_ReturnsTheSameInstance()
    {
        var captured = new object();
        Expression<Func<object>> lambda = () => captured;

        Assert.Same(captured, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_NullClosureCapturedValue_ReturnsNull()
    {
        string? captured = null;
        Expression<Func<string?>> lambda = () => captured;

        Assert.Null(ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_FieldOfACapturedObject_WalksTheChain()
    {
        var holder = new Holder { Field = 7 };
        Expression<Func<int>> lambda = () => holder.Field;

        Assert.Equal(7, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_PropertyOfACapturedObject_WalksTheChain()
    {
        var holder = new Holder { Property = 9 };
        Expression<Func<int>> lambda = () => holder.Property;

        Assert.Equal(9, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_NestedMemberChain_WalksAllTheWayDown()
    {
        var outer = new Holder { Nested = new Holder { Property = 11 } };
        Expression<Func<int>> lambda = () => outer.Nested!.Property;

        Assert.Equal(11, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    // ---------------------------------------------------------------------------
    // Shapes that were already fast, and shapes still left to the compile fallback
    // ---------------------------------------------------------------------------

    [Fact]
    public void Evaluate_Constant_ReturnsItsValue()
    {
        Assert.Equal(5, ExpressionEvaluator.Evaluate(Expression.Constant(5)));
    }

    [Fact]
    public void Evaluate_NullConstant_ReturnsNull()
    {
        Assert.Null(ExpressionEvaluator.Evaluate(Expression.Constant(null, typeof(string))));
    }

    [Fact]
    public void Evaluate_StaticField_ReturnsItsValue()
    {
        Expression<Func<int>> lambda = () => StaticField;

        Assert.Equal(13, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_StaticProperty_ReturnsItsValue()
    {
        Expression<Func<int>> lambda = () => StaticProperty;

        Assert.Equal(17, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_MethodCall_StillEvaluatesViaTheCompileFallback()
    {
        // Genuinely computed operands are not fast-pathed, and must still work.
        Expression<Func<int>> lambda = () => Compute(3);

        Assert.Equal(6, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_Arithmetic_StillEvaluatesViaTheCompileFallback()
    {
        int captured = 4;
        Expression<Func<int>> lambda = () => captured * 2;

        Assert.Equal(8, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_Conversion_StillEvaluatesViaTheCompileFallback()
    {
        int captured = 5;
        Expression<Func<long>> lambda = () => captured;

        Assert.Equal(5L, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_IndexerRead_StillEvaluatesViaTheCompileFallback()
    {
        var values = new[] { 10, 20, 30 };
        Expression<Func<int>> lambda = () => values[1];

        Assert.Equal(20, ExpressionEvaluator.Evaluate(lambda.Body));
    }

    [Fact]
    public void Evaluate_MemberOnANullInstance_StillThrowsFromTheCompiledPath()
    {
        // The direct read would report TargetException where compiled code reports
        // NullReferenceException, so this shape is deliberately left to the compile fallback - the
        // failure a caller sees must not depend on which route the evaluator took.
        Holder? holder = null;
        Expression<Func<int>> lambda = () => holder!.Property;

        var ex = Assert.ThrowsAny<Exception>(() => ExpressionEvaluator.Evaluate(lambda.Body));

        Assert.IsType<NullReferenceException>(ex.InnerException ?? ex);
    }

    [Fact]
    public void Evaluate_PropertyThatThrows_SurfacesTheOriginalException()
    {
        var holder = new Holder();
        Expression<Func<int>> lambda = () => holder.Throws;

        var ex = Assert.ThrowsAny<Exception>(() => ExpressionEvaluator.Evaluate(lambda.Body));

        Assert.IsType<InvalidOperationException>(ex.InnerException ?? ex);
    }

    // ---------------------------------------------------------------------------
    // The consolidation itself
    // ---------------------------------------------------------------------------

    [Fact]
    public void HavingExpressionHelpers_EvaluateExpression_DelegatesToTheSharedEvaluator()
    {
        // HavingExpressionHelpers held one of the eight copies and keeps its name for the HAVING
        // call sites; it must now give the same answer as the shared implementation.
        int captured = 21;
        Expression<Func<int>> lambda = () => captured;

        Assert.Equal(
            ExpressionEvaluator.Evaluate(lambda.Body),
            HavingExpressionHelpers.EvaluateExpression(lambda.Body));
    }

    // ---------------------------------------------------------------------------

    private static readonly int StaticField = 13;
    private static int StaticProperty => 17;

    private static int Compute(int x) => x * 2;

    private sealed class Holder
    {
        public int Field;
        public int Property { get; set; }
        public Holder? Nested { get; set; }
        public int Throws => throw new InvalidOperationException("boom");
    }
}
