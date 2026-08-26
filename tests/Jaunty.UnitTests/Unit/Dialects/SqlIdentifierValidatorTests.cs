using Jaunty.Dialects;

namespace Jaunty.Tests.Unit.Dialects;

public class SqlIdentifierValidatorTests
{
    // Enum.GetValues<T>() is net5.0+; this suite also targets net472.
    private static readonly SqlIdentifierFlavor[] AllFlavors = new[]
    {
        SqlIdentifierFlavor.Common,
        SqlIdentifierFlavor.SqlServer,
        SqlIdentifierFlavor.MySql,
        SqlIdentifierFlavor.PostgreSql,
    };

    [Theory]
    [InlineData("ProductName")]
    [InlineData("_leading_underscore")]
    [InlineData("with9digits")]
    [InlineData("A")]
    public void APlainIdentifierIsAcceptedByEveryFlavour(string identifier)
    {
        foreach (SqlIdentifierFlavor flavor in AllFlavors)
            Assert.True(SqlIdentifierValidator.IsValid(identifier, flavor), $"{identifier} / {flavor}");
    }

    [Theory]
    [InlineData("preço")]
    [InlineData("名前")]
    [InlineData("Ünit")]
    [InlineData("Ναι")]
    public void ANonAsciiLetterIsAcceptedByEveryFlavour(string identifier)
    {
        foreach (SqlIdentifierFlavor flavor in AllFlavors)
            Assert.True(SqlIdentifierValidator.IsValid(identifier, flavor), $"{identifier} / {flavor}");
    }

    [Theory]
    [InlineData("#temp")]
    [InlineData("#")]
    [InlineData("has$dollar")]
    [InlineData("has#hash")]
    public void SqlServerAcceptsHashAndDollarAndTheOthersDoNot(string identifier)
    {
        Assert.True(SqlIdentifierValidator.IsValid(identifier, SqlIdentifierFlavor.SqlServer));
        Assert.False(SqlIdentifierValidator.IsValid(identifier, SqlIdentifierFlavor.Common));
    }

    [Theory]
    [InlineData("has$dollar")]
    [InlineData("$leading")]
    [InlineData("1st_column")]
    public void MySqlAcceptsDollarAnywhereAndALeadingDigit(string identifier)
    {
        Assert.True(SqlIdentifierValidator.IsValid(identifier, SqlIdentifierFlavor.MySql));
        Assert.False(SqlIdentifierValidator.IsValid(identifier, SqlIdentifierFlavor.Common));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("42")]
    public void MySqlRejectsAnAllDigitIdentifier(string identifier)
        => Assert.False(SqlIdentifierValidator.IsValid(identifier, SqlIdentifierFlavor.MySql));

    [Fact]
    public void PostgreSqlAcceptsDollarButNotLeading()
    {
        Assert.True(SqlIdentifierValidator.IsValid("has$dollar", SqlIdentifierFlavor.PostgreSql));
        Assert.False(SqlIdentifierValidator.IsValid("$leading", SqlIdentifierFlavor.PostgreSql));
        Assert.False(SqlIdentifierValidator.IsValid("1st_column", SqlIdentifierFlavor.PostgreSql));
    }

    [Theory]
    [InlineData("has\"quote")]
    [InlineData("has'quote")]
    [InlineData("has`backtick")]
    [InlineData("has[bracket")]
    [InlineData("has]bracket")]
    [InlineData("has;semicolon")]
    [InlineData("has space")]
    [InlineData("has\tTab")]
    [InlineData("has\nnewline")]
    [InlineData("has\rcarriagereturn")]
    [InlineData("has-hyphen")]
    [InlineData("has.dot")]
    [InlineData("has(paren)")]
    [InlineData("has,comma")]
    [InlineData("has*star")]
    [InlineData("has/slash")]
    [InlineData("has\\backslash")]
    [InlineData("has=equals")]
    [InlineData("has%percent")]
    [InlineData("has+plus")]
    [InlineData("has|pipe")]
    [InlineData("has\0null")]
    [InlineData("@parameterLookalike")]
    [InlineData("has@at")]
    [InlineData("")]
    [InlineData("   ")]
    public void EveryFlavourRejectsWhatCouldBreakOutOfQuoting(string identifier)
    {
        foreach (SqlIdentifierFlavor flavor in AllFlavors)
            Assert.False(SqlIdentifierValidator.IsValid(identifier, flavor), $"{identifier} / {flavor}");
    }

    [Theory]
    [InlineData("id\"; DROP TABLE users; --")]
    [InlineData("a\" AS x, (SELECT 1) AS \"b")]
    [InlineData("1 UNION SELECT password FROM users")]
    public void EveryFlavourRejectsAnInjectionPayload(string identifier)
    {
        foreach (SqlIdentifierFlavor flavor in AllFlavors)
            Assert.False(SqlIdentifierValidator.IsValid(identifier, flavor), $"{identifier} / {flavor}");
    }

    [Fact]
    public void ValidateThrowsArgumentExceptionNamingTheParameter()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => SqlIdentifierValidator.Validate("bad name", "columnName"));

        Assert.Equal("columnName", ex.ParamName);
        Assert.Contains("bad name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateWithoutAFlavourIsTheCommonFlavour()
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.Validate("#temp", "tableName"));
        SqlIdentifierValidator.Validate("#temp", "tableName", SqlIdentifierFlavor.SqlServer);
    }

    [Fact]
    public void NullIsRejected()
    {
        foreach (SqlIdentifierFlavor flavor in AllFlavors)
            Assert.False(SqlIdentifierValidator.IsValid(null, flavor), flavor.ToString());
    }
}
