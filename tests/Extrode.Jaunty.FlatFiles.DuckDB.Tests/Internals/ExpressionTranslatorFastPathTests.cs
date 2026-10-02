using System.Linq.Expressions;
using System.Reflection;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class ExpressionTranslatorFastPathTests
{
    private const long FastPathBudgetBytes = 512;

    private static readonly Func<Expression, object?> Evaluate =
        typeof(ExpressionTranslator).GetMethod("EvaluateExpression", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<Expression, object?>>();

    private static readonly int StaticField = 13;

    private static int StaticProperty => 14;

    private sealed class Box
    {
        public int Field = 3;
        public int Property { get; set; } = 4;
        public Box? Next { get; set; }
        private int Hidden { get; } = 6;
        public int WriteOnly { set { } }
        public int this[int index] => index;
        public int Throws => throw new InvalidOperationException("boom");

        public static MemberExpression HiddenOn(Box box) =>
            Expression.Property(Expression.Constant(box), typeof(Box).GetProperty(nameof(Hidden), BindingFlags.NonPublic | BindingFlags.Instance)!);
    }

    private static Box Make() => new() { Field = 7, Property = 8 };

    private static long AllocatedByEvaluating(Expression expression)
    {
        for (int i = 0; i < 5; i++)
            Evaluate(expression);

        long best = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Evaluate(expression);
            best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return best;
    }

    private static void AssertReadDirectly(Expression<Func<int>> read, int expected)
    {
        Assert.Equal(expected, Evaluate(read.Body));
        long allocated = AllocatedByEvaluating(read.Body);
        Assert.True(allocated < FastPathBudgetBytes, $"{read.Body} allocated {allocated} bytes, so it was compiled instead of read");
    }

    [Fact]
    public void ACapturedLocal_IsReadWithoutCompiling()
    {
        int local = 21;
        AssertReadDirectly(() => local, 21);
    }

    [Fact]
    public void AFieldOfACapturedObject_IsReadWithoutCompiling()
    {
        var box = new Box();
        AssertReadDirectly(() => box.Field, 3);
    }

    [Fact]
    public void APropertyOfACapturedObject_IsReadWithoutCompiling()
    {
        var box = new Box();
        AssertReadDirectly(() => box.Property, 4);
    }

    [Fact]
    public void ANestedMemberChain_IsReadWithoutCompiling()
    {
        var box = new Box { Next = new Box { Field = 9 } };
        AssertReadDirectly(() => box.Next!.Field, 9);
    }

    [Fact]
    public void AStaticField_IsReadWithoutCompiling() => AssertReadDirectly(() => StaticField, 13);

    [Fact]
    public void AStaticProperty_IsReadWithoutCompiling() => AssertReadDirectly(() => StaticProperty, 14);

    [Fact]
    public void APropertyWithANonPublicGetter_IsReadWithoutCompiling()
    {
        MemberExpression hidden = Box.HiddenOn(new Box());

        Assert.Equal(6, Evaluate(hidden));
        long allocated = AllocatedByEvaluating(hidden);
        Assert.True(allocated < FastPathBudgetBytes, $"a non-public getter allocated {allocated} bytes, so it was compiled instead of read");
    }

    [Fact]
    public void AFieldOfAMethodCallResult_IsEvaluatedByCompiling()
    {
        Expression<Func<int>> read = () => Make().Field;

        Assert.Equal(7, Evaluate(read.Body));
    }

    [Fact]
    public void APropertyOfAMethodCallResult_IsEvaluatedByCompiling()
    {
        Expression<Func<int>> read = () => Make().Property;

        Assert.Equal(8, Evaluate(read.Body));
    }

    [Fact]
    public void AFieldOfACapturedNull_FailsTheWayCompiledCodeDoes()
    {
        Box? none = null;
        Expression<Func<int>> read = () => none!.Field;

        Assert.Throws<NullReferenceException>(() => Evaluate(read.Body));
    }

    [Fact]
    public void AGetterThatThrows_SurfacesItsOwnException()
    {
        Box box = Make();
        Expression<Func<int>> read = () => box.Throws;

        Assert.Throws<InvalidOperationException>(() => Evaluate(read.Body));
    }

    [Fact]
    public void APropertyOfACapturedNull_FailsTheWayCompiledCodeDoes()
    {
        Box? none = null;
        Expression<Func<int>> read = () => none!.Property;

        Assert.Throws<NullReferenceException>(() => Evaluate(read.Body));
    }

    [Fact]
    public void AnIndexerCannotBeBuiltAsAMemberRead()
    {
        MemberExpression? built = null;
        var indexer = typeof(Box).GetProperty("Item")!;

        Assert.ThrowsAny<ArgumentException>(() => built = Expression.MakeMemberAccess(Expression.Constant(new Box()), indexer));
        Assert.Null(built);
    }

    [Fact]
    public void AWriteOnlyProperty_IsLeftToTheCompiledPath()
    {
        var writeOnly = typeof(Box).GetProperty(nameof(Box.WriteOnly))!;
        MemberExpression read = Expression.MakeMemberAccess(Expression.Constant(new Box()), writeOnly);

        var ex = Assert.Throws<ArgumentException>(() => Evaluate(read));

        Assert.StartsWith("Expression must be readable", ex.Message);
    }

    [Fact]
    public void AMemberThatIsNeitherAFieldNorAPropertyCannotBeBuiltAsAMemberRead()
    {
        var method = typeof(Box).GetMethod(nameof(Make), BindingFlags.NonPublic | BindingFlags.Static)
            ?? typeof(ExpressionTranslatorFastPathTests).GetMethod(nameof(Make), BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.ThrowsAny<ArgumentException>(() => Expression.MakeMemberAccess(null, method));
    }
}
