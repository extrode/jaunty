using Jaunty.Internals.Parameters;

namespace Jaunty.Tests.Unit;

public class SqlParameterParserTests
{
    #region Basic Parameter Extraction

    [Fact]
    public void ExtractParameterNames_SingleParameter_ReturnsName()
    {
        var sql = "SELECT * FROM products WHERE product_id = @ProductId";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("ProductId", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_MultipleParameters_ReturnsAllInOrder()
    {
        var sql = "SELECT * FROM products WHERE product_name = @Name AND unit_price > @Price";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Equal(2, names.Length);
        Assert.Equal("Name", names[0]);
        Assert.Equal("Price", names[1]);
    }

    [Fact]
    public void ExtractParameterNames_DuplicateParameters_ReturnsAll()
    {
        var sql = "SELECT * FROM products WHERE product_name = @Name OR supplier_id = @Name";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Equal(2, names.Length);
        Assert.Equal("Name", names[0]);
        Assert.Equal("Name", names[1]);
    }

    [Fact]
    public void ExtractParameterNames_NoParameters_ReturnsEmpty()
    {
        var sql = "SELECT * FROM products";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Empty(names);
    }

    #endregion

    #region Comment Handling

    [Fact]
    public void ExtractParameterNames_ParameterInSingleLineComment_Ignored()
    {
        var sql = @"SELECT * FROM products 
-- WHERE product_id = @Ignored
WHERE product_name = @Name";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Name", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterInBlockComment_Ignored()
    {
        var sql = "SELECT * FROM products /* @Ignored */ WHERE product_name = @Name";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Name", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_MultiLineBlockComment_Ignored()
    {
        var sql = @"SELECT * FROM products 
/* 
  @Ignored1
  @Ignored2
*/
WHERE product_name = @Name";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Name", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_CommentAtEndOfLine_Ignored()
    {
        var sql = "SELECT * FROM products WHERE id = @Id -- @Ignored comment";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_NestedLookingComments_HandledCorrectly()
    {
        var sql = "SELECT * FROM products /* outer /* not nested */ WHERE id = @Id";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    #endregion

    #region String Literal Handling

    [Fact]
    public void ExtractParameterNames_ParameterInStringLiteral_Ignored()
    {
        var sql = "SELECT * FROM products WHERE product_name = '@NotAParam' AND category_id = @CategoryId";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("CategoryId", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_EscapedQuoteInString_HandledCorrectly()
    {
        var sql = "SELECT * FROM products WHERE product_name = 'It''s @NotThis' AND category_id = @CategoryId";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("CategoryId", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_MultipleEscapedQuotes_HandledCorrectly()
    {
        var sql = "SELECT * FROM products WHERE product_name = 'Test''s ''value''' AND id = @Id";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_EmptyString_Ignored()
    {
        var sql = "SELECT * FROM products WHERE product_name = '' AND id = @Id";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    #endregion

    #region Quoted Identifier Handling

    [Fact]
    public void ExtractParameterNames_ParameterInQuotedIdentifier_Ignored()
    {
        var sql = "SELECT * FROM products WHERE \"@Column\" = @Value";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Value", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterInBracketedIdentifier_Ignored()
    {
        var sql = "SELECT * FROM products WHERE [@Column] = @Value";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Value", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_EscapedBracket_HandledCorrectly()
    {
        var sql = "SELECT * FROM [Table]] Name] WHERE id = @Id";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    #endregion

    #region Parameter Name Characters

    [Fact]
    public void ExtractParameterNames_ParameterWithUnderscore_Extracted()
    {
        var sql = "SELECT * FROM products WHERE product_id = @product_id";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("product_id", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterWithNumbers_Extracted()
    {
        var sql = "SELECT * FROM products WHERE id = @param1 AND name = @param2";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Equal(2, names.Length);
        Assert.Equal("param1", names[0]);
        Assert.Equal("param2", names[1]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterStartingWithNumber_ExtractsCorrectly()
    {
        // @1param is valid - starts extracting from 1
        var sql = "SELECT * FROM products WHERE id = @1param";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("1param", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterWithMixedCase_PreservesCase()
    {
        var sql = "SELECT * FROM products WHERE id = @ProductID AND name = @productName";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Equal(2, names.Length);
        Assert.Equal("ProductID", names[0]);
        Assert.Equal("productName", names[1]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ExtractParameterNames_EmptySql_ReturnsEmpty()
    {
        var names = SqlParameterParser.ExtractParameterNames("");

        Assert.Empty(names);
    }

    [Fact]
    public void ExtractParameterNames_OnlyAtSymbol_ReturnsEmpty()
    {
        var sql = "SELECT * FROM products WHERE @ ";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Empty(names);
    }

    [Fact]
    public void ExtractParameterNames_AtSymbolAtEnd_ReturnsEmpty()
    {
        var sql = "SELECT * FROM products WHERE id = @";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Empty(names);
    }

    [Fact]
    public void ExtractParameterNames_ConsecutiveAtSymbols_HandlesCorrectly()
    {
        var sql = "SELECT * FROM products WHERE id = @@Identity"; // SQL Server system variable

        var names = SqlParameterParser.ExtractParameterNames(sql);

        // @@Identity is a SQL Server global/system variable, not a bindable parameter,
        // so the whole @@identifier token must be skipped rather than treated as @Identity.
        Assert.Empty(names);
    }

    [Fact]
    public void ExtractParameterNames_SystemVariableFollowedByRealParameter_ExtractsOnlyRealParameter()
    {
        var sql = "SELECT @@ROWCOUNT, * FROM products WHERE id = @Id";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Single(names);
        Assert.Equal("Id", names[0]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterFollowedByOperator_ExtractsCorrectly()
    {
        var sql = "SELECT * FROM products WHERE price>@MinPrice AND price<@MaxPrice";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Equal(2, names.Length);
        Assert.Equal("MinPrice", names[0]);
        Assert.Equal("MaxPrice", names[1]);
    }

    [Fact]
    public void ExtractParameterNames_ParameterInParentheses_ExtractsCorrectly()
    {
        var sql = "SELECT * FROM products WHERE id IN (@Id1, @Id2, @Id3)";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Equal(3, names.Length);
        Assert.Equal("Id1", names[0]);
        Assert.Equal("Id2", names[1]);
        Assert.Equal("Id3", names[2]);
    }

    [Fact]
    public void ExtractParameterNames_UnterminatedString_HandlesGracefully()
    {
        var sql = "SELECT * FROM products WHERE name = 'unterminated";

        // Should not throw, just handle gracefully
        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Empty(names);
    }

    [Fact]
    public void ExtractParameterNames_UnterminatedBlockComment_HandlesGracefully()
    {
        var sql = "SELECT * FROM products /* unterminated";

        var names = SqlParameterParser.ExtractParameterNames(sql);

        Assert.Empty(names);
    }

    #endregion
}