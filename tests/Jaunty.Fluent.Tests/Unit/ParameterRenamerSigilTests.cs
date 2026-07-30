using Jaunty.Fluent.Internals;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26-056 (batch 5, low/bug). <c>ParameterRenamer.Rename</c> hardcoded the recognised
/// parameter sigils to <c>@</c> and <c>$</c>, so a <c>:</c>-prefixed parameter was mis-parsed
/// rather than renamed.
///
/// <para>
/// <c>paramPrefix</c> fell back to <c>"@"</c>; <c>baseName</c> stayed <c>":p0"</c> because
/// <c>TrimStart('@').TrimStart('$')</c> strips neither; the search pattern became <c>@:p0</c>, which
/// matches nothing in the SQL; and the parameter was registered under <c>@sq0_:p0</c>. The outcome
/// is broken SQL - the fragment keeps a placeholder the outer query never binds - rather than a
/// merge that fails loudly.
/// </para>
///
/// <para>
/// Two neighbours in the same assembly already accept all three sigils:
/// <c>ParameterCollection.ToParameterObject</c> strips <c>'@' or '$' or ':'</c> and
/// <c>JoinParameterName.Qualify</c> accepts the same set. This was the only component that did not,
/// and it is the one that rewrites SQL text.
/// </para>
///
/// <para>
/// Not reachable through a shipped dialect: all four return <c>"@"</c> from <c>ParameterPrefix</c>.
/// It is the assumption <c>docs/specs/008-dialect-parameter-binding</c> exists to remove, so these
/// tests pin the behaviour ahead of that work rather than leaving it to be rediscovered.
/// </para>
/// </summary>
public class ParameterRenamerSigilTests
{
    private static ParameterCollection Params(params (string Name, object? Value)[] entries)
    {
        var collection = new ParameterCollection();
        foreach ((string name, object? value) in entries)
            collection.Add(name, value);

        return collection;
    }

    // ------------------------------------------------------------------
    // The three sigils
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("@")]
    [InlineData("$")]
    [InlineData(":")]
    public void EverySupportedSigil_IsRenamedInBothTheSqlAndTheCollection(string sigil)
    {
        string original = $"SELECT * FROM t WHERE id = {sigil}p0";

        (string sql, ParameterCollection renamed) = ParameterRenamer.Rename(
            original, Params(($"{sigil}p0", 42)), "sq0");

        Assert.Equal($"SELECT * FROM t WHERE id = {sigil}sq0_p0", sql);

        (string name, object? value) = Assert.Single(renamed.GetAll());
        Assert.Equal($"{sigil}sq0_p0", name);
        Assert.Equal(42, value);
    }

    /// <summary>
    /// The sharpest statement of the bug: the renamed name must still appear in the rewritten SQL.
    /// Before the fix the collection said <c>@sq0_:p0</c> while the SQL still said <c>:p0</c>, so
    /// the two disagreed and the query could not bind.
    /// </summary>
    [Fact]
    public void AColonParameter_LeavesNoUnrenamedPlaceholderBehind()
    {
        (string sql, ParameterCollection renamed) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE a = :p0 AND b = :p1",
            Params((":p0", 1), (":p1", 2)),
            "sq1");

        foreach ((string name, _) in renamed.GetAll())
            Assert.Contains(name, sql, StringComparison.Ordinal);

        Assert.DoesNotContain(":p0 ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Repeated sigils
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>TrimStart</c> stripped <em>repeated</em> leading sigils, so a name like <c>@@rowcount</c>
    /// collapsed to base <c>rowcount</c> and produced the pattern <c>@rowcount</c>, which does not
    /// match the <c>@@rowcount</c> in the SQL it came from. Removing exactly one sigil - as
    /// <c>JoinParameterName.Qualify</c> does - keeps the pattern faithful to the original name.
    /// </summary>
    [Fact]
    public void ARepeatedSigil_IsStrippedOnlyOnce()
    {
        (string sql, ParameterCollection renamed) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE n = @@rowcount",
            Params(("@@rowcount", 7)),
            "sq0");

        (string name, _) = Assert.Single(renamed.GetAll());
        Assert.Equal("@sq0_@rowcount", name);
        Assert.Contains(name, sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    [Fact]
    public void AnUnprefixedName_StillDefaultsToAtSign()
    {
        (_, ParameterCollection renamed) = ParameterRenamer.Rename(
            "SELECT 1", Params(("p0", 1)), "sq0");

        (string name, _) = Assert.Single(renamed.GetAll());
        Assert.Equal("@sq0_p0", name);
    }

    /// <summary>
    /// The word-boundary guard still has to stop <c>@p0</c> from rewriting the <c>@p0</c> inside
    /// <c>@p01</c>, which is the reason the pattern exists at all.
    /// </summary>
    [Fact]
    public void ALongerParameterSharingAPrefix_IsNotPartiallyRewritten()
    {
        (string sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE a = @p0 AND b = @p01",
            Params(("@p0", 1), ("@p01", 2)),
            "sq0");

        Assert.Contains("@sq0_p0 ", sql, StringComparison.Ordinal);
        Assert.Contains("@sq0_p01", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyCollection_LeavesTheSqlAlone()
    {
        (string sql, ParameterCollection renamed) = ParameterRenamer.Rename(
            "SELECT 1", new ParameterCollection(), "sq0");

        Assert.Equal("SELECT 1", sql);
        Assert.Empty(renamed.GetAll());
    }
}
