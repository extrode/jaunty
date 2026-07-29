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

    // R16: the -ves branch had no coverage at all, which is why it wrongly mapped "aves" words
    // (e.g. Leaves) to the -fe form instead of -f (Leafe instead of Leaf) - only "ives" words take
    // the -fe form.
    [Theory]
    [InlineData("Knives", "Knife")]
    [InlineData("Wives", "Wife")]
    [InlineData("Leaves", "Leaf")]
    [InlineData("Calves", "Calf")]
    [InlineData("Wolves", "Wolf")]
    [InlineData("Shelves", "Shelf")]
    [InlineData("Halves", "Half")]
    [InlineData("Thieves", "Thief")]
    [InlineData("Loaves", "Loaf")]
    [InlineData("Selves", "Self")]
    [InlineData("Elves", "Elf")]
    [InlineData("Lives", "Life")]
    [InlineData("Scarves", "Scarf")]
    [InlineData("Hooves", "Hoof")]
    public void Singularize_VesPlurals_ConvertsCorrectly(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    // R24: the -ves branch used to alternate f/fe for *every* word ending in -ves, so the far
    // larger class of ordinary -ve nouns scaffolded to nonsense class names (a table named
    // "Drives" produced "Drife"). These must singularize with the plain -s rule instead.
    [Theory]
    [InlineData("Waves", "Wave")]
    [InlineData("Curves", "Curve")]
    [InlineData("Archives", "Archive")]
    [InlineData("Drives", "Drive")]
    [InlineData("Moves", "Move")]
    [InlineData("Reserves", "Reserve")]
    [InlineData("Valves", "Valve")]
    [InlineData("Olives", "Olive")]
    [InlineData("Sleeves", "Sleeve")]
    public void Singularize_RegularVePlurals_KeepsTheVe(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    // The f/fe alternation still has to fire on the trailing segment of a PascalCase compound,
    // which is how a real table name like "book_shelves" reaches Singularize.
    [Theory]
    [InlineData("BookShelves", "BookShelf")]
    [InlineData("PenKnives", "PenKnife")]
    [InlineData("HouseWives", "HouseWife")]
    [InlineData("MapleLeaves", "MapleLeaf")]
    public void Singularize_CompoundVesPlurals_ConvertsTrailingSegment(string input, string expected)
    {
        var result = NamingHelper.Singularize(input);
        Assert.Equal(expected, result);
    }

    // AUD-R26: the old "-ses (but not -sses) -> strip -es" rule could not tell Bus+es from
    // Database+s - both end "...ses" - and stripped two characters from every one of them. These
    // are ordinary table names, and every one of them scaffolded to a nonsense class name.
    [Theory]
    [InlineData("Databases", "Database")]
    [InlineData("Cases", "Case")]
    [InlineData("Purchases", "Purchase")]
    [InlineData("Licenses", "License")]
    [InlineData("Warehouses", "Warehouse")]
    [InlineData("Expenses", "Expense")]
    [InlineData("Responses", "Response")]
    [InlineData("Courses", "Course")]
    [InlineData("Houses", "House")]
    [InlineData("Releases", "Release")]
    [InlineData("Phases", "Phase")]
    [InlineData("Leases", "Lease")]
    [InlineData("Clauses", "Clause")]
    [InlineData("Causes", "Cause")]
    public void Singularize_SeNouns_KeepTheirE(string input, string expected)
        => Assert.Equal(expected, NamingHelper.Singularize(input));

    // The other half of the same rule: singulars that really do end in a sibilant still lose the
    // -es. Narrowing the rule to "-uses" would not have worked - Houses, Warehouses, Clauses and
    // Causes all end in -uses too - so this closed class is enumerated.
    [Theory]
    [InlineData("Buses", "Bus")]
    [InlineData("Statuses", "Status")]
    [InlineData("Campuses", "Campus")]
    [InlineData("Bonuses", "Bonus")]
    [InlineData("Viruses", "Virus")]
    [InlineData("Gases", "Gas")]
    [InlineData("Lenses", "Lens")]
    [InlineData("Aliases", "Alias")]
    [InlineData("Atlases", "Atlas")]
    [InlineData("Surpluses", "Surplus")]
    public void Singularize_SibilantSingulars_StillLoseTheEs(string input, string expected)
        => Assert.Equal(expected, NamingHelper.Singularize(input));

    // AUD-R26: the -ches rule is right for the large -ch class and truncated the small -che one.
    [Theory]
    [InlineData("Caches", "Cache")]
    [InlineData("Niches", "Niche")]
    [InlineData("Aches", "Ache")]
    public void Singularize_CheNouns_KeepTheirE(string input, string expected)
        => Assert.Equal(expected, NamingHelper.Singularize(input));

    [Theory]
    [InlineData("Branches", "Branch")]
    [InlineData("Matches", "Match")]
    [InlineData("Batches", "Batch")]
    [InlineData("Searches", "Search")]
    [InlineData("Watches", "Watch")]
    [InlineData("Patches", "Patch")]
    public void Singularize_ChNouns_StillLoseTheEs(string input, string expected)
        => Assert.Equal(expected, NamingHelper.Singularize(input));

    // AUD-R26: a bare -zes rule truncated effectively the whole -ze class. Requiring -zzes fixes
    // them and still handles the doubled-z plurals.
    [Theory]
    [InlineData("Sizes", "Size")]
    [InlineData("Prizes", "Prize")]
    [InlineData("Bronzes", "Bronze")]
    [InlineData("Buzzes", "Buzz")]
    [InlineData("Quizzes", "Quiz")]
    public void Singularize_ZPlurals_AreHandledByDoubledZOnly(string input, string expected)
        => Assert.Equal(expected, NamingHelper.Singularize(input));

    // The closed classes must fire on the trailing PascalCase segment too, which is how a real
    // table name like "order_statuses" reaches Singularize.
    [Theory]
    [InlineData("OrderStatuses", "OrderStatus")]
    [InlineData("QueryCaches", "QueryCache")]
    [InlineData("UserAliases", "UserAlias")]
    public void Singularize_CompoundClosedClassPlurals_ConvertTrailingSegment(string input, string expected)
        => Assert.Equal(expected, NamingHelper.Singularize(input));

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