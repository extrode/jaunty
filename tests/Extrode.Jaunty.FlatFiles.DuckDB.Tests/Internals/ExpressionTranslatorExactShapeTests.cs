using System.Linq.Expressions;
using System.Reflection;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Exact SQL, parameter numbering and exact rejection messages from <c>ExpressionTranslator</c>,
/// including the span-based <c>MemoryExtensions.Contains</c> shape a C# 14 compiler emits for
/// <c>array.Contains(x)</c>, which the pinned language version here never produces by itself.
/// </summary>
public class ExpressionTranslatorExactShapeTests
{
    public class Holder
    {
        public int Field;
        public int Prop { get; set; }
    }

    [Fact]
    public void InClause_OverAnArray_NumbersEachValueFromTheOffset()
    {
        int[] ids = [4, 5, 6];
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Contains(x.Id);

        var (sql, parameters) = ExpressionTranslator.Translate(predicate, paramOffset: 2);

        Assert.Equal("\"Id\" IN ($3, $4, $5)", sql);
        Assert.Equal(new object?[] { 4, 5, 6 }, parameters.Select(p => p.Value));
    }

    [Fact]
    public void InClause_OverAList_UsesTheInstanceContains()
    {
        var ids = new List<int> { 7 };
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Contains(x.Id);

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal("\"Id\" IN ($1)", sql);
        Assert.Equal(7, Assert.Single(parameters).Value);
    }

    [Fact]
    public void InClause_ViaMemoryExtensionsOverASpanConversion_UnwrapsToTheArray()
    {
        int[] ids = [1, 2];
        ParameterExpression x = Expression.Parameter(typeof(SalesRecord), "x");
        MethodInfo toSpan = typeof(ReadOnlySpan<int>).GetMethod("op_Implicit", [typeof(int[])])!;
        MethodInfo contains = typeof(MemoryExtensions).GetMethods()
            .Single(m => m.Name == "Contains" && m.GetParameters().Length == 2 &&
                         m.GetParameters()[0].ParameterType.IsGenericType &&
                         m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
            .MakeGenericMethod(typeof(int));
        var body = Expression.Call(
            contains,
            Expression.Call(toSpan, Expression.Constant(ids)),
            Expression.Property(x, nameof(SalesRecord.Id)));
        var predicate = Expression.Lambda<Func<SalesRecord, bool>>(body, x);

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal("\"Id\" IN ($1, $2)", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void InClause_WithoutAnEntityMember_IsRejected()
    {
        var ids = new List<int> { 1 };
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Contains(5);

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("IN clause requires a property access on the entity.", ex.Message);
    }

    [Fact]
    public void InClause_OverANullCollection_IsRejected()
    {
        List<int>? ids = null;
        Expression<Func<SalesRecord, bool>> predicate = x => ids!.Contains(x.Id);

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("IN clause requires an enumerable collection.", ex.Message);
    }

    [Fact]
    public void Contains_OnAGenericTypeOtherThanList_IsRejected()
    {
        var ids = new HashSet<int> { 1 };
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Contains(x.Id);

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("Method 'Contains' is not supported in flat file predicates.", ex.Message);
    }

    [Fact]
    public void AnotherEnumerableMethod_IsRejected()
    {
        int[] ids = [1];
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Any(i => i == x.Id);

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("Method 'Any' is not supported in flat file predicates.", ex.Message);
    }

    [Fact]
    public void AnUnsupportedStringMethod_IsRejected()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.Equals("a");

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("String method 'Equals' is not supported.", ex.Message);
    }

    [Fact]
    public void AnUnsupportedExpressionType_IsRejected()
    {
        var x = Expression.Parameter(typeof(SalesRecord), "x");
        var predicate = Expression.Lambda<Func<SalesRecord, bool>>(Expression.Constant(true), x);

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("Expression type 'Constant' is not supported in flat file predicates.", ex.Message);
    }

    [Fact]
    public void AnUnsupportedBinaryOperator_IsRejected()
    {
        Expression<Func<InventoryItem, bool>> predicate = x => x.InStock ^ true;

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("Binary operator 'ExclusiveOr' is not supported.", ex.Message);
    }

    [Fact]
    public void AComparisonWithNoEntityMember_IsRejected()
    {
        int a = 1;
        Expression<Func<SalesRecord, bool>> predicate = x => a == 1;

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Equal("Binary comparison must have at least one property access on the entity.", ex.Message);
    }

    [Fact]
    public void StringContains_WithANullArgument_BindsAnEmptyPattern()
    {
        string? value = null;
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.Contains(value!);

        var (_, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal("%%", Assert.Single(parameters).Value);
    }

    [Fact]
    public void AFieldOfANullHolder_FailsTheSameWayCompiledCodeWould()
    {
        Holder? holder = null;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == holder!.Field;

        Assert.Throws<NullReferenceException>(() => ExpressionTranslator.Translate(predicate));
    }

    [Fact]
    public void APropertyOfANullHolder_FailsTheSameWayCompiledCodeWould()
    {
        Holder? holder = null;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == holder!.Prop;

        Assert.Throws<NullReferenceException>(() => ExpressionTranslator.Translate(predicate));
    }
}
