using Jaunty.Scaffolding.CodeGeneration;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class NamingHelperTests
{
    #region ToPascalCase Tests

    [Theory]
    [InlineData("user_name", "UserName")]
    [InlineData("first_name", "FirstName")]
    [InlineData("product_id", "ProductId")]
    [InlineData("order_details", "OrderDetails")]
    [InlineData("created_at", "CreatedAt")]
    public void ToPascalCase_SnakeCase_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user-name", "UserName")]
    [InlineData("first-name", "FirstName")]
    [InlineData("order-id", "OrderId")]
    public void ToPascalCase_KebabCase_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UserName", "UserName")]
    [InlineData("ID", "ID")]
    [InlineData("firstName", "FirstName")]
    [InlineData("productId", "ProductId")]
    public void ToPascalCase_NoDelimiters_CapitalizesFirstAndPreservesRest(string input, string expected)
    {
        var result = NamingHelper.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void ToPascalCase_EmptyOrNull_ReturnsInput(string? input, string? expected)
    {
        var result = NamingHelper.ToPascalCase(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToPascalCase_SingleWord_CapitalizesFirst()
    {
        var result = NamingHelper.ToPascalCase("name");
        Assert.Equal("Name", result);
    }

    [Theory]
    [InlineData("__double__underscore__", "DoubleUnderscore")]
    [InlineData("_leading", "Leading")]
    [InlineData("trailing_", "Trailing")]
    public void ToPascalCase_MultipleDelimiters_HandlesCorrectly(string input, string expected)
    {
        var result = NamingHelper.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region ToCamelCase Tests

    [Theory]
    [InlineData("user_name", "userName")]
    [InlineData("first_name", "firstName")]
    [InlineData("product_id", "productId")]
    public void ToCamelCase_SnakeCase_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.ToCamelCase(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToCamelCase_SingleWord_LowercasesFirst()
    {
        var result = NamingHelper.ToCamelCase("Name");
        Assert.Equal("name", result);
    }

    #endregion

    #region Singularize Tests

    [Theory]
    [InlineData("Products", "Product")]
    [InlineData("Categories", "Category")]
    [InlineData("Orders", "Order")]
    [InlineData("Users", "User")]
    [InlineData("Customers", "Customer")]
    public void Singularize_RegularPlurals_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Companies", "Company")]
    [InlineData("Categories", "Category")]
    [InlineData("Entities", "Entity")]
    [InlineData("Cities", "City")]
    public void Singularize_IesPlurals_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Boxes", "Box")]
    [InlineData("Matches", "Match")]
    [InlineData("Buses", "Bus")]
    [InlineData("Classes", "Class")]
    [InlineData("Wishes", "Wish")]
    public void Singularize_EsPlurals_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("People", "Person")]
    [InlineData("Children", "Child")]
    [InlineData("Men", "Man")]
    [InlineData("Women", "Woman")]
    public void Singularize_IrregularPlurals_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Status", "Status")]
    [InlineData("Address", "Address")]
    [InlineData("Analysis", "Analysis")]
    public void Singularize_WordsEndingInS_DoesNotChange(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Singularize_EmptyOrNull_ReturnsInput(string? input, string? expected)
    {
        var result = NamingHelper.Singularize(input!);
        Assert.Equal(expected, result);
    }

    #endregion

    #region IsValidCSharpIdentifier Tests

    [Theory]
    [InlineData("Name", true)]
    [InlineData("_name", true)]
    [InlineData("name123", true)]
    [InlineData("_123", true)]
    public void IsValidCSharpIdentifier_ValidIdentifiers_ReturnsTrue(string input, bool expected)
    {
        var result = NamingHelper.IsValidCSharpIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123name", false)]
    [InlineData("user-name", false)]
    [InlineData("user name", false)]
    [InlineData("", false)]
    public void IsValidCSharpIdentifier_InvalidIdentifiers_ReturnsFalse(string input, bool expected)
    {
        var result = NamingHelper.IsValidCSharpIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("class", false)]
    [InlineData("public", false)]
    [InlineData("string", false)]
    [InlineData("int", false)]
    [InlineData("return", false)]
    public void IsValidCSharpIdentifier_Keywords_ReturnsFalse(string input, bool expected)
    {
        var result = NamingHelper.IsValidCSharpIdentifier(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region EscapeIdentifier Tests

    [Theory]
    [InlineData("Name", "Name")]
    [InlineData("_name", "_name")]
    [InlineData("name123", "name123")]
    public void EscapeIdentifier_ValidIdentifiers_ReturnsUnchanged(string input, string expected)
    {
        var result = NamingHelper.EscapeIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123name", "_123name")]
    [InlineData("1st", "_1st")]
    public void EscapeIdentifier_StartsWithDigit_PrefixesUnderscore(string input, string expected)
    {
        var result = NamingHelper.EscapeIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user-name", "user_name")]
    [InlineData("user name", "user_name")]
    public void EscapeIdentifier_InvalidChars_ReplacesWithUnderscore(string input, string expected)
    {
        var result = NamingHelper.EscapeIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("class", "@class")]
    [InlineData("public", "@public")]
    [InlineData("string", "@string")]
    [InlineData("int", "@int")]
    public void EscapeIdentifier_Keywords_PrefixesAtSign(string input, string expected)
    {
        var result = NamingHelper.EscapeIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EscapeIdentifier_EmptyString_ReturnsUnderscore()
    {
        var result = NamingHelper.EscapeIdentifier("");
        Assert.Equal("_", result);
    }

    #endregion

    #region ToClassName Tests

    [Fact]
    public void ToClassName_NoOptions_ConvertsTableNameToPascalCase()
    {
        var result = NamingHelper.ToClassName("order_details", singularize: false, classPrefix: null, classSuffix: null);
        Assert.Equal("OrderDetails", result);
    }

    [Fact]
    public void ToClassName_Singularize_SingularizesAfterPascalCase()
    {
        var result = NamingHelper.ToClassName("products", singularize: true, classPrefix: null, classSuffix: null);
        Assert.Equal("Product", result);
    }

    [Fact]
    public void ToClassName_WithPrefixAndSuffix_AppliesBothAroundPascalCaseName()
    {
        var result = NamingHelper.ToClassName("products", singularize: false, classPrefix: "Db", classSuffix: "Entity");
        Assert.Equal("DbProductsEntity", result);
    }

    [Fact]
    public void ToClassName_ResultStartsWithDigit_PrefixesWithUnderscore()
    {
        var result = NamingHelper.ToClassName("123name", singularize: false, classPrefix: null, classSuffix: null);
        Assert.Equal("_123name", result);
    }

    #endregion
}