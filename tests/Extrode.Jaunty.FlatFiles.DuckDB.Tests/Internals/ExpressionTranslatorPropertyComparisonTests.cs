using System.Linq.Expressions;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class ExpressionTranslatorPropertyComparisonTests
{
    [Fact]
    public void PropertyToPropertyComparison_ThrowsNotSupported()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == x.Quantity;

        var ex = Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));

        Assert.Contains("Property-to-property", ex.Message);
    }

    [Fact]
    public void StringMethodTakingAnotherProperty_ThrowsNotSupported()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.StartsWith(x.Region!);

        Assert.Throws<NotSupportedException>(() => ExpressionTranslator.Translate(predicate));
    }

    [Fact]
    public void AComputedValueUsingANestedLambda_StillEvaluates()
    {
        int[] values = [1, 5, 9];
        Expression<Func<SalesRecord, bool>> predicate = x => x.Quantity == values.Count(v => v > 2);

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Equal("\"Quantity\" = $1", sql);
        Assert.Equal(2, Assert.Single(parameters).Value);
    }
}
