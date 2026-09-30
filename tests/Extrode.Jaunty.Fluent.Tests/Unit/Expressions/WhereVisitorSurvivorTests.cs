using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

public class WhereVisitorSurvivorTests
{
    private readonly TestDialect _dialect = new();

    public sealed class IntBag : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => new List<int> { 1, 2 }.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static implicit operator ReadOnlySpan<int>(IntBag bag) => new([1, 2]);
    }

    public sealed class Holder
    {
        public int Id { get; set; }

        public bool Flag { get; set; }

        public string? Name { get; set; }
    }

    public sealed class Stamped
    {
        public DateTime At { get; set; }
    }

    private (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<Product, bool>> expr)
        => new WhereExpressionVisitor<Product>(_dialect).Translate(expr);

    private string SqlOf(Expression<Func<Product, bool>> expr) => Translate(expr).Sql;

    [Fact]
    public void ANullSearchValue_NamesTheMethodAndTheParameter()
    {
        string? needle = null;

        var exception = Assert.Throws<ArgumentNullException>(() => Translate(p => p.ProductName.Contains(needle!)));

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("'Contains' was given a null search value, which string.Contains rejects.", exception.Message);
    }

    [Fact]
    public void AContainsOverACapturedMember_IsNotAnInListOnTheColumn()
    {
        var holder = new Holder { Id = 1 };
        int[] ids = [1, 2];

        Assert.Equal("1 = 1", SqlOf(p => ids.Contains(holder.Id)));
    }

    [Fact]
    public void ACapturedBooleanMember_IsItsValueNotAColumn()
    {
        var holder = new Holder { Flag = true };

        var (sql, parameters) = Translate(p => holder.Flag);

        Assert.Equal("1 = 1", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ALengthComparedToAnotherLength_ComparesBothLengths()
        => Assert.Equal(
            "(LEN([product_name]) > LEN([quantity_per_unit]))",
            SqlOf(p => p.ProductName.Length > Sql.Length(p.QuantityPerUnit)));

    [Fact]
    public void TwoFunctionsOfColumns_AreComparedNotTestedForNull()
        => Assert.Equal(
            "(UPPER([product_name]) = LOWER([quantity_per_unit]))",
            SqlOf(p => Sql.Upper(p.ProductName) == Sql.Lower(p.QuantityPerUnit)));

    [Fact]
    public void AConvertedColumn_IsStillTheColumn()
    {
        var (sql, parameters) = Translate(p => (long)p.ProductId == 5L);

        Assert.Equal("([product_id] = @product_id)", sql);
        Assert.Equal([("@product_id", (object?)5L)], parameters);
    }

    [Fact]
    public void ANullBehindAConvert_IsStillNull()
    {
        var p = Expression.Parameter(typeof(Product), "p");
        var body = Expression.Equal(
            Expression.Call(typeof(Sql).GetMethod(nameof(Sql.Upper))!, Expression.Property(p, nameof(Product.ProductName))),
            Expression.Convert(Expression.Constant(null), typeof(string)));

        var (sql, _) = new WhereExpressionVisitor<Product>(_dialect).Translate(Expression.Lambda<Func<Product, bool>>(body, p));

        Assert.Equal("(UPPER([product_name]) IS NULL)", sql);
    }

    [Fact]
    public void ANonNullableDateBehindAConvert_IsTranslatedAsTheColumn()
    {
        var (sql, _) = new WhereExpressionVisitor<Stamped>(_dialect).Translate(s => Sql.Year(s.At) == 2020);

        Assert.Equal("(YEAR([At]) = @Value)", sql);
    }

    [Fact]
    public void ASpanConversionFromAnotherType_IsNotUnwrapped()
    {
        var p = Expression.Parameter(typeof(Product), "p");
        var toSpan = typeof(IntBag).GetMethod("op_Implicit")!;
        var contains = typeof(MemoryExtensions).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
            .MakeGenericMethod(typeof(int));
        var body = Expression.Call(contains, Expression.Call(toSpan, Expression.Constant(new IntBag())), Expression.Property(p, nameof(Product.ProductId)));

        Assert.ThrowsAny<Exception>(() => new WhereExpressionVisitor<Product>(_dialect).Translate(Expression.Lambda<Func<Product, bool>>(body, p)));
    }

    [Fact]
    public void AnotherMemoryExtensionsMethod_IsNotTreatedAsContains()
    {
        var p = Expression.Parameter(typeof(Product), "p");
        var toSpan = typeof(ReadOnlySpan<int>).GetMethod("op_Implicit", [typeof(int[])])!;
        var indexOf = typeof(MemoryExtensions).GetMethods()
            .First(m => m.Name == "IndexOf" && m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType.IsGenericType &&
                        m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>) &&
                        m.GetParameters()[1].ParameterType.IsGenericParameter)
            .MakeGenericMethod(typeof(int));
        var body = Expression.Equal(
            Expression.Call(indexOf, Expression.Call(toSpan, Expression.Constant(new[] { 1, 2 })), Expression.Property(p, nameof(Product.ProductId))),
            Expression.Constant(0));

        Assert.ThrowsAny<Exception>(() => new WhereExpressionVisitor<Product>(_dialect).Translate(Expression.Lambda<Func<Product, bool>>(body, p)));
    }

    private sealed class NullFormatting
    {
        public override string? ToString() => null;
    }

    private static string FormatLength(object? length)
    {
        var method = typeof(WhereExpressionVisitor<Product>).GetMethod("FormatSubstringLength", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return (string)method.Invoke(null, [length])!;
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException!;
        }
    }

    [Fact]
    public void ASubstringLengthOfNull_IsRejected()
        => Assert.Equal(
            "The length argument of Substring(start, length) evaluated to null and cannot be translated to SQL.",
            Assert.Throws<NotSupportedException>(() => FormatLength(null)).Message);

    [Fact]
    public void ASubstringLengthThatFormatsToNull_IsRejected()
        => Assert.Equal(
            "The length argument of Substring(start, length) could not be formatted for SQL.",
            Assert.Throws<NotSupportedException>(() => FormatLength(new NullFormatting())).Message);

    [Fact]
    public void ASubstringLength_FormatsInvariantly()
    {
        Assert.Equal("5", FormatLength(5));
        Assert.Equal("1.5", FormatLength(1.5));
    }
}
