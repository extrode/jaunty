using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-201, 202 and 203 - what <c>ParameterRenamer.Rename</c> may and may not rewrite, and the
/// duplicate guard on <c>ParameterCollection.Add</c> that the same sigil rules imply.
/// </summary>
public class ParameterRenamerContextTests
{
    private static ParameterCollection Params(params (string Name, object? Value)[] entries)
    {
        var collection = new ParameterCollection();
        foreach ((string name, object? value) in entries)
            collection.Add(name, value);

        return collection;
    }

    // ------------------------------------------------------------------
    // AUD-R35-202 (1) - a placeholder's text inside a literal or comment is not a placeholder
    // ------------------------------------------------------------------

    [Fact]
    public void AParameterNameInsideAStringLiteral_IsNotRewritten()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE tag = '@p0' AND id = @p0",
            Params(("@p0", 1)),
            "sq0");

        Assert.Equal("SELECT * FROM t WHERE tag = '@p0' AND id = @sq0_p0", sql);
    }

    [Fact]
    public void AParameterNameInsideALiteralWithADoubledQuote_IsNotRewritten()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE tag = 'it''s @p0' AND id = @p0",
            Params(("@p0", 1)),
            "sq0");

        Assert.Equal("SELECT * FROM t WHERE tag = 'it''s @p0' AND id = @sq0_p0", sql);
    }

    [Fact]
    public void AParameterNameInsideAQuotedIdentifier_IsNotRewritten()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT \"@p0\", [@p0], `@p0` FROM t WHERE id = @p0",
            Params(("@p0", 1)),
            "sq0");

        Assert.Equal("SELECT \"@p0\", [@p0], `@p0` FROM t WHERE id = @sq0_p0", sql);
    }

    [Fact]
    public void AParameterNameInsideACommentIsNotRewritten()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t -- was @p0\nWHERE id = @p0 /* also @p0 */",
            Params(("@p0", 1)),
            "sq0");

        Assert.Equal("SELECT * FROM t -- was @p0\nWHERE id = @sq0_p0 /* also @p0 */", sql);
    }

    // ------------------------------------------------------------------
    // AUD-R35-202 (2) - one rename must not feed the next
    // ------------------------------------------------------------------

    [Fact]
    public void ANameThatAnotherRenameProduces_IsNotRewrittenTwice()
    {
        var (sql, parameters) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE a = @a AND b = @sq0_a",
            Params(("@a", 1), ("@sq0_a", 2)),
            "sq0");

        Assert.Equal("SELECT * FROM t WHERE a = @sq0_a AND b = @sq0_sq0_a", sql);
        Assert.Equal([("@sq0_a", (object?)1), ("@sq0_sq0_a", (object?)2)], parameters.GetAll());
    }

    [Fact]
    public void RenamingIsIndependentOfTheOrderThePlaceholdersAppear()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE b = @sq0_a AND a = @a",
            Params(("@a", 1), ("@sq0_a", 2)),
            "sq0");

        Assert.Equal("SELECT * FROM t WHERE b = @sq0_sq0_a AND a = @sq0_a", sql);
    }

    // ------------------------------------------------------------------
    // Behaviour that had to survive the rewrite
    // ------------------------------------------------------------------

    [Fact]
    public void ALongerNameSharingAPrefix_IsNotRewritten()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE a = @p0 AND b = @p0x",
            Params(("@p0", 1)),
            "sq0");

        Assert.Equal("SELECT * FROM t WHERE a = @sq0_p0 AND b = @p0x", sql);
    }

    [Theory]
    [InlineData("@")]
    [InlineData("$")]
    [InlineData(":")]
    public void EachSigilIsStillRecognised(string sigil)
    {
        var (sql, parameters) = ParameterRenamer.Rename(
            $"SELECT * FROM t WHERE id = {sigil}p0",
            Params(($"{sigil}p0", 1)),
            "sq0");

        Assert.Equal($"SELECT * FROM t WHERE id = {sigil}sq0_p0", sql);
        Assert.Equal($"{sigil}sq0_p0", parameters.GetAll()[0].Name);
    }

    [Fact]
    public void ARepeatedSigilKeepsAllButTheFirst()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT @@rowcount FROM t",
            Params(("@@rowcount", 1)),
            "sq0");

        Assert.Equal("SELECT @sq0_@rowcount FROM t", sql);
    }

    [Fact]
    public void AFragmentWithNoParametersIsReturnedUnchanged()
    {
        const string original = "SELECT * FROM t WHERE tag = '@p0'";

        var (sql, parameters) = ParameterRenamer.Rename(original, new ParameterCollection(), "sq0");

        Assert.Equal(original, sql);
        Assert.Equal(0, parameters.Count);
    }

    // ------------------------------------------------------------------
    // AUD-R35-201 - two names that differ only by their sigil are one parameter
    // ------------------------------------------------------------------

    [Fact]
    public void TwoNamesDifferingOnlyByTheirSigil_AreRejected()
    {
        var collection = new ParameterCollection();
        collection.Add("@p0", 1);

        var ex = Assert.Throws<ArgumentException>(() => collection.Add("p0", 2));

        Assert.Contains("sigil", ex.Message);
    }

    [Theory]
    [InlineData("@p0", "$p0")]
    [InlineData("@p0", ":p0")]
    [InlineData(":p0", "p0")]
    public void EverySigilPairingIsRejected(string first, string second)
    {
        var collection = new ParameterCollection();
        collection.Add(first, 1);

        Assert.Throws<ArgumentException>(() => collection.Add(second, 2));
    }

    [Fact]
    public void ARejectedNameDoesNotLeaveTheCollectionHoldingIt()
    {
        var collection = new ParameterCollection();
        collection.Add("@p0", 1);

        Assert.Throws<ArgumentException>(() => collection.Add("p0", 2));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("p0"));
        Assert.True(collection.Contains("@p0"));
    }

    [Fact]
    public void DistinctNamesAreStillAccepted()
    {
        var collection = new ParameterCollection();
        collection.Add("@p0", 1);
        collection.Add("@p1", 2);
        collection.Add("@@p0", 3);

        Assert.Equal(3, collection.Count);
    }
}
