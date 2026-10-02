using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

public class SelectExistsSurvivorTests
{
    private readonly TestDialect _dialect = new();

    private List<(string, string)> Cols<TResult>(Expression<Func<Product, TResult>> expr)
        => new SelectExpressionVisitor<Product>(_dialect).Translate(expr).Select(c => (c.Sql, c.Alias)).ToList();

    [Fact]
    public void AQuotedLambdaInAProjection_TranslatesItsBody()
    {
        ParameterExpression x = Expression.Parameter(typeof(Product), "x");
        Expression<Func<Product, string>> inner = Expression.Lambda<Func<Product, string>>(Expression.Property(x, nameof(Product.ProductName)), x);
        ParameterExpression p = Expression.Parameter(typeof(Product), "p");
        Expression<Func<Product, object>> outer = Expression.Lambda<Func<Product, object>>(Expression.Convert(Expression.Quote(inner), typeof(object)), p);

        Assert.Equal([("[product_name]", "Value")], Cols(outer));
    }

    [Fact]
    public void ABoxedPartitionKey_IsUnwrappedToTheColumn()
        => Assert.Equal(
            [("ROW_NUMBER()OVER (PARTITION BY [product_id] )", "R")],
            Cols(p => new { R = (long)Sql.RowNumber<Product>().PartitionBy<object>(x => x.ProductId) }));

    [Fact]
    public void ABoxedOrderKey_IsUnwrappedToTheColumn()
        => Assert.Equal(
            [("ROW_NUMBER()OVER (ORDER BY [product_id] ASC )", "R")],
            Cols(p => new { R = (long)Sql.RowNumber<Product>().OrderBy<object>(x => x.ProductId) }));
}
