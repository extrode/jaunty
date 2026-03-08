using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class ExpressionTranslatorTests
{
    [Fact]
    public void Translate_EqualsOperator_GeneratesEqualsSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.Id == 1;

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"Id\" = $1", sql);
        Assert.Single(parameters);
        Assert.Equal(1, parameters[0].Value);
    }

    [Fact]
    public void Translate_GreaterThanOperator_GeneratesCorrectSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.Revenue > 1000;

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"Revenue\" > $1", sql);
        Assert.Single(parameters);
        Assert.Equal(1000m, parameters[0].Value);
    }

    [Fact]
    public void Translate_AndOperator_GeneratesAndSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.Revenue > 1000 && x.Quantity > 5;

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("AND", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Translate_OrOperator_GeneratesOrSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.Revenue > 1000 || x.Quantity > 5;

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("OR", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Translate_StringContains_GeneratesLikeSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.Contains("Widget");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        Assert.Equal("%Widget%", parameters[0].Value);
    }

    [Fact]
    public void Translate_StringStartsWith_GeneratesLikeSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.StartsWith("W");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        Assert.Equal("W%", parameters[0].Value);
    }

    [Fact]
    public void Translate_StringEndsWith_GeneratesLikeSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.EndsWith("t");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        Assert.Equal("%t", parameters[0].Value);
    }

    [Fact]
    public void Translate_NullCheck_GeneratesIsNullSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.Region == null;

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("IS NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translate_NotNullCheck_GeneratesIsNotNullSql()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.Region != null;

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("IS NOT NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact(Skip = "IN clause translation needs implementation")]
    public void Translate_InClause_GeneratesInSql()
    {
        // Arrange
        var ids = new[] { 1, 2, 3 };
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Contains(x.Id);

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("IN", sql);
        Assert.Equal(3, parameters.Count);
    }

    [Fact]
    public void ResolveColumnName_RespectsColumnAttribute()
    {
        // Arrange
        Expression<Func<SalesRecord, object>> selector = x => x.ProductName;

        // Act
        var columnName = ExpressionTranslator.ResolveColumnName(selector);

        // Assert
        Assert.Equal("product_name", columnName);
    }
}