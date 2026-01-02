using Jaunty.Configuration;

namespace Jaunty.Tests.Unit;

public class NamingConventionTests
{
    [Theory]
    [InlineData("ProductName", "product_name")]
    [InlineData("Id", "id")]
    [InlineData("ProductID", "product_i_d")]
    [InlineData("XMLParser", "x_m_l_parser")]
    [InlineData("firstName", "first_name")]
    [InlineData("", "")]
    public void ToSnakeCase_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingConvention.ToSnakeCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Product", "Products")]
    [InlineData("Category", "Categories")]
    [InlineData("Class", "Classes")]
    [InlineData("Box", "Boxes")]
    [InlineData("Church", "Churches")]
    [InlineData("Brush", "Brushes")]
    [InlineData("Day", "Days")]
    public void Pluralize_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingConvention.Pluralize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SnakeCasePluralTable_CombinesCorrectly()
    {
        var resolver = NamingConvention.SnakeCasePluralTable;
        
        var result = resolver(typeof(TestProduct));
        
        Assert.Equal("test_products", result);
    }

    private class TestProduct { }
}
