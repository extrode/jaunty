using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class ExpressionTranslatorTests
{
    // AUD-R12-127: entity scoped to this file's column-name-escaping regression tests. The
    // embedded double quote in each [Column] name is the exact character ExpressionTranslator's
    // WHERE-clause interpolation used to fail to double, unlike DuckDbDialect.EscapeColumnName.
    public class ColumnEscapingEntity
    {
        [Column("bad\"name")]
        public int? BadName { get; set; }

        [Column("bad\"flag")]
        public bool BadFlag { get; set; }

        [Column("bad\"text")]
        public string? BadText { get; set; }
    }

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
    public void Translate_RelationalCompareAgainstNullValue_GeneratesMatchNothing()
    {
        decimal? min = null;
        Expression<Func<SalesRecord, bool>> predicate = x => x.Revenue > min;

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("1 = 0", sql);
        Assert.DoesNotContain("IS NOT NULL", sql);
        Assert.Empty(parameters);
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

    [Fact]
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
    public void Translate_StringContains_EscapesLikeClauseAndAddsEscapeKeyword()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.Contains("100%_off");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("ESCAPE '\\'", sql);
        Assert.Single(parameters);
        // The literal % and _ in the value must be escaped so they match literally rather than
        // acting as LIKE wildcards.
        Assert.Equal("%100\\%\\_off%", parameters[0].Value);
    }

    [Fact]
    public void Translate_StringStartsWith_EscapesLikeClauseAndAddsEscapeKeyword()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.StartsWith("50%_discount");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("ESCAPE '\\'", sql);
        Assert.Equal("50\\%\\_discount%", parameters[0].Value);
    }

    [Fact]
    public void Translate_StringEndsWith_EscapesLikeClauseAndAddsEscapeKeyword()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.EndsWith("a_b%c");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("ESCAPE '\\'", sql);
        Assert.Equal("%a\\_b\\%c", parameters[0].Value);
    }

    [Fact]
    public void Translate_StringContains_EscapesLiteralBackslash()
    {
        // Arrange
        Expression<Func<SalesRecord, bool>> predicate = x => x.ProductName.Contains(@"C:\temp");

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Equal(@"%C:\\temp%", parameters[0].Value);
    }

    [Fact]
    public void Translate_InClause_EmptyCollection_GeneratesAlwaysFalseInsteadOfInvalidSql()
    {
        // Arrange
        var ids = Array.Empty<int>();
        Expression<Func<SalesRecord, bool>> predicate = x => ids.Contains(x.Id);

        // Act
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Equal("1 = 0", sql);
        Assert.Empty(parameters);
        Assert.DoesNotContain("IN ()", sql);
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

    [Fact]
    public void Translate_EqualsOperator_ColumnNameWithEmbeddedQuote_EscapesColumnName()
    {
        // Arrange
        Expression<Func<ColumnEscapingEntity, bool>> predicate = x => x.BadName == 1;

        // Act
        var (sql, _) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"bad\"\"name\" = $1", sql);
    }

    [Fact]
    public void Translate_NullCheck_ColumnNameWithEmbeddedQuote_EscapesColumnName()
    {
        // Arrange
        Expression<Func<ColumnEscapingEntity, bool>> predicate = x => x.BadText == null;

        // Act
        var (sql, _) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"bad\"\"text\" IS NULL", sql);
    }

    [Fact]
    public void Translate_BoolMember_ColumnNameWithEmbeddedQuote_EscapesColumnName()
    {
        // Arrange
        Expression<Func<ColumnEscapingEntity, bool>> predicate = x => x.BadFlag;

        // Act
        var (sql, _) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"bad\"\"flag\" = true", sql);
    }

    [Fact]
    public void Translate_StringStartsWith_ColumnNameWithEmbeddedQuote_EscapesColumnName()
    {
        // Arrange
        Expression<Func<ColumnEscapingEntity, bool>> predicate = x => x.BadText!.StartsWith("W");

        // Act
        var (sql, _) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"bad\"\"text\" LIKE", sql);
    }

    [Fact]
    public void Translate_InClause_ColumnNameWithEmbeddedQuote_EscapesColumnName()
    {
        // Arrange
        var ids = new List<int?> { 1, 2, 3 };
        Expression<Func<ColumnEscapingEntity, bool>> predicate = x => ids.Contains(x.BadName);

        // Act
        var (sql, _) = ExpressionTranslator.Translate(predicate);

        // Assert
        Assert.Contains("\"bad\"\"name\" IN (", sql);
    }

    // AUD-R33-001: ExtractColumnAndValue swaps the operands when the entity member is on the
    // right, but the operator used to be emitted from binary.NodeType unchanged, so every
    // constant-on-left relational predicate produced the exact inverse of what was written.
    [Fact]
    public void Translate_ConstantOnLeftLessThan_MirrorsToGreaterThan()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => 1000 < x.Revenue;

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("\"Revenue\" > $1", sql);
        Assert.Single(parameters);
        Assert.Equal(1000m, parameters[0].Value);
    }

    [Fact]
    public void Translate_ConstantOnLeftGreaterThan_MirrorsToLessThan()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => 1000 > x.Revenue;

        var (sql, _) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("\"Revenue\" < $1", sql);
    }

    [Fact]
    public void Translate_ConstantOnLeftLessThanOrEqual_MirrorsToGreaterThanOrEqual()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => 1000 <= x.Revenue;

        var (sql, _) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("\"Revenue\" >= $1", sql);
    }

    [Fact]
    public void Translate_ConstantOnLeftGreaterThanOrEqual_MirrorsToLessThanOrEqual()
    {
        Expression<Func<SalesRecord, bool>> predicate = x => 1000 >= x.Revenue;

        var (sql, _) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("\"Revenue\" <= $1", sql);
    }

    [Fact]
    public void Translate_ConstantOnLeftEquality_IsUnchangedBecauseItIsSymmetric()
    {
        Expression<Func<SalesRecord, bool>> equal = x => 1 == x.Id;
        Expression<Func<SalesRecord, bool>> notEqual = x => 1 != x.Id;

        var (equalSql, _) = ExpressionTranslator.Translate(equal);
        var (notEqualSql, _) = ExpressionTranslator.Translate(notEqual);

        Assert.Contains("\"Id\" = $1", equalSql);
        Assert.Contains("\"Id\" != $1", notEqualSql);
    }

    [Fact]
    public void Translate_ConstantOnLeftNullEquality_StillEmitsIsNull()
    {
        string? nothing = null;
        Expression<Func<SalesRecord, bool>> predicate = x => nothing == x.Region;

        var (sql, parameters) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("\"region\" IS NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translate_ConstantOnLeftRelationalAgainstNull_StillMatchesNothing()
    {
        decimal? nothing = null;
        Expression<Func<SalesRecord, bool>> predicate = x => nothing < x.Revenue;

        var (sql, _) = ExpressionTranslator.Translate(predicate);

        Assert.Contains("1 = 0", sql);
    }
}